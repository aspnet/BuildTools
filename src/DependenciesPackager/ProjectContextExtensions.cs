using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;

namespace DependenciesPackager
{
    internal static class ProjectContextExtensions
    {
        public static IEnumerable<PackageEntry> GetPackageEntries(this ProjectContext context, string runtime, string restoreFolder)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (restoreFolder == null)
            {
                throw new ArgumentNullException(nameof(restoreFolder));
            }

            var dependencies = context.CreateExporter("CACHE").GetDependencies();

            var result = new List<PackageEntry>();
            foreach (var dependency in dependencies)
            {
                if (dependency.Library?.Path == null)
                {
                    continue;
                }

                var assemblyGroup =
                    dependency.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == runtime) ??
                    dependency.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == string.Empty);

                var assets = assemblyGroup?.Assets;

                if (assets?.Any() != true)
                {
                    continue;
                }

                result.Add(new PackageEntry
                {
                    Library = dependency.Library,
                    Assets = assets
                });
            }

            return result;
        }

        public static string GetStagingFolderPath(this ProjectContext context, string publishFolder) =>
            Path.GetFullPath(Path.Combine(publishFolder, context.TargetFramework.GetShortFolderName(), context.RuntimeIdentifier));
    }
}
