// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System.IO;
#endif
using System.Reflection;
#if NETCOREAPP2_0
using NuGet.ProjectModel;
using NugetReferenceResolver;
#endif

namespace ApiCheck
{
    public abstract class AssemblyLoader
    {
        public static Assembly LoadAssembly(
                string assemblyPath,
                string assetsJson,
                string framework)
        {
#if NETCOREAPP2_0
            var lockFile = new LockFileFormat().Read(assetsJson);
            var graph = PackageGraph.Create(lockFile, framework);
            var loader = new CoreClrAssemblyLoader(graph, assemblyPath);
#else
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var loader = new FullFrameworkAssemblyLoader(assemblyDirectory);
#endif

            return loader.Load(assemblyPath);
        }
    }
}
