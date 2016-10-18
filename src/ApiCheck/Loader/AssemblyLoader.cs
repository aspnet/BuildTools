using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

namespace ApiCheck
{
    public abstract class AssemblyLoader
    {
        protected readonly LibraryExport[] _libraries;

        public AssemblyLoader(LibraryExport[] exports)
        {
            _libraries = exports;
        }

        public static Assembly LoadAssembly(
                string assemblyPath,
                string projectDirectory,
                string lockFile,
                NuGetFramework framework,
                string configuration,
                string packagesFolder)
        {
            var ctx = CreateProjectContext(framework, projectDirectory, lockFile, packagesFolder);
            var exporter = ctx.CreateExporter(configuration);
            var dependencies = exporter.GetDependencies().ToArray();

#if NETCOREAPP1_0
            var loader = new CoreClrAssemblyLoader(dependencies);
#else
            var loader = new FullFrameworkAssemblyLoader(dependencies);
#endif            
            return loader.Load(assemblyPath);
        }

        public abstract Assembly Load(string assemblyPath);
        protected abstract AssemblyName GetAssemblyName(string assemblyPath);

        protected string FindAssemblyPath(AssemblyName name)
        {
            foreach (var library in _libraries)
            {
                var runtimeGroup = library.RuntimeAssemblyGroups.FirstOrDefault(rg => rg.Runtime == "");
                if (runtimeGroup == null)
                {
                    continue;
                }

                var libraryPath = runtimeGroup.Assets.FirstOrDefault(l => l.FileName.EndsWith($"{name.Name}.dll")).ResolvedPath;
                if (libraryPath == null)
                {
                    continue;
                }

                var resolvedName = GetAssemblyName(libraryPath);
                if (name.FullName == resolvedName.FullName)
                {
                    return libraryPath;
                }
            }

            return null;
        }

        private static ProjectContext CreateProjectContext(NuGetFramework framework, string projectDirectory, string lockFilePath, string packagesFolder)
        {
            var lockFile = LockFileReader.Read(lockFilePath, designTime: false);
            var compatibleTarget = lockFile.Targets.Single(t => t.TargetFramework.Framework == framework.Framework);
            var builder = new ProjectContextBuilder()
                .WithProjectDirectory(projectDirectory)
                .WithPackagesDirectory(packagesFolder)
                .WithLockFile(lockFile)
                .WithTargetFramework(compatibleTarget.TargetFramework);

            return builder.Build();
        }

        private static bool IsCompatible(NuGetFramework reference, NuGetFramework target)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(reference, target);
        }
    }
}
