// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class RequiredPackageMetadataRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (string.IsNullOrEmpty(context.Metadata.Description))
            {
                yield return PackageIssueFactory.RequiredDescription();
            }
            if (string.IsNullOrEmpty(context.Metadata.Tags))
            {
                yield return PackageIssueFactory.RequiredTags();
            }
            if (string.IsNullOrEmpty(context.Metadata.Id))
            {
                yield return PackageIssueFactory.RequiredId();
            }
        }
    }
}
