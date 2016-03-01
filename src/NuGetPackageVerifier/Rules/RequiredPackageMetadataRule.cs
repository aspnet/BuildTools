// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class RequiredPackageMetadataRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            if (string.IsNullOrEmpty(package.Summary))
            {
                yield return PackageIssueFactory.RequiredSummary();
            }
            if (string.IsNullOrEmpty(package.Tags))
            {
                yield return PackageIssueFactory.RequiredTags();
            }
            if (string.IsNullOrEmpty(package.Title))
            {
                yield return PackageIssueFactory.RequiredTitle();
            }

            yield break;
        }
    }
}
