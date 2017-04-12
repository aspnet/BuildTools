// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasCommitHashAttributeRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            AssemblyDefinition assembly,
            Collection<CustomAttribute> assemblyAttributes)
        {
            var fileName = Path.GetFileNameWithoutExtension(currentFilePath);
            var isSourcesPackage = fileName.EndsWith(".Sources", StringComparison.OrdinalIgnoreCase);
            if (!isSourcesPackage && !HasCommitHashInMetadataAttribute(assemblyAttributes))
            {
                yield return PackageIssueFactory.AssemblyMissingHashAttribute(currentFilePath);
            }
        }

        private static bool HasCommitHashInMetadataAttribute(Collection<CustomAttribute> assemblyAttributes)
        {
            var hashAttribute = assemblyAttributes
                .SingleOrDefault(a =>
                    a.AttributeType.FullName == typeof(AssemblyMetadataAttribute).FullName
                    && a.ConstructorArguments.Count == 2
                    && a.ConstructorArguments[0].Value as string == "CommitHash"
                    && !string.IsNullOrEmpty(a.ConstructorArguments[1].Value as string));

            return hashAttribute != null;
        }
    }
}
