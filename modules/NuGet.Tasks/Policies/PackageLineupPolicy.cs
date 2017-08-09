// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NuGet.Build;
using NuGet.Tasks.Lineup;
using NuGet.Tasks.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Tasks.Policies
{
    internal class PackageLineupPolicy : INuGetPolicy
    {
        private readonly IEnumerable<ITaskItem> _items;
        private readonly IRestoreLineupCommand _command;
        private int _pinned;

        public PackageLineupPolicy(ITaskItem[] items)
            : this(items, new RestoreLineupsCommand())
        {
        }

        // for testing
        internal PackageLineupPolicy(ITaskItem[] items, IRestoreLineupCommand command)
        {
            _items = items;
            _command = command;
        }

        public async Task ApplyAsync(PolicyContext context, CancellationToken cancellationToken)
        {
            var logger = new MSBuildLogger(context.Log);
            var versionSource = new PackageVersionSource(logger);
            var lineupRequests = CreateRequests(context);
            if (lineupRequests.Count == 0)
            {
                return;
            }

            logger.LogInformation("Beginning lineup package restore.");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var restoreContext = new RestoreContext
                {
                    Log = logger,
                    PackageLineups = lineupRequests,
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
            var builder = new MSBuildLineupFileBuilder(project.TargetsExtension);

            foreach (var lineup in versionSource.Lineups)
            {
                builder.AddLineup(lineup.Id, lineup.Version.ToNormalizedString());
            }

            foreach (var framework in project.Frameworks)
            {
                foreach (var package in framework.Dependencies)
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
                        builder.PinPackageReference(package, version, framework.TargetFramework);
                        Interlocked.Increment(ref _pinned);
                    }
                    else if (!package.IsImplicitlyDefined && !package.NoWarn)
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
        }

        private List<PackageLineupRequest> CreateRequests(PolicyContext context)
        {
            var lineupRequest = new List<PackageLineupRequest>();

            foreach (var item in _items)
            {
                var packageId = item.ItemSpec;
                var version = item.GetMetadata("Version");

                if (string.IsNullOrEmpty(version))
                {
                    context.Log.LogError($"Missing required metadata 'Version' on lineup policy for {packageId}.");
                    continue;
                }

                if (!VersionRange.TryParse(version, out var versionRange))
                {
                    context.Log.LogError($"Invalid version range '{version}' on package lineup '{packageId}'");
                    continue;
                }

                lineupRequest.Add(new PackageLineupRequest(packageId, versionRange));
            }

            return lineupRequest;
        }
    }
}
