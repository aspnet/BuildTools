// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using NuGet.Build;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Uses a remote package to update the version variables in the local dependencies.props file
    /// </summary>
    public class UpgradeDependencies : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The lineup package ID of the nupkg that contains the master dependencies.props files that will be used to upgrade versions
        /// </summary>
        [Required]
        public string LineupPackageId { get; set; }

        /// <summary>
        /// The version of the lineup package. If empty, this will attempt to pull the latest
        /// </summary>
        public string LineupPackageVersion { get; set; }

        /// <summary>
        /// The dependencies.props file to use versions from
        /// </summary>
        public string LineupDependenciesFile { get; set; }

        /// <summary>
        /// The NuGet feed containing the lineup package
        /// </summary>
        [Required]
        public string LineupPackageRestoreSource { get; set; }

        /// <summary>
        /// The dependencies.props file to update
        /// </summary>
        [Required]
        public string DependenciesFile { get; set; }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            if (!DependencyVersionsFile.TryLoad(DependenciesFile, out var localVersionsFile))
            {
                Log.LogError($"Could not load file from {DependenciesFile}");
                return false;
            }

            if (!localVersionsFile.HasVersionsPropertyGroup())
            {
                Log.LogKoreBuildWarning(KoreBuildErrors.PackageRefPropertyGroupNotFound, $"No PropertyGroup with Label=\"{DependencyVersionsFile.PackageVersionsLabel}\" could be found in {DependenciesFile}");
            }

            if (localVersionsFile.VersionVariables.Count == 0)
            {
                Log.LogMessage(MessageImportance.High, $"No version variables could be found in {DependenciesFile}");
                return true;
            }


            var tmpNupkgPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var logger = new MSBuildLogger(Log);

            try
            {
                var remoteDepsVersionFile = await TryDownloadLineupDepsFile() ?? await TryDownloadLineupPackage(logger, tmpNupkgPath);

                if (remoteDepsVersionFile == null)
                {
                    return false;
                }

                var updateCount = UpdateDependencies(localVersionsFile, remoteDepsVersionFile);

                if (updateCount > 0)
                {
                    Log.LogMessage($"Finished updating {updateCount} version variables in {DependenciesFile}");
                    localVersionsFile.Save(DependenciesFile);
                }
                else
                {
                    Log.LogMessage($"Versions in {DependenciesFile} are already up to date");
                }

                return !Log.HasLoggedErrors;
            }
            finally
            {
                if (File.Exists(tmpNupkgPath))
                {
                    File.Delete(tmpNupkgPath);
                }
            }
        }

        private async Task<DependencyVersionsFile> TryDownloadLineupDepsFile()
        {
            if (string.IsNullOrEmpty(LineupDependenciesFile))
            {
                return null;
            }

            var path = LineupDependenciesFile;
            string text;
            if (path.StartsWith("http"))
            {
                using (var client = new HttpClient())
                {
                    text = await client.GetStringAsync(path);
                }
            }
            else
            {
                text = File.ReadAllText(path);
            }

            using (var stringReader = new StringReader(text))
            using (var reader = new XmlTextReader(stringReader))
            {
                var projectRoot = ProjectRootElement.Create(reader);
                return DependencyVersionsFile.Load(projectRoot);
            }
        }

        private async Task<DependencyVersionsFile> TryDownloadLineupPackage(MSBuildLogger logger, string tmpNupkgPath)
        {
            VersionRange versionRange;

            if (string.IsNullOrEmpty(LineupPackageVersion))
            {
                versionRange = VersionRange.AllFloating;
            }
            else if (!VersionRange.TryParse(LineupPackageVersion, out versionRange))
            {
                Log.LogError($"{LineupPackageVersion} is not a valid NuGet package version");
                return null;
            }

            var packageVersion = await GetPackageVersion(versionRange);
            if (packageVersion == null)
            {
                Log.LogError($"Could not find a version of {LineupPackageId} in the version range {versionRange}.");
                return null;
            }

            var packageId = new PackageIdentity(LineupPackageId, packageVersion);

            var request = new PackageDownloadRequest
            {
                Identity = packageId,
                OutputPath = tmpNupkgPath,
                Sources = new[] { LineupPackageRestoreSource },
            };

            var result = await new PackageDownloader(logger).DownloadPackagesAsync(new[] { request }, TimeSpan.FromSeconds(60), _cts.Token);

            if (!result)
            {
                Log.LogError("Could not download the lineup package");
                return null;
            }

            using (var nupkgReader = new PackageArchiveReader(tmpNupkgPath))
            using (var stream = nupkgReader.GetStream("build/dependencies.props"))
            using (var reader = new XmlTextReader(stream))
            {
                var projectRoot = ProjectRootElement.Create(reader);
                return  DependencyVersionsFile.Load(projectRoot);
            }
        }

        private int UpdateDependencies(DependencyVersionsFile localVersionsFile, DependencyVersionsFile remoteDepsVersionFile)
        {
            var updateCount = 0;
            foreach (var var in localVersionsFile.VersionVariables)
            {
                string newValue;
                // special case any package bundled in KoreBuild
                if (!string.IsNullOrEmpty(KoreBuildVersion.Current) && var.Key == "InternalAspNetCoreSdkPackageVersion")
                {
                    newValue = KoreBuildVersion.Current;
                    Log.LogMessage(MessageImportance.Low, "Setting InternalAspNetCoreSdkPackageVersion to the current version of KoreBuild");
                }
                else if (!remoteDepsVersionFile.VersionVariables.TryGetValue(var.Key, out newValue))
                {
                    Log.LogKoreBuildWarning(
                        DependenciesFile, KoreBuildErrors.PackageVersionNotFoundInLineup,
                        $"A new version variable for {var.Key} could not be found in {LineupPackageId}. This might be an unsupported external dependency.");
                    continue;
                }

                if (newValue != var.Value)
                {
                    updateCount++;
                    localVersionsFile.Set(var.Key, newValue);
                }
            }

            return updateCount;
        }

        private async Task<NuGetVersion> GetPackageVersion(VersionRange range)
        {
            if (!range.IsFloating)
            {
                return range.MinVersion;
            }

            using (var cacheContext = new SourceCacheContext())
            {
                var log = new MSBuildLogger(Log);
                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                var repo = sourceProvider.CreateRepository(new PackageSource(LineupPackageRestoreSource));

                var metadata = await repo.GetResourceAsync<MetadataResource>();
                if (!await metadata.Exists(LineupPackageId, cacheContext, log, _cts.Token))
                {
                    Log.LogError($"Package {LineupPackageId} is not available on '{repo}'");
                    return null;
                }

                try
                {
                    var versions = await metadata.GetVersions(LineupPackageId, includePrerelease: true, includeUnlisted: false, sourceCacheContext: cacheContext, log: log, token: _cts.Token);

                    return range.FindBestMatch(versions);
                }
                catch (Exception ex)
                {
                    Log.LogError($"Unexpected error while fetching versions from {repo.PackageSource.Source}: " + ex.Message);
                    return null;
                }
            }
        }
    }
}
