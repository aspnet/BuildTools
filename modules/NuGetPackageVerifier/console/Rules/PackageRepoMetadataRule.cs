// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class PackageRepoMetadataRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            var repoMetadata = context.PackageReader.NuspecReader.GetRepositoryMetadata();

            if (repoMetadata == null)
            {
                yield return PackageIssueFactory.PackageRepositoryMetadataMissing();
            }
            else
            {
                if (string.IsNullOrEmpty(repoMetadata.Url))
                {
                    yield return PackageIssueFactory.PackageRepositoryUrl();
                }

                if (string.IsNullOrEmpty(repoMetadata.Type))
                {
                    yield return PackageIssueFactory.PackageRepositoryType();
                }

                if (string.IsNullOrEmpty(repoMetadata.Commit))
                {
                    yield return PackageIssueFactory.PackageRepositoryCommit();
                }
            }
        }
    }
}
