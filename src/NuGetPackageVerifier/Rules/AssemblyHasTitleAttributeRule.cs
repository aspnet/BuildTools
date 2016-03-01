// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NuGet;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasTitleAttributeRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            IPackageFile currentFile,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes)
        {
            if (!HasTitleAttribute(assemblyAttributes))
            {
                yield return PackageIssueFactory.AssemblyMissingTitleAttribute(currentFile.Path);
            }
        }

        private static bool HasTitleAttribute(Mono.Collections.Generic.Collection<CustomAttribute> asmAttrs)
        {
            var foundAttr = asmAttrs.SingleOrDefault(attr => attr.AttributeType.FullName == typeof(AssemblyTitleAttribute).FullName);
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
