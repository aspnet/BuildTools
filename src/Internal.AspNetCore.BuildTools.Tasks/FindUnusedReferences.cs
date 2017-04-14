// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_FindUnusedReferences : Task
#elif BuildTools
    public class FindUnusedReferences : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        /// <summary>
        /// IntermediateAssembly from CoreCompile
        /// </summary>
        [Required]
        public string Assembly { get; set; }

        /// <summary>
        /// ReferencePath from CoreCompile
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// FileDefinitions from RunResolvePackageDependencies
        /// </summary>
        [Required]
        public ITaskItem[] Packages { get; set; }

        /// <summary>
        /// PackageDependencies from RunResolvePackageDependencies
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] UnusedReferences { get; set; }

        public override bool Execute()
        {
            var references = new HashSet<string>(References.Select(item => item.ItemSpec), StringComparer.OrdinalIgnoreCase);
            var referenceFiles = Files.Where(file => references.Contains(file.GetMetadata("ResolvedPath")))
                .ToDictionary(item => Path.GetFileNameWithoutExtension(item.GetMetadata("ResolvedPath")), StringComparer.OrdinalIgnoreCase);

            var directReferences = new HashSet<string>(
                Packages.Where(p => string.IsNullOrEmpty(p.GetMetadata("ParentPackage"))).Select(i => i.ItemSpec),
                StringComparer.OrdinalIgnoreCase);

            using (var fileStream = File.OpenRead(Assembly))
            using (PEReader reader = new PEReader(fileStream))
            {
                var metadataReader = reader.GetMetadataReader();
                foreach (AssemblyReferenceHandle assemblyReferenceHandle in metadataReader.AssemblyReferences)
                {
                    var assemblyReference = metadataReader.GetAssemblyReference(assemblyReferenceHandle);
                    var name = metadataReader.GetString(assemblyReference.Name);
                    if (referenceFiles.TryGetValue(name, out var fileItem))
                    {
                        var packageName = fileItem.GetMetadata("PackageName") + "/" + fileItem.GetMetadata("PackageVersion");
                        directReferences.Remove(packageName);
                        referenceFiles.Remove(name);
                    }

                }
            }

            UnusedReferences = referenceFiles.Values.Where(f => directReferences.Any(r => f.ItemSpec.StartsWith(r))).ToArray();
            return true;
        }
    }
}
