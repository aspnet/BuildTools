// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;
using NuGet.RuntimeModel;

namespace KoreBuild.Tasks.Lineup
{
    // Executes a NuGet restore, but only downloads the top-level packages and not their dependencies.
    internal class RestoreLineupsCommand : IRestoreLineupCommand
    {
        public async Task<bool> ExecuteAsync(RestoreContext context, CancellationToken cancellationToken)
        {
            var success = true;

            var providersCache = new RestoreCommandProvidersCache();
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = context.Policy.RestoreNoCache;
                cacheContext.IgnoreFailedSources = context.Policy.RestoreIgnoreFailedSources;

                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                var restoreContext = new RestoreArgs
                {
                    CacheContext = cacheContext,
                    DisableParallel = context.Policy.RestoreDisableParallel,
                    Log = context.Log,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    ConfigFile = context.Policy.RestoreConfigFile,
                    PackageSaveMode = PackageSaveMode.Defaultv3,
                    GlobalPackagesFolder = context.Policy.RestorePackagesPath,
                    CachingSourceProvider = sourceProvider,
                    Sources = context.Policy.RestoreSources
                };

                var settings = restoreContext.GetSettings(context.ProjectDirectory);
                var globalFolder = restoreContext.GetEffectiveGlobalPackagesFolder(context.ProjectDirectory, settings);
                var effectiveSources = GetEffectiveSources(settings, restoreContext, context.Policy.RestoreAdditionalSources);
                var sharedCache = providersCache.GetOrCreate(
                    globalFolder,
                    restoreContext.GetEffectiveFallbackPackageFolders(settings),
                    effectiveSources,
                    cacheContext,
                    context.Log);

                var installer = new SimplePackageInstaller(globalFolder, cacheContext, context.Policy.RestoreDisableParallel, context.Log);
                var localRepositories = new List<NuGetv3LocalRepository>
                {
                    sharedCache.GlobalPackages
                };

                localRepositories.AddRange(sharedCache.FallbackPackageFolders);

                var remoteWalkContext = new RemoteWalkContext(cacheContext, context.Log)
                {
                    // don't write msbuild props/targets
                    IsMsBuildBased = false,
                };

                remoteWalkContext.LocalLibraryProviders.AddRange(sharedCache.LocalProviders);
                remoteWalkContext.RemoteLibraryProviders.AddRange(sharedCache.RemoteProviders);

                var remoteWalker = new RemoteDependencyWalker(remoteWalkContext);
                var graphs = new List<GraphNode<RemoteResolveResult>>();

                foreach (var group in context.PackageLineups)
                {
                    var libraryRange = new LibraryRange(group.Id, group.Version, LibraryDependencyTarget.Package);
                    var graph = await remoteWalker.WalkAsync(libraryRange, NuGetFramework.AnyFramework, null, RuntimeGraph.Empty, recursive: false);
                    graphs.Add(graph);
                }

                var restoreTargetGraph = RestoreTargetGraph.Create(graphs, remoteWalkContext, context.Log, NuGetFramework.AnyFramework);
                var allInstalledPackages = new HashSet<LibraryIdentity>();
                await installer.InstallPackagesAsync(new[] { restoreTargetGraph }, allInstalledPackages, cancellationToken);

                foreach (var lineup in context.PackageLineups)
                {
                    var library = restoreTargetGraph.Flattened.FirstOrDefault(g => g.Key.Name.Equals(lineup.Id, StringComparison.OrdinalIgnoreCase))?.Key;
                    if (library == null)
                    {
                        context.Log.LogError($"Could not resolve a package for the lineup '{lineup.RestoreSpec}'.");
                        success = false;
                        break;
                    }

                    var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(localRepositories, library.Name, library.Version);
                    var dependencies = packageInfo.Package.Nuspec.GetDependencyGroups().ToList();

                    if (dependencies == null)
                    {
                        context.Log.LogMinimal($"Could not find dependency info for lineup '{lineup.RestoreSpec}'.");
                        success = false;
                        break;
                    }

                    context.VersionSource.AddPackagesFromLineup(library.Name, library.Version, dependencies);
                }
            }

            return success;
        }

        private List<SourceRepository> GetEffectiveSources(ISettings settings, RestoreArgs context, List<string> additionalSources)
        {
            var sources = new List<PackageSource>();

            if (context.Sources.Any())
            {
                // if explicit sources are specified, use these instead of any that may come from NuGet.config files
                sources.AddRange(context.Sources.Select(s => new PackageSource(s)));
            }
            else
            {
                // load sources from NuGet.config files
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sourcesFromConfig = packageSourceProvider.LoadPackageSources();

                sources.AddRange(sourcesFromConfig.Where(s => s.IsEnabled));
            }

            if (additionalSources.Any())
            {
                // always use any additional sources
                sources.AddRange(additionalSources.Select(s => new PackageSource(s)));
            }

            return sources.Select(entry =>
                context.CachingSourceProvider.CreateRepository(entry))
                .ToList();
        }
    }
}
