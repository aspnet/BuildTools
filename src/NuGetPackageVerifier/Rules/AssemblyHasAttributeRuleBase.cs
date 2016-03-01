// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public abstract class AssemblyHasAttributeRuleBase : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            foreach (var currentFile in package.GetFiles())
            {
                var extension = Path.GetExtension(currentFile.Path);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var assemblyPath = Path.ChangeExtension(
                        Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

                    try
                    {
                        using (var packageFileStream = currentFile.GetStream())
                        using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                        {
                            packageFileStream.CopyTo(fileStream);
                        }

                        if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                        {
                            var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);

                            var asmAttrs = assemblyDefinition.CustomAttributes;

                            return ValidateAttribute(currentFile, asmAttrs);
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
            IPackageFile currentFile,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes);
    }
}
