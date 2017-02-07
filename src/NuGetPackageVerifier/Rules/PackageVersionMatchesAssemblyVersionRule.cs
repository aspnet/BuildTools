// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public class PackageVersionMatchesAssemblyVersionRule : AssemblyHasAttributeRuleBase
    {
        private Version _packageVersion;
        private string _packageId;

        public override IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            _packageVersion = context.Metadata.Version.Version;
            _packageId = context.Metadata.Id;
            return base.Validate(context);
        }

        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(string currentFilePath,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes)
        {
            var assemblyVersion = assemblyAttributes.SingleOrDefault(a =>
            a.AttributeType.FullName.Equals(
                (typeof(AssemblyInformationalVersionAttribute).FullName
                ?? typeof(AssemblyVersionAttribute).FullName), StringComparison.Ordinal))
                .AttributeType.Module.Assembly.Name.Version;

            if (assemblyVersion == null)
            {
                yield return null;
            }

            if (!_packageVersion.Equals(assemblyVersion))
            {
                yield return PackageIssueFactory.AssemblyVersionDoesNotMatchPackageVersion(currentFilePath, assemblyVersion, _packageVersion, _packageId);
            }
        }
    }
}
