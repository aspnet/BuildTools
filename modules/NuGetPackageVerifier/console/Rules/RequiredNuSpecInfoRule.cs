// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class RequiredNuSpecInfoRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (string.IsNullOrEmpty(context.Metadata.Copyright))
            {
                yield return PackageIssueFactory.RequiredCopyright();
            }
            if (context.Metadata.LicenseUrl == null)
            {
                yield return PackageIssueFactory.RequiredLicenseUrl();
            }
            if (context.Metadata.IconUrl == null)
            {
                yield return PackageIssueFactory.RequiredIconUrl();
            }
            if (context.Metadata.ProjectUrl == null)
            {
                yield return PackageIssueFactory.RequiredProjectUrl();
            }
            if (!context.Metadata.RequireLicenseAcceptance)
            {
                yield return PackageIssueFactory.RequiredRequireLicenseAcceptanceTrue();
            }
        }
    }
}