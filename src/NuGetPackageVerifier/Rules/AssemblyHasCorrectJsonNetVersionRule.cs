// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasCorrectJsonNetVersionRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            foreach (var dependencySet in package.DependencySets)
            {
                var jsonDependency = dependencySet.Dependencies.FirstOrDefault(d => d.Id == "Newtonsoft.Json");
                if (jsonDependency != null && !string.Equals(jsonDependency.VersionSpec.ToString(), "8.0.2"))
                {
                    yield return PackageIssueFactory.AssemblyHasWrongJsonNetVersion(
                        package.Id,
                        dependencySet.TargetFramework.FullName,
                        jsonDependency.VersionSpec.ToString());
                }
            }
        }
    }
}
