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
                    if (TryGetAssemblyName(assembly.FullName, out var name))
                    {
                        _assemblyPaths.Add(name, assembly.FullName);
                    }
                }
            }

            foreach (var path in graph.GetAssembliesFullPath())
            {
                if (TryGetAssemblyName(path, out var name) && !_assemblyPaths.ContainsKey(name))
                {
                    _assemblyPaths.Add(name, path);
                }
            }

            _loadContext = new ApiCheckLoadContext(FindAssemblyPath);
        }

        public Assembly Load(string assemblyPath)
        {
            return _loadContext.LoadFromAssemblyPath(assemblyPath);
        }

        private bool TryGetAssemblyName(string path, out AssemblyName assemblyName)
        {
            assemblyName = null;
            if (!File.Exists(path))
            {
                // Path might be bin\placeholder\** if assembly came from a project-to-project reference. Since those
                // assemblies are found in the current output directory, just ignore non-existent paths. If this path
                // came from somewhere else and assembly is used, loading will fail soon enough.
                return false;
            }

            // From http://msdn.microsoft.com/en-us/library/ms173100.aspx and AssemblyHelper.IsAssemblyManaged().
            try
            {
                assemblyName = AssemblyLoadContext.GetAssemblyName(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                // The file cannot be found (should be redundant).
            }
            catch (BadImageFormatException)
            {
                // The file is not an assembly.
            }
            catch (FileLoadException)
            {
                // The assembly has already been loaded.
            }

            return false;
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