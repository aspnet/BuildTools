// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Generates a nupkg from a nuspec file
    /// </summary>
    public class PackNuSpec : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The path the nuspec file.
        /// </summary>
        [Required]
        public string NuspecPath { get; set; }

        /// <summary>
        /// Output nupkg is placed in folder + '$(id).$(version).nupkg'.
        /// Either this or <see cref="OutputPath" /> must be specified.
        /// </summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// The output path for the nupkg.
        /// Either this or <see cref="DestinationFolder" /> must be specified.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// The base path to use for any relative paths in the &lt;files%gt; section of nuspec.
        /// Defaults to the nuspec folder.
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// Dependencies to add to the metadata>dependencies section of the spec.
        /// Metadata 'TargetFramework' can be specified to further put dependencies into >group[targetFramework]
        /// </summary>
        public ITaskItem[] Dependencies { get; set; }

        /// <summary>
        /// Files to add to the package. Must specify the PackagePath metadata.
        /// </summary>
        public ITaskItem[] PackageFiles { get; set; }

        /// <summary>
        /// Subsitution in the nuspec via $key$.
        /// </summary>
        public string[] Properties { get; set; }

        /// <summary>
        /// Pack empty directories.
        /// </summary>
        public bool IncludeEmptyDirectories { get; set; } = false;

        /// <summary>
        /// Overwrite the destination file if it exists.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// The nupkg files created
        /// </summary>
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

            if (!(string.IsNullOrEmpty(DestinationFolder) ^ string.IsNullOrEmpty(OutputPath)))
            {
                Log.LogError("Either DestinationFolder and OutputPath must be specified, but only not both.");
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

                using (var file = File.OpenRead(NuspecPath))
                {
                    var manifest = Manifest.ReadFrom(file, PropertyProvider, validateSchema: false);
                    if (!manifest.HasFilesNode)
                    {
                        // Warn about this overly permissive default in nuspec.
                        Log.LogKoreBuildWarning(KoreBuildErrors.NuspecMissingFilesNode,
                            "The nuspec file is missing the <files> nodes. This causes all files in NuspecBase to be included in the package. " +
                            @"Add an empty `<files />` node to prevent this behavior. Add `<files> <file src=""**\*\"" target=""\"" /> </files>` to the nuspec to suppress this warning.");
                    }
                }

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

            if (PackageFiles != null)
            {
                AddFiles(packageBuilder);
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            var dest = !string.IsNullOrEmpty(OutputPath)
                ? OutputPath
                : Path.Combine(DestinationFolder, $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg");

            // normalize path
            dest = Path.GetFullPath(dest);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (!Overwrite && File.Exists(dest))
            {
                Log.LogError($"File path '{dest}' already exists. Set Overwrite=true to overwrite the destination nupkg file.");
                return false;
            }

            if (packageBuilder.Files != null)
            {
                foreach (var file in packageBuilder.Files)
                {
                    if (file is PhysicalPackageFile p)
                    {
                        Log.LogMessage($"Packing {p.SourcePath} => {p.Path}");
                    }
                    else
                    {
                        Log.LogMessage($"Packing {file.Path}");
                    }
                }
            }

            using (var stream = File.Create(dest))
            {
                packageBuilder.Save(stream);
            }

            Log.LogMessage(MessageImportance.High, $"Created package {dest}");
            Packages = new[] { new TaskItem(dest) };

            return true;
        }

        private void AddFiles(PackageBuilder builder)
        {
            foreach (var file in PackageFiles)
            {
                var packagePath = file.GetMetadata("PackagePath");
                var fileName = Path.GetFileName(packagePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    Log.LogKoreBuildError(KoreBuildErrors.InvalidPackagePathMetadata,
                        "The PackagePath metadata value on {0} is invalid. PackagePath must be set to the exact file path within the nuget package.");
                    continue;
                }

                builder.Files.Add(new PhysicalPackageFile
                {
                    SourcePath = file.ItemSpec,
                    TargetPath = packagePath,
                });
            }
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

                return new
                {
                    tfm,
                    dependency = new PackageDependency(d.ItemSpec,
                        VersionRange.Parse(d.GetMetadata("Version")),
                        d.GetMetadata("IncludeAssets").Split(';').Select(s => s.Trim()).ToArray(),
                        d.GetMetadata("ExcludeAssets").Split(';').Select(s => s.Trim()).ToArray())
                };
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
