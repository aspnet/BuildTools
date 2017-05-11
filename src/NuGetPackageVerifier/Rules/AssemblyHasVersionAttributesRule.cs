// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasVersionAttributesRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            AssemblyAttributesDataHelper.SetAssemblyAttributesData(context);
            foreach (var assemblyData in context.AssemblyData)
            {
                if (!HasAttrWithArg(assemblyData.Value.AssemblyAttributes, typeof(AssemblyFileVersionAttribute).FullName))
                {
                    yield return PackageIssueFactory.AssemblyMissingFileVersionAttribute(assemblyData.Key);
                }

                if (!HasAttrWithArg(assemblyData.Value.AssemblyAttributes, typeof(AssemblyInformationalVersionAttribute).FullName))
                {
                    yield return PackageIssueFactory.AssemblyMissingInformationalVersionAttribute(assemblyData.Key);
                }
            }
        }

        private static bool HasAttrWithArg(Collection<CustomAttribute> asmAttrs, string attrTypeName)
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
