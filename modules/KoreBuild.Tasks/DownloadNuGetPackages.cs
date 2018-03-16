// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Build;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Downloads NuGet packages in parallel.
    /// </summary>
    public class DownloadNuGetPackages : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The NuGet packages to download. Expected form:
        ///
        /// ItemSpec = PackageID
        ///
        /// Metadata:
        ///   Version = the exact package version to download
        ///   Source = the NuGet feed (remote or folder)
        /// </summary>
        [Required]
        public ITaskItem[] Packages { get; set; }

        /// <summary>
        /// The directory for download NuGet files. The task will write files to $(DestinationFolder)/$(PackageId.ToLower()).$(Version).nupkg
        /// </summary>
        [Required]
        public string DestinationFolder { get; set; }

        /// <summary>
        /// The package files that were downloaded.
        /// </summary>
        [Output]
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// The maximum amount of time to allow for downloading packages.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60 * 5;

        public void Cancel() => _cts.Cancel();

        public override bool Execute()
        {
            return ExecuteAsync().Result;
        }

        public async Task<bool> ExecuteAsync()
        {
            DestinationFolder = DestinationFolder.Replace('\\', '/');

            var requests = new List<PackageDownloadRequest>();
            var files = new List<ITaskItem>();
            var downloadCount = 0;
            foreach (var item in Packages)
            {
                var id = item.ItemSpec;
                var rawVersion = item.GetMetadata("Version");
                if (!NuGetVersion.TryParse(rawVersion, out var version))
                {
                    Log.LogError($"Package '{id}' has an invalid 'Version' metadata value: '{rawVersion}'.");
                    return false;
                }

                var source = item.GetMetadata("Source");
                if (string.IsNullOrEmpty(source))
                {
                    Log.LogError($"Package '{id}' is missing the 'Source' metadata value.");
                    return false;
                }

                var outputPath = Path.Combine(DestinationFolder, $"{id.ToLowerInvariant()}.{version.ToNormalizedString()}.nupkg");

                files.Add(new TaskItem(outputPath));
                if (File.Exists(outputPath))
                {
                    Log.LogMessage($"Skipping {id} {version}. Already exists in '{outputPath}'");
                    continue;
                }
                else
                {
                    downloadCount++;

                    var request = new PackageDownloadRequest
                    {
                        Identity = new PackageIdentity(id, version),
                        OutputPath = outputPath,
                        Sources = source.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                    };

                    requests.Add(request);
                }
            }

            Files = files.ToArray();

            if (downloadCount == 0)
            {
                Log.LogMessage("All packages are downloaded.");
                return true;
            }

            Directory.CreateDirectory(DestinationFolder);
            var logger = new MSBuildLogger(Log);
            var timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            var downloader = new PackageDownloader(logger);
            var timer = Stopwatch.StartNew();

            var result = await downloader.DownloadPackagesAsync(requests, timeout, _cts.Token);

            timer.Stop();
            logger.LogMinimal($"Finished downloading {requests.Count} package(s) in {timer.ElapsedMilliseconds}ms");
            return result;
        }
    }
}
