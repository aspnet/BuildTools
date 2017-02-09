// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NuGet.Versioning;

namespace NuGetPackageVerifier.Rules
{
    public class PackageVersionMatchesAssemblyVersionRule : AssemblyHasAttributeRuleBase
    {
        private bool _isPrerelease;
        private NuGetVersion _packageVersion;
        private string _packageId;

        public override IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            _isPrerelease = context.Metadata.Version.IsPrerelease;
            _packageVersion = context.Metadata.Version;
            _packageId = context.Metadata.Id;
            return base.Validate(context);
        }

        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(string currentFilePath,
            Mono.Collections.Generic.Collection<CustomAttribute> assemblyAttributes)
        {

            var versionAttribute = assemblyAttributes.SingleOrDefault(a =>
                a.AttributeType.FullName.Equals(
                    typeof(AssemblyInformationalVersionAttribute).FullName,
                    StringComparison.Ordinal));

            if (versionAttribute == null)
            {
                yield break;
            }

            if (_isPrerelease)
            {
                var assemblyVersion = versionAttribute.ConstructorArguments[0].Value.ToString();
                if (!_packageVersion.ToString().Equals(assemblyVersion))
                {
                    yield return PackageIssueFactory.AssemblyVersionDoesNotMatchPackageVersion(currentFilePath, assemblyVersion, _packageVersion, _packageId);
                }
            }

            else
            {
                var assemblyVersion = versionAttribute.AttributeType.Module.Assembly.Name.Version.ToString();
                if (!_packageVersion.Version.ToString().Equals(assemblyVersion))
                {
                    yield return PackageIssueFactory.AssemblyVersionDoesNotMatchPackageVersion(currentFilePath, assemblyVersion, _packageVersion, _packageId);
                }
            }
        }
    }
}
