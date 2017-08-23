// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace KoreBuild.Tasks.Lineup
{
    internal class SimplePackageInstaller
    {
        private readonly string _packagesDirectory;
        private readonly ILogger _logger;
        private readonly SourceCacheContext _cacheContext;
        private readonly int _maxDegreeOfConcurrency;

        public SimplePackageInstaller(string packageDir, SourceCacheContext cacheContext, bool disableParallel, ILogger logger)
        {
            _packagesDirectory = packageDir;
            _cacheContext = cacheContext;
            _logger = logger;
            _maxDegreeOfConcurrency = disableParallel ? 1 : 16;
        }

        public async Task InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            HashSet<LibraryIdentity> allInstalledPackages,
            CancellationToken token)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (_maxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackageAsync(match, token);
                }
            }
            else
            {
                var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                var tasks = Enumerable.Range(0, _maxDegreeOfConcurrency)
                    .Select(async _ =>
                    {
                        while (bag.TryTake(out RemoteMatch match))
                        {
                            await InstallPackageAsync(match, token);
                        }
                    });
                await Task.WhenAll(tasks);
            }
        }

        private async Task InstallPackageAsync(RemoteMatch installItem, CancellationToken token)
        {
            var packageIdentity = new PackageIdentity(installItem.Library.Name, installItem.Library.Version);

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                _packagesDirectory,
                _logger,
                PackageSaveMode.Defaultv3,
                XmlDocFileSaveMode.Skip);

            using (var packageDependency = await installItem.Provider.GetPackageDownloaderAsync(
                packageIdentity,
                _cacheContext,
                _logger,
                token))
            {
                await PackageExtractor.InstallFromSourceAsync(
                    packageDependency,
                    versionFolderPathContext,
                    token);
            }
        }
    }
}
