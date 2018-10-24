// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGetPackageVerifier.Rules;
using NuGetPackageVerifier.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace NuGetPackageVerifier
{
    public class PackageRepoMetadataRuleTests
    {
        private readonly ITestOutputHelper _output;

        public PackageRepoMetadataRuleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItWarnsAboutMissingMetadata()
        {
            var metadata = new ManifestMetadata
            {
                Repository = new RepositoryMetadata { },
            };

            var rule = new PackageRepoMetadataRule();

            using (var context = TestPackageAnalysisContext.Create(_output, metadata))
            {
                Assert.Contains(rule.Validate(context), r => string.Equals(r.IssueId, "PACKAGE_MISSING_REPO_TYPE", StringComparison.Ordinal));
                Assert.Contains(rule.Validate(context), r => string.Equals(r.IssueId, "PACKAGE_MISSING_REPO_URL", StringComparison.Ordinal));
                Assert.Contains(rule.Validate(context), r => string.Equals(r.IssueId, "PACKAGE_MISSING_REPO_COMMIT", StringComparison.Ordinal));
            }
        }
    }
}
