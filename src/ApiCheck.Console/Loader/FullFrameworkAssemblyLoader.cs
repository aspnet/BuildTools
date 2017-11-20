// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if !NETCOREAPP2_1

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ApiCheck
{
    public class FullFrameworkAssemblyLoader
    {
        private readonly Dictionary<AssemblyName, string> _resolvedDlls;

        public FullFrameworkAssemblyLoader(string probingPath)
        {
            var directory = new DirectoryInfo(probingPath);
            _resolvedDlls = directory.EnumerateFiles("*.dll")
                .ToDictionary(f => GetAssemblyName(f.FullName), f => f.FullName, new AssemblyNameComparer());

            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        private AssemblyName GetAssemblyName(string assemblyPath) => AssemblyName.GetAssemblyName(assemblyPath);

        private Assembly Resolver(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            var path = FindAssemblyPath(name);

            return path != null ? Assembly.LoadFile(path) : null;
        }

        private string FindAssemblyPath(AssemblyName name) => _resolvedDlls.TryGetValue(name, out var path) ? path : null;

        public Assembly Load(string assemblyPath) => Assembly.LoadFile(assemblyPath);
    }
}

#endif