#if !NETCOREAPP1_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace ApiCheck
{
    public class FullFrameworkAssemblyLoader : AssemblyLoader
    {
        public FullFrameworkAssemblyLoader(LibraryExport[] exports)
            : base(exports)
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        protected override AssemblyName GetAssemblyName(string assemblyPath)
        {
            return AssemblyName.GetAssemblyName(assemblyPath);
        }

        private Assembly Resolver(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            var path = FindAssemblyPath(name);

            if (path != null)
            {
                return Assembly.LoadFile(path);
            }

            return null;
        }

        public override Assembly Load(string assemblyPath)
        {
            return Assembly.LoadFile(assemblyPath);
        }
    }
}

#endif