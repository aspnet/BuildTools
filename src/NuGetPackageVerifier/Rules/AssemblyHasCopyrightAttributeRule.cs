// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasCopyrightAttributeRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            AssemblyHasAttributeHelper.GetAssemblyAttributesData(context);
            foreach (var assemblyData in context.AssemblyData)
            {
                if (!HasCopyrightAttribute(assemblyData.Value.AssemblyAttributes))
                {
                    yield return PackageIssueFactory.AssemblyMissingCopyrightAttribute(assemblyData.Key);
                }
            }
        }

        private static bool HasCopyrightAttribute(Collection<CustomAttribute> asmAttrs)
        {
            var foundAttr = asmAttrs.SingleOrDefault(attr => attr.AttributeType.FullName == typeof(AssemblyCopyrightAttribute).FullName);
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
