// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class PrereleaseDependenciesVersionRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (context.Metadata.Version.IsPrerelease)
            {
                yield break;
            }

            foreach (var dependencyGroup in context.Metadata.DependencyGroups)
            {
                foreach (var packageDependency in dependencyGroup.Packages)
                {
                    var minVersion = packageDependency.VersionRange.MinVersion;
                    if (minVersion != null && minVersion.IsPrerelease)
                    {
                        yield return PackageIssueFactory.DependencyVersionIsPrereleaseForRTMPackage(context.Metadata.Id, context.Metadata.Version, packageDependency.Id, packageDependency.VersionRange.MinVersion);
                    }
                }
            }
        }
    }
}
