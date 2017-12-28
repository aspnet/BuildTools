// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetPackageVerifier.Rules;
using NuGetPackageVerifier.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace NuGetPackageVerifier.Tests
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
    <Container Path=""TestPackage.1.0.0.nupkg"" Type=""nupkg"">
    </Container>
</SignRequest>";

            var context = TestHelper.CreateAnalysisContext(_output,
            new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
            signRequest: signRequest);

            var rule = new SignRequestListsAllSignableFiles();

            var errors = rule.Validate(context);

            Assert.NotEmpty(errors);

            Assert.Contains(errors, e =>
                e.Instance.Equals("lib/netstandard2.0/Test.dll", StringComparison.Ordinal)
                && e.IssueId.Equals("FILE_MISSING_FROM_SIGN_REQUEST", StringComparison.Ordinal));

            Assert.Contains(errors, e =>
                e.Instance.Equals("tools/MyScript.psd1", StringComparison.Ordinal)
                && e.IssueId.Equals("FILE_MISSING_FROM_SIGN_REQUEST", StringComparison.Ordinal));
        }

        [Fact]
        public void DoesNotFailWhenSignRequestIncludesAllFiles()
        {
            var signRequest = @"
<SignRequest>
    <Container Path=""TestPackage.1.0.0.nupkg"" Type=""nupkg"">
        <File Path=""lib/netstandard2.0/Test.dll"" />
        <File Path=""tools/MyScript.psd1"" />
    </Container>
</SignRequest>";

            var context = TestHelper.CreateAnalysisContext(_output,
                new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
                signRequest: signRequest);

            var rule = new SignRequestListsAllSignableFiles();

            var errors = rule.Validate(context);

            Assert.Empty(errors);
        }

        [Fact]
        public void DoesNotFailWhenSignRequestListsAllFiles()
        {
            var signRequest = @"
<SignRequest>
    <Container Path=""TestPackage.1.0.0.nupkg"" Type=""nupkg"">
        <ExcludedFile Path=""lib/netstandard2.0/Test.dll"" />
        <ExcludedFile Path=""tools/MyScript.psd1"" />
    </Container>
</SignRequest>";

            var context = TestHelper.CreateAnalysisContext(_output,
                 new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" },
                signRequest: signRequest);

            var rule = new SignRequestListsAllSignableFiles();

            var errors = rule.Validate(context);

            Assert.Empty(errors);
        }
    }
}
