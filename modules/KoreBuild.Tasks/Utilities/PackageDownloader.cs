// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace KoreBuild.Tasks.Utilities
{
    public class PackageDownloader
    {
        private static readonly Task<bool> FalseTask = Task.FromResult(false);
        private readonly NuGet.Common.ILogger logger;

        public PackageDownloader(NuGet.Common.ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> DownloadPackagesAsync(ICollection<PackageDownloadRequest> requests, TimeSpan timeout, CancellationToken cancellationToken)
        {
            logger.LogMinimal($"Downloading {requests.Count} package(s)");

            var cts = new CancellationTokenSource(timeout);
            cancellationToken.Register(() => cts.Cancel());

            using (var cacheContext = new SourceCacheContext())
            using (var throttle = new SemaphoreSlim(8))
            {
                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));
                var tasks = new List<Task<bool>>();

                foreach (var request in requests)
                {
                    var feeds = request.Sources.Select(sourceProvider.CreateRepository);
                    tasks.Add(DownloadPackageAsync(request, feeds, cacheContext, throttle, logger, cts.Token));
                }

                var all = Task.WhenAll(tasks);
                var delay = Task.Delay(timeout);

                var finished = await Task.WhenAny(all, delay);
                if (ReferenceEquals(delay, finished))
                {
                    logger.LogError($"Timed out after {timeout.TotalSeconds}s");
                    cts.Cancel();
                    return false;
                }

                if (!tasks.All(a => a.Result))
                {
                    logger.LogError("Failed to download all packages");
                    return false;
                }

                return true;
            }
        }

        private async Task<bool> DownloadPackageAsync(
            PackageDownloadRequest request,
            IEnumerable<SourceRepository> repositories,
            SourceCacheContext cacheContext,
            SemaphoreSlim throttle,
            NuGet.Common.ILogger logger,
            CancellationToken cancellationToken)
        {
            foreach (var repo in repositories)
            {
                var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

                if (findPackageByIdResource == null)
                {
                    logger.LogError($"{nameof(FindPackageByIdResource)} for '{repo}' could not be loaded.");
                    return false;
                }

                var downloader = await findPackageByIdResource.GetPackageDownloaderAsync(request.Identity, cacheContext, logger, cancellationToken);
                if (downloader == null)
                {
                    logger.LogInformation($"Package {request.Identity.Id} {request.Identity.Version} is not available on '{repo}'");
                    
                    // Skip to the next source if a package cannot be found in a given source.
                    continue;
                }

                downloader.SetThrottle(throttle);
                if (!await downloader.CopyNupkgFileToAsync(request.OutputPath, cancellationToken))
                {
                    logger.LogError($"Could not download {request.Identity.Id} {request.Identity.Version} from {repo}.");
                    return false;
                }

                return true;
            }

            logger.LogError($"{request.Identity.Id} {request.Identity.Version} is not available.'");
            return false;
        }
    }
}
