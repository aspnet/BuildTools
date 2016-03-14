// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasCorrectJsonNetVersionRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            foreach (var dependencySet in package.DependencyGroups)
            {
                var jsonDependency = dependencySet.Packages.FirstOrDefault(d => d.Id == "Newtonsoft.Json");
                if (jsonDependency != null && !string.Equals(jsonDependency.VersionRange.MinVersion.ToString(), "8.0.3"))
                {
                    yield return PackageIssueFactory.AssemblyHasWrongJsonNetVersion(
                        package.Id,
                        dependencySet.TargetFramework.DotNetFrameworkName,
                        jsonDependency.VersionRange.MinVersion.ToString());
                }
            }
        }
    }
}
