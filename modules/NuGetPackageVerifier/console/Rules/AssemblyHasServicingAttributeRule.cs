// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasServicingAttributeRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            AssemblyAttributesDataHelper.SetAssemblyAttributesData(context);
            foreach (var assemblyData in context.AssemblyData)
            {
                if (!HasServicingAttribute(assemblyData.Value.AssemblyAttributes))
                {
                    yield return PackageIssueFactory.AssemblyMissingServicingAttribute(assemblyData.Key);
                }
            }
        }

        private static bool HasServicingAttribute(Collection<CustomAttribute> asmAttrs)
        {
            return asmAttrs.Any(asmAttr => IsValidServicingAttribute(asmAttr));
        }

        private static bool IsValidServicingAttribute(CustomAttribute asmAttr)
        {
            if (asmAttr.AttributeType.FullName != typeof(AssemblyMetadataAttribute).FullName)
            {
                return false;
            }
            if (asmAttr.ConstructorArguments.Count != 2)
            {
                return false;
            }

            var keyValue = asmAttr.ConstructorArguments[0].Value as string;
            var valueValue = asmAttr.ConstructorArguments[1].Value as string;

            return (keyValue == "Serviceable") && (valueValue == "True");
        }
    }
}
