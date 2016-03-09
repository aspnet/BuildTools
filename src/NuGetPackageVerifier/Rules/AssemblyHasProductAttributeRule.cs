// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasProductAttributeRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            string currentFilePath,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes)
        {
            if (!HasProductAttribute(assemblyAttributes))
            {
                yield return PackageIssueFactory.AssemblyMissingProductAttribute(currentFilePath);
            }
        }

        private static bool HasProductAttribute(Mono.Collections.Generic.Collection<CustomAttribute> asmAttrs)
        {
            var foundAttr = asmAttrs.SingleOrDefault(attr => attr.AttributeType.FullName == typeof(AssemblyProductAttribute).FullName);
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
