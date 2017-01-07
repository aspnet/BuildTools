// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class DependenciesVersionRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!context.Metadata.Version.IsPrerelease)
            {
                foreach (var dependencyGroup in context.Metadata.DependencyGroups)
                {                    
                    foreach (var packageDependency in dependencyGroup.Packages)
                    {
                        if (packageDependency.VersionRange.HasUpperBound)
                        {
                            yield return PackageIssueFactory.DependencyVersionHasUpperBound(context.Metadata.Id, packageDependency.Id);
                        }

                        if (!packageDependency.VersionRange.HasLowerBound)
                        {
                            yield return PackageIssueFactory.DependencyVersionDoesNotHaveLowerBound(context.Metadata.Id, packageDependency.Id);
                        }

                        if (packageDependency.VersionRange.MinVersion.IsPrerelease)
                        {
                            yield return PackageIssueFactory.DependencyVersionIsPrereleaseForRTMPackage(context.Metadata.Id, packageDependency.Id);
                        }
                    }
                }
            }
        }
    }
}
