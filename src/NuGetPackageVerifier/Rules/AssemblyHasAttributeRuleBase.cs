// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NuGet.Packaging;

namespace NuGetPackageVerifier.Rules
{
    public abstract class AssemblyHasAttributeRuleBase : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            using (var reader = new PackageArchiveReader(context.PackageFileInfo.FullName))
            {
                foreach (var currentFile in reader.GetFiles())
                {
                    var extension = Path.GetExtension(currentFile);
                    if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var assemblyPath = Path.ChangeExtension(
                            Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

                        try
                        {
                            using (var packageFileStream = reader.GetStream(currentFile))
                            using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                            {
                                packageFileStream.CopyTo(fileStream);
                            }

                            if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                            {
                                using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
                                {

                                    var asmAttrs = assembly.CustomAttributes;

                                    return ValidateAttribute(currentFile, asmAttrs);
                                }
                            }
                        }
                        finally
                        {
                            if (File.Exists(assemblyPath))
                            {
                                File.Delete(assemblyPath);
                            }
                        }
                    }
                }
            }

            return Enumerable.Empty<PackageVerifierIssue>();
        }

        public abstract IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes);
    }
}
