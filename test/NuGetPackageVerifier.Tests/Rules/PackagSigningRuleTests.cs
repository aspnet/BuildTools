// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NuGetPackageVerifier.Rules;
using NuGetPackageVerifier.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace NuGetPackageVerifier
{
    public class PackagSigningRuleTests
    {
        private readonly ITestOutputHelper _output;

        public PackagSigningRuleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Validate_ReturnsErrorIssue_IfPackageNotSigned()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // PackageSign verification only works on desktop
                return;
            }

            // Arrange
            var context = TestPackageAnalysisContext.CreateContext(
                _output,
                new[] { "lib/netstandard2.0/Test.dll", "tools/MyScript.psd1" });

            using (context)
            {
                var rule = GetRule();

                // Act
                var issues = rule.Validate(context);

                // Assert
                Assert.Collection(
                    issues,
                    issue =>
                    {
                        Assert.Equal(TestPackageAnalysisContext.PackageId, issue.Instance);
                        Assert.Equal("PACKAGE_SIGN_VERIFICATION_FAILED", issue.IssueId);
                        Assert.Equal(PackageIssueLevel.Error, issue.Level);
                        Assert.StartsWith($"Sign verification for package {TestPackageAnalysisContext.PackageId} failed:", issue.Issue);
                    });
            }
        }

        [Fact]
        public async Task Validate_ReturnsEmpty()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // PackageSign verification only works on desktop
                return;
            }

            // Arrange
            const string DownloadUri = "https://dotnet.myget.org/F/dotnet-core/api/v2/package/Microsoft.AspNetCore.All/2.1.0-rc1-30682";
            using (var disposableDirectory = new DisposableDirectory())
            {
                var file = await DownloadFileAsync(DownloadUri, disposableDirectory.Path);

                var context = new PackageAnalysisContext
                {
                    Logger = new TestLogger(_output),
                    PackageFileInfo = new FileInfo(file),
                };

                var rule = GetRule();

                // Act
                var issues = rule.Validate(context);

                // Assert
                Assert.Empty(issues);
            }
        }

        private static PackageSigningRule GetRule()
        {
            var solutionDir = SolutionDirectory.GetSolutionRootDirectory();
            var nugetExe = Path.Combine(solutionDir, "obj", "nuget.exe");
            if (!File.Exists(nugetExe))
            {
                throw new FileNotFoundException($"File {nugetExe} could not be found. Ensure build /t:Prepare is invoked from the root of this repository, before this test is executed.");
            }

            return new PackageSigningRule(nugetExe);
        }

        private async Task<string> DownloadFileAsync(string downloadUri, string basePath)
        {
            var client = new HttpClient();
            var response = await client.GetAsync(downloadUri);

            var fileName = Path.GetRandomFileName();
            var filePath = Path.Combine(basePath, fileName);
            using (var fileStream = File.Create(filePath))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            return filePath;
        }
    }
}
