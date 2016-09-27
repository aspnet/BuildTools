// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyIsBuiltInReleaseConfiguraitonRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(string currentFilePath, Collection<CustomAttribute> assemblyAttributes)
        {
            if (!HasReleaseConfiguration(assemblyAttributes))
            {
                yield return PackageIssueFactory.AssemblyHasIncorrectBuildConfiguration(currentFilePath);
            }
        }

        private static bool HasReleaseConfiguration(Collection<CustomAttribute> assemblyAttributes)
        {
            var foundAttr = assemblyAttributes.SingleOrDefault(attr => attr.AttributeType.FullName == typeof(DebuggableAttribute).FullName);
            if (foundAttr == null)
            {
                return false;
            }

            var foundAttrArg = foundAttr.ConstructorArguments.SingleOrDefault();
            var attrValue = (DebuggableAttribute.DebuggingModes)foundAttrArg.Value;
            if (attrValue.HasFlag(DebuggableAttribute.DebuggingModes.Default) ||
                attrValue.HasFlag(DebuggableAttribute.DebuggingModes.DisableOptimizations))
            {
                return false;
            }

            return true;
        }
    }
}
