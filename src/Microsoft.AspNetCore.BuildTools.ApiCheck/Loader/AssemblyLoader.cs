// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;
#if NETCOREAPP1_1
using NuGet.Frameworks;
using NuGet.ProjectModel;
#endif
using NugetReferenceResolver;
using System.IO;

namespace ApiCheck
{
    public abstract class AssemblyLoader
    {
        public static Assembly LoadAssembly(
                string assemblyPath,
                string assetsJson,
                string framework)
        {
            
#if NETCOREAPP1_1
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
