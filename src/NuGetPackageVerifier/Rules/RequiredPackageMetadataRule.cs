// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class RequiredPackageMetadataRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (string.IsNullOrEmpty(context.Metadata.Summary))
            {
                yield return PackageIssueFactory.RequiredSummary();
            }
            if (string.IsNullOrEmpty(context.Metadata.Tags))
            {
                yield return PackageIssueFactory.RequiredTags();
            }
            if (string.IsNullOrEmpty(context.Metadata.Title))
            {
                yield return PackageIssueFactory.RequiredTitle();
            }

            yield break;
        }
    }
}
