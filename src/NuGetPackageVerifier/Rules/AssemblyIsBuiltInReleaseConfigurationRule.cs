﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Collections.Generic;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyIsBuiltInReleaseConfigurationRule : AssemblyHasAttributeRuleBase
    {
        // TODO remove
        public override IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            // todo remove "Cecil throws ArgumentException when evaluating constructor arguments"
            context.Logger.Log(LogLevel.Info, $"{nameof(AssemblyIsBuiltInReleaseConfigurationRule)} skipped");
            return Enumerable.Empty<PackageVerifierIssue>();
        }

        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(string currentFilePath, AssemblyDefinition assembly, Collection<CustomAttribute> assemblyAttributes)
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
            return !attrValue.HasFlag(DebuggableAttribute.DebuggingModes.Default) &&
                   !attrValue.HasFlag(DebuggableAttribute.DebuggingModes.DisableOptimizations);
        }
    }
}
