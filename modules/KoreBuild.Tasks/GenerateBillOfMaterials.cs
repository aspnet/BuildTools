// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Packaging;

namespace KoreBuild.Tasks
{
    public class GenerateBillOfMaterials : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] Artifacts { get; set; }

        [Required]
        [Output]
        public string OutputPath { get; set; }

        public string[] AdditionalMetadataFilters { get; set; }

        public override bool Execute()
        {
            OutputPath = OutputPath.Replace('\\', '/');

            return Execute(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                return File.CreateText(OutputPath);
            });
        }

        internal bool Execute(Func<TextWriter> writerFactory)
        {
            var bom = new BillOfMaterials();

            // metadata items that shouldn't end up in the bom
            var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // common metadata set by MSBuild tasks
                "MSBuildSourceProjectFile",
                "MSBuildSourceTargetName",
                "OriginalItemSpec",
                "RepositoryRoot",
                // reserved metadata
                "Id",
                "Type",
                "ArtifactType",
                "Category",
                "Dependencies",
            };

            if (AdditionalMetadataFilters != null)
            {
                filter.AddRange(AdditionalMetadataFilters);
            }

            foreach (var item in Artifacts)
            {
                var id = Path.GetFileName(item.ItemSpec);
                var type = item.GetMetadata("ArtifactType");
                var dependencies = item.GetMetadata("Dependencies");

                if (string.IsNullOrEmpty(type))
                {
                    Log.LogKoreBuildError(KoreBuildErrors.MissingArtifactType, "Missing required metadata 'ArtifactType' for artifact {0}", item.ItemSpec);
                    continue;
                }

                var artifact = bom.AddArtifact(id, type);
                artifact.Category = !string.IsNullOrEmpty(item.GetMetadata("Category"))
                    ? item.GetMetadata("Category")
                    : null;

                foreach (var obj in item.CloneCustomMetadata().Keys)
                {
                    var key = obj as string;
                    if (filter.Contains(key))
                    {
                        continue;
                    }

                    var value = item.GetMetadata(key) as string;

                    if (!string.IsNullOrEmpty(value))
                    {
                        artifact.SetMetadata(key, value);
                    }
                }

                if (!string.IsNullOrEmpty(dependencies))
                {
                    foreach (var dep in dependencies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        bom.Dependencies.AddLink(id, dep.Trim());
                    }
                }

                if (type.Equals("NuGetPackage", StringComparison.OrdinalIgnoreCase))
                {
                    using (var reader = new PackageArchiveReader(item.ItemSpec))
                    {
                        var nuspec = new NuspecReader(reader.GetNuspec());
                        foreach (var dep in nuspec.GetDependencyGroups().SelectMany(g => g.Packages).Distinct())
                        {
                            var version = dep.VersionRange.MinVersion;
                            if (version == null)
                            {
                                Log.LogWarning($"Skipping adding automatic asset dependency from {nuspec.GetIdentity()} to {dep.Id} because the dependency version range '{dep.VersionRange}' does not have a lower bound.");
                                continue;
                            }

                            var target = $"{dep.Id}.{version}.nupkg";
                            bom.Dependencies.AddLink(id, target);
                        }
                    }
                }
            }

            using (var stream = writerFactory())
            using (var writer = new BillOfMaterialsXmlWriter(stream))
            {
                writer.Write(bom);
            }

            Log.LogMessage($"Generated bill of materials in {OutputPath}");

            return !Log.HasLoggedErrors;
        }
    }
}
