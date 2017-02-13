// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasVersionAttributesRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            AssemblyDefinition assembly,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes)
        {
            if (!HasAttrWithArg(assemblyAttributes, typeof(AssemblyFileVersionAttribute).FullName))
            {
                yield return PackageIssueFactory.AssemblyMissingFileVersionAttribute(currentFilePath);
            }

            if (!HasAttrWithArg(assemblyAttributes, typeof(AssemblyInformationalVersionAttribute).FullName))
            {
                yield return PackageIssueFactory.AssemblyMissingInformationalVersionAttribute(currentFilePath);
            }
        }

        private static bool HasAttrWithArg(Mono.Collections.Generic.Collection<CustomAttribute> asmAttrs, string attrTypeName)
        {
            var foundAttr = asmAttrs.SingleOrDefault(attr => attr.AttributeType.FullName == attrTypeName);
            if (foundAttr == null)
            {
                return false;
            }
            var foundAttrArg = foundAttr.ConstructorArguments.SingleOrDefault();
            var attrValue = foundAttrArg.Value as string;

            return !string.IsNullOrEmpty(attrValue);
        }
    }
}
