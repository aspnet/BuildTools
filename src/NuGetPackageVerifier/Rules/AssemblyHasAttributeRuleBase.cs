// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public abstract class AssemblyHasAttributeRuleBase : IPackageVerifierRule
    {
        public virtual IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var currentFile in context.PackageReader.GetFiles())
            {
                var extension = Path.GetExtension(currentFile);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var assemblyPath = Path.ChangeExtension(
                        Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

                    try
                    {
                        using (var packageFileStream = context.PackageReader.GetStream(currentFile))
                        using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                        {
                            packageFileStream.CopyTo(fileStream);
                        }

                        if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                        {
                            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
                            {
                                var asmAttrs = assembly.CustomAttributes;

                                return ValidateAttribute(currentFile, assembly, asmAttrs);
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

            return Enumerable.Empty<PackageVerifierIssue>();
        }

        public abstract IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            AssemblyDefinition assembly,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes);
    }
}
