// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class SatellitePackageRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            if (package.IsSatellitePackage())
            {
                if (package.Summary.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageSummaryNotLocalized();
                }
                if (package.Title.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageTitleNotLocalized();
                }
                if (package.Description.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageDescriptionNotLocalized();
                }
            }

            yield break;
        }
    }
}
