// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;

namespace ApiCheck
{
    public abstract class AssemblyLoader
    {
        private readonly IDictionary<AssemblyName, string> _assemblyPaths;
        private readonly IDictionary<string, string> _nativeLibraries;
        private readonly string _searchPath;

        private static readonly string[] NativeLibraryExtensions;
        private static readonly string[] ManagedAssemblyExtensions = new[]
        {
            ".dll",
            ".ni.dll",
            ".exe",
            ".ni.exe"
        };

        static AssemblyLoader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NativeLibraryExtensions = new[] { ".dll" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                NativeLibraryExtensions = new[] { ".dylib" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                NativeLibraryExtensions = new[] { ".so" };
            }
            else
            {
                NativeLibraryExtensions = new string[0];
            }
        }

        public AssemblyLoader(LibraryExport[] exports, string runtime, string outputPath)
        {
            _assemblyPaths = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);
            _nativeLibraries = new Dictionary<string, string>();
            var rids = DependencyContext.Default?.RuntimeGraph ?? Enumerable.Empty<RuntimeFallbacks>();
            var runtimeIdentifier = runtime;
            var fallbacks = rids.FirstOrDefault(r => r.Runtime.Equals(runtimeIdentifier));

            foreach (var export in exports)
            {
                // Process managed assets
                var group = string.IsNullOrEmpty(runtimeIdentifier) ?
                    export.RuntimeAssemblyGroups.GetDefaultGroup() :
                    GetGroup(export.RuntimeAssemblyGroups, runtimeIdentifier, fallbacks);
                if (group != null && group.Assets.Count > 0)
                {
                    foreach (var asset in group.Assets)
                    {
                        _assemblyPaths[asset.GetAssemblyName()] = asset.ResolvedPath;
                    }
                }
                else
                {
                    // Fallback to runtime specific assembly group.
                    var candidateRids = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers();
                    foreach (var candidate in candidateRids)
                    {
                        var candidateFallbacks = rids.FirstOrDefault(r => r.Runtime.Equals(candidate));

                        var fallbackGroup = GetGroup(export.RuntimeAssemblyGroups, candidate, candidateFallbacks);
                        if (fallbackGroup != null)
                        {
                            foreach (var asset in fallbackGroup.Assets)
                            {
                                _assemblyPaths[asset.GetAssemblyName()] = asset.ResolvedPath;
                            }
                            break;
                        }
                    }
                }

                // Process native assets
                group = string.IsNullOrEmpty(runtimeIdentifier) ?
                    export.NativeLibraryGroups.GetDefaultGroup() :
                    GetGroup(export.NativeLibraryGroups, runtimeIdentifier, fallbacks);
                if (group != null)
                {
                    foreach (var asset in group.Assets)
                    {
                        _nativeLibraries[asset.Name] = asset.ResolvedPath;
                    }
                }
            }

            _searchPath = outputPath;
        }

        public static Assembly LoadAssembly(
                string assemblyPath,
                string projectJson,
                string configuration,
                string packagesFolder)
        {
            var ctx = CreateProjectContext(projectJson, packagesFolder);
            var exporter = ctx.CreateExporter(configuration);
            var dependencies = exporter.GetDependencies().ToArray();

#if NETCOREAPP1_0
            var loader = new CoreClrAssemblyLoader(dependencies, ctx.RuntimeIdentifier, ctx.GetOutputPaths(configuration)?.CompilationOutputPath);
#else
            var loader = new FullFrameworkAssemblyLoader(dependencies, ctx.RuntimeIdentifier, ctx.GetOutputPaths(configuration)?.CompilationOutputPath);
#endif            
            return loader.Load(assemblyPath);
        }

        public abstract Assembly Load(string assemblyPath);
        protected abstract AssemblyName GetAssemblyName(string assemblyPath);

        protected string FindAssemblyPath(AssemblyName name)
        {
            string path;
            if (_assemblyPaths.TryGetValue(name, out path) || SearchForLibrary(ManagedAssemblyExtensions, name.Name, out path))
            {
                return path;
            }

            return null;
        }

        private bool SearchForLibrary(string[] extensions, string name, out string path)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(_searchPath, name + extension);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private static ProjectContext CreateProjectContext(string projectJson, string packagesFolder)
        {
#if NETCOREAPP1_0
            var resolvedFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
#else
            var resolvedFramework = FrameworkConstants.CommonFrameworks.Net452;
#endif
            var project = ProjectReader.GetProject(projectJson);

            var targetFramework = project.GetTargetFrameworks().Where(t =>
#if NETCOREAPP1_0
                    t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.NetCoreApp10.Framework) ||
                        t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.NetStandard.Framework)
#else
                    t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.Net45.Framework)
#endif
            ).First();

            var builder = new ProjectContextBuilder()
            .WithProject(project)
            .WithTargetFramework(targetFramework.FrameworkName)
            .WithRuntimeIdentifiers(RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            return builder.Build();
        }

        private static bool IsCompatible(NuGetFramework reference, NuGetFramework target)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(reference, target);
        }

        private static LibraryAssetGroup GetGroup(IEnumerable<LibraryAssetGroup> groups, string runtimeIdentifier, RuntimeFallbacks fallbacks)
        {
            IEnumerable<string> rids = new[] { runtimeIdentifier };
            if (fallbacks != null)
            {
                rids = Enumerable.Concat(rids, fallbacks.Fallbacks);
            }

            foreach (var rid in rids)
            {
                var group = groups.GetRuntimeGroup(rid);
                if (group != null)
                {
                    return group;
                }
            }
            return null;
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static readonly IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

            private AssemblyNameComparer()
            {
            }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                // Ignore case because that's what Assembly.Load does.
                return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.CultureName ?? string.Empty, y.CultureName ?? string.Empty, StringComparison.Ordinal);
            }

            public int GetHashCode(AssemblyName obj)
            {
                var hashCode = 0;
                if (obj.Name != null)
                {
                    hashCode ^= obj.Name.GetHashCode();
                }

                hashCode ^= (obj.CultureName ?? string.Empty).GetHashCode();
                return hashCode;
            }
        }
    }
}
