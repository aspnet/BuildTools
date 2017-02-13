// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETCOREAPP1_1

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using NuGet.Frameworks;
using NugetReferenceResolver;

namespace ApiCheck
{
    public class CoreClrAssemblyLoader
    {
        private readonly IDictionary<AssemblyName, string> _assemblyPaths;
        private readonly PackageGraph _graph;
        private readonly ApiCheckLoadContext _loadContext;

        public CoreClrAssemblyLoader(PackageGraph graph, string assemblyPath)
        {
            _graph = graph;
            _assemblyPaths = new Dictionary<AssemblyName, string>(new AssemblyNameComparer());
            var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));
            if (directory.Exists)
            {
                foreach (var assembly in directory.EnumerateFiles("*.dll"))
                {
                    var name = AssemblyLoadContext.GetAssemblyName(assembly.FullName);
                    _assemblyPaths.Add(name, assembly.FullName);
                }
            }
            foreach (var path in graph.GetAssembliesFullPath())
            {
                var name = AssemblyLoadContext.GetAssemblyName(path);
                if (!_assemblyPaths.ContainsKey(name))
                {
                    _assemblyPaths.Add(name, path);
                }
            }

            _loadContext = new ApiCheckLoadContext(FindAssemblyPath);
        }
        private static bool IsCompatible(NuGetFramework reference, NuGetFramework target)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(reference, target);
        }

        public Assembly Load(string assemblyPath)
        {
            return _loadContext.LoadFromAssemblyPath(assemblyPath);
        }

        private string FindAssemblyPath(AssemblyName name)
        {
            if (_assemblyPaths.TryGetValue(name, out var path))
            {
                return path;
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