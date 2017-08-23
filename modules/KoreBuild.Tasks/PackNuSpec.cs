// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using KoreBuild.Tasks.Utilties;
using NuGet.Versioning;

namespace KoreBuild.Tasks
{
    public class PackNuSpec : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string NuspecPath { get; set; }

        [Required]
        public string DestinationFolder { get; set; }

        public string BasePath { get; set; }

        public ITaskItem[] Dependencies { get; set; }

        public string Properties { get; set; }

        public bool IncludeEmptyDirectories { get; set; } = false;

        [Output]
        public ITaskItem[] Packages { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(NuspecPath))
            {
                Log.LogError("Nuspec does not exist: " + NuspecPath);
                return false;
            }

            var packageBasePath = string.IsNullOrEmpty(BasePath)
                ? Path.GetDirectoryName(NuspecPath)
                : BasePath;

            if (!Directory.Exists(packageBasePath))
            {
                Log.LogError("Base path does not exist: " + packageBasePath);
                return false;
            }

            var properties = MSBuildListSplitter.GetNamedProperties(Properties);

            string PropertyProvider(string name)
            {
                if (properties.TryGetValue(name, out var value))
                {
                    return value;
                }
                Log.LogError("Undefined property: " + name);
                return null;
            }

            PackageBuilder packageBuilder;
            try
            {
                Log.LogMessage($"Loading nuspec {NuspecPath}");

                packageBuilder = new PackageBuilder(NuspecPath, packageBasePath, PropertyProvider, IncludeEmptyDirectories);
            }
            catch (InvalidDataException ex)
            {
                Log.LogKoreBuildError(NuspecPath, KoreBuildErrors.InvalidNuspecFile, ex.Message);
                return false;
            }

            if (Dependencies != null)
            {
                AddDependencies(packageBuilder);
            }

            Directory.CreateDirectory(DestinationFolder);
            var dest = Path.Combine(DestinationFolder, $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg");
            using (var stream = File.Create(dest))
            {
                packageBuilder.Save(stream);
            }

            Log.LogMessage(MessageImportance.High, $"Created package {dest}");
            Packages = new[] { new TaskItem(dest) };

            return true;
        }

        private void AddDependencies(PackageBuilder builder)
        {
            var packageRequest = Dependencies.Select(d =>
            {
                NuGetFramework tfm = NuGetFramework.AnyFramework;
                if (!string.IsNullOrEmpty(d.GetMetadata("TargetFramework")))
                {
                    tfm = NuGetFramework.Parse(d.GetMetadata("TargetFramework"));
                }

                if (string.IsNullOrEmpty(d.GetMetadata("Version")))
                {
                    Log.LogError($"Dependency {d.ItemSpec} is missing expected metdata: Version");
                }

                return new { tfm, dependency = new PackageDependency(d.ItemSpec, VersionRange.Parse(d.GetMetadata("Version"))) };
            });

            foreach (var group in packageRequest.GroupBy(g => g.tfm))
            {
                var existingPackages = builder.DependencyGroups.FirstOrDefault(g => g.TargetFramework == group.Key)?.Packages
                    ?? Enumerable.Empty<PackageDependency>();

                var depGroup = new PackageDependencyGroup(group.Key, existingPackages.Concat(group.Select(d => d.dependency)));
                builder.DependencyGroups.Add(depGroup);
            }
        }
    }
}
