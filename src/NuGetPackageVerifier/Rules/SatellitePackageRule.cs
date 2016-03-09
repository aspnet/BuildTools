// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class SatellitePackageRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            using (var reader = new PackageArchiveReader(nupkgFile.FullName))
            {
                PackageIdentity identity;
                string packageLanguage;
                if (PackageHelper.IsSatellitePackage(reader, out identity, out packageLanguage))
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
            }

            yield break;
        }
    }
}
