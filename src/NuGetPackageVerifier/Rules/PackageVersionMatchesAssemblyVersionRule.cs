﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGetPackageVerifier.Rules
{
    public class PackageVersionMatchesAssemblyVersionRule : AssemblyHasAttributeRuleBase
    {
        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(
            IPackageMetadata packageMetadata,
            string currentFilePath,
            AssemblyDefinition assembly,
            Collection<CustomAttribute> assemblyAttributes)
        {
            var assemblyInformationalVersionAttribute = assemblyAttributes.SingleOrDefault(a =>
                a.AttributeType.FullName.Equals(
                    typeof(AssemblyInformationalVersionAttribute).FullName,
                    StringComparison.Ordinal));

            var assemblyInformationalNuGetVersion = new NuGetVersion(assemblyInformationalVersionAttribute.ConstructorArguments[0].Value.ToString());
            if (!VersionEquals(packageMetadata.Version, assemblyInformationalNuGetVersion))
            {
                yield return PackageIssueFactory.AssemblyInformationalVersionDoesNotMatchPackageVersion(
                    currentFilePath,
                    assemblyInformationalNuGetVersion,
                    packageMetadata.Version,
                    packageMetadata.Id);
            }

            var assemblyFileVersionAttribute = assemblyAttributes.SingleOrDefault(a =>
                a.AttributeType.FullName.Equals(
                    typeof(AssemblyFileVersionAttribute).FullName,
                    StringComparison.Ordinal));

            var assemblyFileNuGetVersion = new NuGetVersion(assemblyFileVersionAttribute.ConstructorArguments[0].Value.ToString());
            if (!VersionEquals(packageMetadata.Version, assemblyFileNuGetVersion))
            {
                yield return PackageIssueFactory.AssemblyFileVersionDoesNotMatchPackageVersion(
                    currentFilePath,
                    assemblyFileNuGetVersion,
                    packageMetadata.Version,
                    packageMetadata.Id);
            }

            var assemblyVersion = assembly.Name.Version;
            if (!packageMetadata.Version.Version.Equals(assemblyVersion))
            {
                yield return PackageIssueFactory.AssemblyVersionDoesNotMatchPackageVersion(
                    currentFilePath,
                    assemblyVersion,
                    packageMetadata.Version.Version,
                    packageMetadata.Id);
            }
        }

        public override IEnumerable<PackageVerifierIssue> ValidateAttribute(string currentFilePath, AssemblyDefinition assembly, Collection<CustomAttribute> assemblyAttributes)
        {
            return null;
        }

        private bool VersionEquals(NuGetVersion packageVersion, NuGetVersion assemblyNuGetVersion)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            if (assemblyNuGetVersion == null)
            {
                throw new ArgumentNullException(nameof(assemblyNuGetVersion));
            }

            // Pre-release and build metadata does not need to match
            if (packageVersion.Major == assemblyNuGetVersion.Major &&
                packageVersion.Minor == assemblyNuGetVersion.Minor &&
                packageVersion.Patch == assemblyNuGetVersion.Patch)
            {
                return true;
            }

            return false;
        }
    }
}
