// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETCOREAPP1_0

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Loader;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace ApiCheck
{
    public class CoreClrAssemblyLoader : AssemblyLoader
    {
        private AssemblyLoadContext _loadContext;

        public CoreClrAssemblyLoader(LibraryExport[] exports, string runtime, string outputPath)
            : base(exports, runtime, outputPath)
        {
            _loadContext = new ApiCheckLoadContext(FindAssemblyPath);
        }

        public override Assembly Load(string assemblyPath)
        {
            return _loadContext.LoadFromAssemblyPath(assemblyPath);
        }

        protected override AssemblyName GetAssemblyName(string assemblyPath)
        {
            return AssemblyLoadContext.GetAssemblyName(assemblyPath);
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