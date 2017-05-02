// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using Mono.Cecil;
using Mono.Collections.Generic;
using NuGet.Packaging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasNeutralResourcesLanguageAttributeRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            AssemblyDefinition assembly,
            Collection<CustomAttribute> assemblyAttributes)
        {
            if (!HasNeutralResourcesLanguageAttribute(assemblyAttributes))
            {
                yield return PackageIssueFactory.AssemblyMissingNeutralResourcesLanguageAttribute(currentFilePath);
            }
        }

        private static bool HasNeutralResourcesLanguageAttribute(Collection<CustomAttribute> asmAttrs)
        {
            return asmAttrs.Any(asmAttr => IsValidNeutralResourcesLanguageAttribute(asmAttr));
        }

        private static bool IsValidNeutralResourcesLanguageAttribute(CustomAttribute asmAttr)
        {
            if (asmAttr.AttributeType.FullName != typeof(NeutralResourcesLanguageAttribute).FullName)
            {
                return false;
            }
            if (asmAttr.ConstructorArguments.Count != 1)
            {
                return false;
            }

            var value = asmAttr.ConstructorArguments[0].Value as string;

            return string.Equals(value, "en-us", StringComparison.OrdinalIgnoreCase);
        }

        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(IPackageMetadata packageMetadata, string currentFilePath, AssemblyDefinition assembly, Collection<CustomAttribute> assemblyAttributes)
        {
            return null;
        }
    }
}
