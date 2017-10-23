// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
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
            {
                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));
                var tasks = new List<Task<bool>>();

                foreach (var feed in requests.GroupBy(r => r.Source, StringComparer.OrdinalIgnoreCase))
                {
                    var repo = sourceProvider.CreateRepository(new PackageSource(feed.Key));
                    tasks.Add(DownloadPackagesAsync(repo, feed, cacheContext, logger, cts.Token));
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

        private async Task<bool> DownloadPackagesAsync(
            SourceRepository repo,
            IEnumerable<PackageDownloadRequest> requests,
            SourceCacheContext cacheContext,
            NuGet.Common.ILogger logger,
            CancellationToken cancellationToken)
        {
            var remoteLibraryProvider = new SourceRepositoryDependencyProvider(repo, logger, cacheContext, ignoreFailedSources: false, ignoreWarning: false);
            var metadataResource = await repo.GetResourceAsync<MetadataResource>();

            if (metadataResource == null)
            {
                logger.LogError($"MetadataResource for '{repo}' could not be loaded.");
                return false;
            }

            var downloads = new List<Task<bool>>();
            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await metadataResource.Exists(request.Identity, logger, cancellationToken))
                {
                    logger.LogError($"Package {request.Identity.Id} {request.Identity.Version} is not available on '{repo}'");
                    downloads.Add(FalseTask);
                    continue;
                }

                var download = DownloadPackageAsync(cacheContext, logger, remoteLibraryProvider, request, cancellationToken);
                downloads.Add(download);
            }

            await Task.WhenAll(downloads);
            return downloads.All(d => d.Result);
        }

        private async Task<bool> DownloadPackageAsync(SourceCacheContext cacheContext,
            NuGet.Common.ILogger logger,
            SourceRepositoryDependencyProvider remoteLibraryProvider,
            PackageDownloadRequest request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation($"Downloading {request.Identity.Id} {request.Identity.Version} to '{request.OutputPath}'");

            using (var packageDependency = await remoteLibraryProvider.GetPackageDownloaderAsync(request.Identity, cacheContext, logger, cancellationToken))
            {
                if (!await packageDependency.CopyNupkgFileToAsync(request.OutputPath, cancellationToken))
                {
                    logger.LogError($"Could not download {request.Identity.Id} {request.Identity.Version} from {remoteLibraryProvider.Source}");
                    return false;
                }
            }

            return true;
        }
    }
}
