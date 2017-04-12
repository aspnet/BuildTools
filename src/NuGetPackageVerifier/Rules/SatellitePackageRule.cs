// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGetPackageVerifier.Rules
{
    public class SatellitePackageRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (PackageHelper.IsSatellitePackage(context.PackageReader, out var _, out var _))
            {
                if (context.Metadata.Summary.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageSummaryNotLocalized();
                }
                if (context.Metadata.Title.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageTitleNotLocalized();
                }
                if (context.Metadata.Description.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageDescriptionNotLocalized();
                }
            }
        }
    }
}
