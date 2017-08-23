// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Build;
using KoreBuild.Tasks.Lineup;
using KoreBuild.Tasks.ProjectModel;
using NuGet.Versioning;
using Task = System.Threading.Tasks.Task;

namespace KoreBuild.Tasks.Policies
{
    internal class PackageLineupPolicy : INuGetPolicy
    {
        private readonly static string NoWarn = KoreBuildErrors.Prefix + KoreBuildErrors.PackageReferenceHasVersion;

        private readonly IReadOnlyList<ITaskItem> _items;
        private readonly IRestoreLineupCommand _command;
        private readonly TaskLoggingHelper _logger;
        private int _pinned;
        private readonly List<string> _additionalSources = new List<string>();
        private readonly List<PackageLineupRequest> _restoreLineupRequests = new List<PackageLineupRequest>();

        public PackageLineupPolicy(IReadOnlyList<ITaskItem> items, TaskLoggingHelper logger)
            : this(items, new RestoreLineupsCommand(), logger)
        {
        }

        // for testing
        internal PackageLineupPolicy(IReadOnlyList<ITaskItem> items, IRestoreLineupCommand command, TaskLoggingHelper logger)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ApplyAsync(PolicyContext context, CancellationToken cancellationToken)
        {
            if (_items.Count == 0)
            {
                return;
            }

            var logger = new MSBuildLogger(context.Log);
            var versionSource = new PackageVersionSource(logger);

            InitializeRequests(context, versionSource);

            logger.LogInformation("Beginning lineup package restore.");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_restoreLineupRequests.Count > 0)
                {
                    var restoreContext = new RestoreContext
                    {
                        Log = logger,
                        PackageLineups = _restoreLineupRequests,
                        VersionSource = versionSource,
                        Policy = context,
                        ProjectDirectory = context.SolutionDirectory
                    };

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var success = await _command.ExecuteAsync(restoreContext, cancellationToken);

                    if (!success)
                    {
                        stopwatch.Stop();
                        logger.LogError($"Failed to pin packages. Time elapsed = {stopwatch.ElapsedMilliseconds}ms");
                        return;
                    }
                }

                var lineupCount = 0;
                foreach (var lineup in versionSource.Lineups)
                {
                    logger.LogMinimal($"Using lineup {lineup.Id} {lineup.Version.ToNormalizedString()}");
                    lineupCount++;
                }

                _pinned = 0;
                Parallel.ForEach(context.Projects, project =>
                {
                    GeneratePinFile(context, project, versionSource);
                });

                stopwatch.Stop();
                logger.LogMinimal($"Pinned {_pinned} package(s) from {lineupCount} lineup(s) in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
#if DEBUG
                var showStackTrace = true;
#else
                var showStackTrace = false;
#endif
                context.Log.LogErrorFromException(ex, showStackTrace);
            }
        }

        private void GeneratePinFile(PolicyContext context, ProjectInfo project, PackageVersionSource versionSource)
        {
            var builder = project.TargetsExtension;

            foreach (var lineup in versionSource.Lineups)
            {
                builder.AddLineup(lineup.Id, lineup.Version.ToNormalizedString());
            }

            foreach (var framework in project.Frameworks)
            {
                foreach (var package in framework.Dependencies.Values)
                {
                    var shouldOverride =
                        // true for references that come from the SDK, like Microsoft.NETCore.App
                        package.IsImplicitlyDefined
                        // if the original PackageReference is version-less
                        || string.IsNullOrEmpty(package.Version);

                    if (!shouldOverride)
                    {
                        continue;
                    }

                    if (versionSource.TryGetPackageVersion(package.Id, out string version))
                    {
                        builder.PinPackageReference(package.Id, version, framework.TargetFramework);
                        Interlocked.Increment(ref _pinned);
                    }
                    else if (!package.IsImplicitlyDefined
                        && (package.NoWarn.Count == 0 || !package.NoWarn.Any(i => i.Equals(NoWarn, StringComparison.Ordinal))))
                    {
                        context.Log.LogKoreBuildError(project.FullPath, KoreBuildErrors.PackageVersionNotFoundInLineup,
                            $"PackageReference to {package.Id} did not specify a version and was not found in any lineup.");
                    }
                }
            }

            foreach (var toolPackage in project.Tools)
            {
                if (!string.IsNullOrEmpty(toolPackage.Version))
                {
                    continue;
                }

                if (versionSource.TryGetPackageVersion(toolPackage.Id, out string version))
                {
                    builder.PinCliToolReference(toolPackage, version);
                    Interlocked.Increment(ref _pinned);
                }
                else
                {
                    context.Log.LogKoreBuildError(project.FullPath, KoreBuildErrors.PackageVersionNotFoundInLineup,
                        $"DotNetCliToolReference to {toolPackage.Id} did not specify a version and was not found in any lineup.");
                }
            }

            foreach (var source in _additionalSources)
            {
                project.TargetsExtension.AddAdditionalRestoreSource(source);
            }
        }

        private void InitializeRequests(PolicyContext context, PackageVersionSource versionSource)
        {
            foreach (var item in _items)
            {
                var type = item.GetMetadata("LineupType") ?? string.Empty;

                switch (type.ToLowerInvariant())
                {
                    case "folder":
                        InitializeFolderLineup(item, versionSource);
                        break;
                    case "package":
                        InitializePackageLineup(item);
                        break;
                    default:
                        _logger.LogError($"Unrecognized value of LineupType '{type}' on {item.ItemSpec}");
                        break;
                }

            }
        }

        private void InitializeFolderLineup(ITaskItem item, PackageVersionSource versionSource)
        {
            if (!Directory.Exists(item.ItemSpec))
            {
                _logger.LogWarning($"Expected directory of packages '{item.ItemSpec}' to exist but it was not found. Skipping.");
                return;
            }

            _additionalSources.Add(item.ItemSpec);

            versionSource.AddPackagesFromFolder(item.ItemSpec);
        }

        private void InitializePackageLineup(ITaskItem item)
        {
            var packageId = item.ItemSpec;
            var version = item.GetMetadata("Version");

            if (string.IsNullOrEmpty(version))
            {
                _logger.LogError($"Missing required metadata 'Version' on lineup policy for {packageId}.");
                return;
            }

            if (!VersionRange.TryParse(version, out var versionRange))
            {
                _logger.LogError($"Invalid version range '{version}' on package lineup '{packageId}'");
                return;
            }

            _restoreLineupRequests.Add(new PackageLineupRequest(packageId, versionRange));
        }
    }
}
