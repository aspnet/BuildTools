// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETCOREAPP1_0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;

namespace ApiCheck
{
    public class CoreClrAssemblyLoader
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

        private readonly AssemblyLoadContext _loadContext;

        static CoreClrAssemblyLoader()
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

        public CoreClrAssemblyLoader(LibraryExport[] exports, string runtime, string outputPath)
        {
            _assemblyPaths = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);
            _nativeLibraries = new Dictionary<string, string>();
            IEnumerable<RuntimeFallbacks> rids = DependencyContext.Default?.RuntimeGraph ?? Enumerable.Empty<RuntimeFallbacks>();
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

            _loadContext = new ApiCheckLoadContext(FindAssemblyPath);
        }

        public Assembly Load(string assemblyPath)
        {
            return _loadContext.LoadFromAssemblyPath(assemblyPath);
        }

        private AssemblyName GetAssemblyName(string assemblyPath)
        {
            return AssemblyLoadContext.GetAssemblyName(assemblyPath);
        }

        private string FindAssemblyPath(AssemblyName name)
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

        private class ApiCheckLoadContext : AssemblyLoadContext
        {
            private readonly Func<AssemblyName, string> _finder;

            public ApiCheckLoadContext(Func<AssemblyName, string> finder)
            {
                _finder = finder;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                try
                {
                    var assembly = Default.LoadFromAssemblyName(assemblyName);
                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
                catch (FileNotFoundException)
                {
                }

                string path = _finder(assemblyName);
                if (path != null)
                {
                    return LoadFromAssemblyPath(path);
                }

                return null;
            }
        }
    }
}
#endif