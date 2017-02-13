// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NugetReferenceResolver
{
    public class PackageGraph
    {
        public PackageGraph(
            IEnumerable<string> packageFolders,
            IDictionary<string, Package> allPackages,
            IEnumerable<Package> packages,
            string targetFrameworkName)
        {
            PackageSources = packageFolders;
            AllPackages = allPackages;
            Dependencies = packages;
            Framework = targetFrameworkName;
        }

        public PackageGraph GetClosure(string packageId)
        {
            if (!AllPackages.TryGetValue(packageId, out var package))
            {
                return null;
            }

            var allPackages = new Dictionary<string, Package>();
            CollectPackages(package, allPackages);
            return new PackageGraph(PackageSources, allPackages, new[] { package }, Framework);
        }

        private void CollectPackages(Package package, IDictionary<string, Package> allPackages)
        {
            var remainingPackages = new Stack<Package>();
            remainingPackages.Push(package);
            while (remainingPackages.Count > 0)
            {
                var current = remainingPackages.Pop();
                if (!allPackages.ContainsKey(current.Name))
                {
                    allPackages.Add(current.Name, current);
                    foreach (var dependency in current.Dependencies)
                    {
                        if (!allPackages.ContainsKey(dependency.Name))
                        {
                            remainingPackages.Push(dependency);
                        }
                    }
                }
            }
        }

        public PackageGraph WithoutPackage(string packageId)
        {
            var packages = new Dictionary<string, Package>(AllPackages, StringComparer.OrdinalIgnoreCase);
            var package = packages.FirstOrDefault(p => p.Key.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package.Value != null)
            {
                var packagesToExclude = GetPackagesToExclude(package);
                foreach (var exclusion in packagesToExclude)
                {
                    packages.Remove(exclusion.Key);
                }

                var newDependencies = Dependencies.ToList();
                foreach (var dependency in Dependencies)
                {
                    if (packagesToExclude.ContainsKey(dependency.Name))
                    {
                        newDependencies.Remove(dependency);
                    }
                }

                foreach (var dependency in newDependencies.ToArray())
                {
                    var newDependency = RemovePackagesFromTransitiveDependencies(dependency, packagesToExclude);
                    if (newDependency != dependency)
                    {
                        newDependencies.Remove(dependency);
                        newDependencies.Add(newDependency);
                    }
                }

                return new PackageGraph(PackageSources, packages, newDependencies, Framework);
            }

            return this;
        }

        private Package RemovePackagesFromTransitiveDependencies(
            Package dependency,
            Dictionary<string, Package> packagesToExclude)
        {
            var dependenciesModified = false;
            var newDependencies = new List<Package>();
            foreach (var package in dependency.Dependencies)
            {
                if (!packagesToExclude.ContainsKey(package.Name))
                {
                    var newDependency = RemovePackagesFromTransitiveDependencies(package, packagesToExclude);
                    if (newDependency != package || dependenciesModified)
                    {
                        dependenciesModified = true;
                    }
                    newDependencies.Add(newDependency);
                }
                else
                {
                    dependenciesModified = true;
                }
            }

            if (dependenciesModified)
            {
                var newPackage = new Package(dependency);
                newPackage.Dependencies = newDependencies;

                return newPackage;
            }
            else
            {
                return dependency;
            }
        }

        private static Dictionary<string, Package> GetPackagesToExclude(KeyValuePair<string, Package> package)
        {
            var packagesToExclude = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
            var pendingDependencies = new Stack<Package>();
            pendingDependencies.Push(package.Value);
            while (pendingDependencies.Count > 0)
            {
                var current = pendingDependencies.Pop();
                if (!packagesToExclude.ContainsKey(current.Name))
                {
                    packagesToExclude.Add(current.Name, current);
                    foreach (var dependency in current.Dependencies)
                    {
                        pendingDependencies.Push(dependency);
                    }
                }
            }

            return packagesToExclude;
        }

        public IEnumerable<Package> Dependencies { get; set; }

        public IDictionary<string, Package> AllPackages { get; set; }

        public IEnumerable<string> PackageSources { get; set; }

        public string Framework { get; set; }

        public static PackageGraph Create(LockFile lockFile, string targetFrameworkName)
        {
            var runtimeIdentifier = RuntimeGraph.GetCurrentRuntimeId();
            var fallbacks = RuntimeGraph.GetCompatibleRuntimes(runtimeIdentifier);

            var parsedFramework = NuGetFramework.Parse(targetFrameworkName);
            var dependencyGroup = FindCompatibleDependencyGroup(lockFile, parsedFramework);
            if (dependencyGroup == null)
            {
                return null;
            }

            var chosenFramework = NuGetFramework.Parse(dependencyGroup.FrameworkName);
            var targetFramework = lockFile.Targets
                .FirstOrDefault(t => t.TargetFramework.Equals(chosenFramework) &&
                    runtimeIdentifier.Equals(t.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase));

            targetFramework = targetFramework ??
                lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(chosenFramework) &&
                    RuntimeIsCompatible(t.RuntimeIdentifier, runtimeIdentifier, fallbacks));

            targetFramework = targetFramework ?? lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(chosenFramework) &&
                t.RuntimeIdentifier == null);

            var directDependencies = targetFramework
                .Libraries
                .Where(l => dependencyGroup.Dependencies.Any(d => d.StartsWith(l.Name, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            var allPackages = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);

            var sources = lockFile.PackageFolders.Select(p => p.Path).ToArray();

            var packages = directDependencies
                .Select(dd => CreatePackage(dd, targetFramework, lockFile.Libraries, allPackages, sources)).ToArray();

            return new PackageGraph(sources, allPackages, packages, chosenFramework.GetShortFolderName());
        }

        private static ProjectFileDependencyGroup FindCompatibleDependencyGroup(LockFile lockFile, NuGetFramework parsedFramework)
        {
            return lockFile.ProjectFileDependencyGroups.FirstOrDefault(g => NuGetFramework.Parse(g.FrameworkName).Equals(parsedFramework)) ??
                lockFile.ProjectFileDependencyGroups.FirstOrDefault(g => IsCompatible(parsedFramework, NuGetFramework.Parse(g.FrameworkName)));
        }

        private static bool IsCompatible(NuGetFramework reference, NuGetFramework target)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(reference, target);
        }

        private static bool RuntimeIsCompatible(string runtime, string candidateRuntime, IEnumerable<string> fallbacks)
        {
            return candidateRuntime.Equals(runtime, StringComparison.OrdinalIgnoreCase) ||
                fallbacks.Any(f => f.Equals(runtime, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> GetAssembliesFullPath() =>
            AllPackages.SelectMany(p => p.Value.Assemblies.Select(a => a.ResolvedPath));

        private static Package CreatePackage(
            LockFileTargetLibrary dependency,
            LockFileTarget targetFramework,
            IList<LockFileLibrary> libraries,
            IDictionary<string, Package> packageDictionary,
            IEnumerable<string> sources)
        {
            var library = libraries.First(l => l.Name.Equals(dependency.Name) && l.Version.Equals(dependency.Version));
            if (packageDictionary.TryGetValue(library.Name, out var package))
            {
                return package;
            }

            var dependencies = new List<Package>();
            if (dependency.Dependencies?.Count > 0)
            {
                foreach (var d in dependency.Dependencies)
                {
                    Package dependentPackage;
                    if (!packageDictionary.TryGetValue(d.Id, out dependentPackage))
                    {
                        dependentPackage = CreatePackage(
                            FindLibrary(targetFramework, d), targetFramework, libraries, packageDictionary, sources);
                    }
                    else
                    {
                    }

                    dependencies.Add(dependentPackage);
                }
            }

            string packagePath = ResolvePackagePath(library.Name, library.Version.ToString(), sources);
            package = new Package(
                library.Name,
                library.Version.ToString(),
                packagePath,
                library.Files.Single(f => f.EndsWith("sha512", StringComparison.OrdinalIgnoreCase)),
                dependencies,
                dependency
                    ?.RuntimeAssemblies
                    ?.Select(rt => rt.Path)
                    ?.Where(p => !p.EndsWith("_._"))
                    ?.ToArray() ?? Enumerable.Empty<string>());

            packageDictionary.Add(package.Name, package);

            return package;
        }

        private static string ResolvePath(string path, string libraryName, string version, IEnumerable<string> sources)
        {
            foreach (var source in sources)
            {
                var fullPath = Path.Combine(source, libraryName, version, path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return path;
        }

        private static string ResolvePackagePath(string libraryName, string version, IEnumerable<string> sources)
        {
            foreach (var source in sources)
            {
                var fullPath = Path.Combine(source, libraryName, version);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return "";
        }

        private static LockFileTargetLibrary FindLibrary(LockFileTarget targetFramework, PackageDependency d)
        {
            return targetFramework.Libraries.First(l => d.Id.Equals(l.Name));
        }
    }
}
