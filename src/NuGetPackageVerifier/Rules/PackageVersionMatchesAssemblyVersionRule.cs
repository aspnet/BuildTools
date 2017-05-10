// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NuGet.Versioning;

namespace NuGetPackageVerifier.Rules
{
    public class PackageVersionMatchesAssemblyVersionRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            AssemblyHasAttributeHelper.GetAssemblyAttributesData(context);
            foreach (var assemblyData in context.AssemblyData)
            {
                var assemblyInformationalVersionAttribute = assemblyData.Value.AssemblyAttributes.SingleOrDefault(a =>
                a.AttributeType.FullName.Equals(
                    typeof(AssemblyInformationalVersionAttribute).FullName,
                    StringComparison.Ordinal));

                var assemblyInformationalNuGetVersion = new NuGetVersion(assemblyInformationalVersionAttribute.ConstructorArguments[0].Value.ToString());
                if (!VersionEquals(context.Metadata.Version, assemblyInformationalNuGetVersion))
                {
                    yield return PackageIssueFactory.AssemblyInformationalVersionDoesNotMatchPackageVersion(
                        assemblyData.Key,
                        assemblyInformationalNuGetVersion,
                        context.Metadata.Version,
                        context.Metadata.Id);
                }

                var assemblyFileVersionAttribute = assemblyData.Value.AssemblyAttributes.SingleOrDefault(a =>
                    a.AttributeType.FullName.Equals(
                        typeof(AssemblyFileVersionAttribute).FullName,
                        StringComparison.Ordinal));

                var assemblyFileNuGetVersion = new NuGetVersion(assemblyFileVersionAttribute.ConstructorArguments[0].Value.ToString());
                if (!VersionEquals(context.Metadata.Version, assemblyFileNuGetVersion))
                {
                    yield return PackageIssueFactory.AssemblyFileVersionDoesNotMatchPackageVersion(
                        assemblyData.Key,
                        assemblyFileNuGetVersion,
                        context.Metadata.Version,
                        context.Metadata.Id);
                }

                var assemblyVersion = assemblyData.Value.Assembly.Name.Version;
                if (!context.Metadata.Version.Version.Equals(assemblyVersion))
                {
                    yield return PackageIssueFactory.AssemblyVersionDoesNotMatchPackageVersion(
                        assemblyData.Key,
                        assemblyVersion,
                        context.Metadata.Version.Version,
                        context.Metadata.Id);
                }
            }
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
