// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetPackageVerifier.Rules;
using NuGetPackageVerifier.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace NuGetPackageVerifier
{
    public class SignRequestListsAllSignableFilesRuleTests
    {
        private readonly ITestOutputHelper _output;

        public SignRequestListsAllSignableFilesRuleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItFailsWhenPackageContainsUnlistedFiles()
        {
            var signRequest = @"
<SignRequest>
    <Nupkg Path=""TestPackage.1.0.0.nupkg"">
    </Nupkg>
</SignRequest>";

            var context = TestPackageAnalysisContext.CreateContext(
                _output,
                new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
                signRequest: signRequest);

            using (context)
            {
                var rule = new SignRequestListsAllSignableFiles();

                var errors = rule.Validate(context);

                Assert.NotEmpty(errors);

                Assert.Contains(errors, e =>
                    e.Instance.Equals("lib/netstandard2.0/Test.dll", StringComparison.Ordinal) &&
                    e.IssueId.Equals("FILE_MISSING_FROM_SIGN_REQUEST", StringComparison.Ordinal));

                Assert.Contains(errors, e =>
                    e.Instance.Equals("tools/MyScript.psd1", StringComparison.Ordinal) &&
                    e.IssueId.Equals("FILE_MISSING_FROM_SIGN_REQUEST", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void DoesNotFailWhenSignRequestIncludesAllFiles()
        {
            var signRequest = @"
<SignRequest>
    <Nupkg Path=""TestPackage.1.0.0.nupkg"">
        <File Path=""lib/netstandard2.0/Test.dll"" />
        <File Path=""tools/MyScript.psd1"" />
    </Nupkg>
</SignRequest>";

            var context = TestPackageAnalysisContext.CreateContext(
                _output,
                new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
                signRequest: signRequest);

            using (context)
            {
                var rule = new SignRequestListsAllSignableFiles();

                var errors = rule.Validate(context);

                Assert.Empty(errors);
            }
        }

        [Fact]
        public void DoesNotFailWhenSignRequestListsAllFiles()
        {
            var signRequest = @"
<SignRequest>
    <Nupkg Path=""TestPackage.1.0.0.nupkg"">
        <ExcludedFile Path=""lib/netstandard2.0/Test.dll"" />
        <ExcludedFile Path=""tools/MyScript.psd1"" />
    </Nupkg>
</SignRequest>";

            var context = TestPackageAnalysisContext.CreateContext(
                _output,
                 new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
                signRequest: signRequest);

            using (context)
            {
                var rule = new SignRequestListsAllSignableFiles();

                var errors = rule.Validate(context);

                Assert.Empty(errors);
            }
        }
    }
}
