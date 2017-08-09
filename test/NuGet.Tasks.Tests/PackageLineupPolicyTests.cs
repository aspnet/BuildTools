// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Tasks.Lineup;
using NuGet.Tasks.Policies;
using NuGet.Tasks.ProjectModel;
using NuGet.Versioning;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.Tasks.Tests
{
    public class PackageLineupPolicyTests
    {
        [Fact]
        public async Task CreatesRestoreRequests()
        {
            var requests = new[]
            {
                new TaskItem("Example.Lineup", new Hashtable{ ["Version"] = "1.0.0-*" })
            };

            RestoreContext restoreArgs = null;
            var mockCommand = new Mock<IRestoreLineupCommand>();
            mockCommand.Setup(i => i.ExecuteAsync(It.IsAny<RestoreContext>(), It.IsAny<CancellationToken>()))
                .Callback<RestoreContext, CancellationToken>((ctx, _) =>
                {
                    restoreArgs = ctx;
                })
                .Returns(Task.FromResult(true));

            var policy = new PackageLineupPolicy(requests, mockCommand.Object);
            await policy.ApplyAsync(new PolicyContext { Log = new TaskLoggingHelper(new MockEngine(), "TestTask"), }, default(CancellationToken));
            mockCommand.VerifyAll();

            Assert.NotNull(restoreArgs);

            var request = Assert.Single(restoreArgs.PackageLineups);
            Assert.Equal("Example.Lineup", request.Id);
            Assert.Equal(VersionRange.Parse("1.0.0-*"), request.Version);
        }

        [Fact]
        public async Task GeneratesMSBuildFile()
        {
            // arrange
            var mockCommand = new Mock<IRestoreLineupCommand>();
            mockCommand.Setup(i => i.ExecuteAsync(It.IsAny<RestoreContext>(), It.IsAny<CancellationToken>()))
                .Callback<RestoreContext, CancellationToken>((ctx, _) =>
                {
                    ctx.VersionSource.AddPackagesFromLineup("SampleLineup",
                        NuGetVersion.Parse("1.0.0"),
                        new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup(
                            NuGetFramework.AnyFramework,
                            new [] {
                                new PackageDependency("Microsoft.AspNetCore", VersionRange.Parse("2.0.0")),
                                new PackageDependency("xunit", VersionRange.Parse("2.3.0")),
                                new PackageDependency("Microsoft.NETCore.App", VersionRange.Parse("99.99.99")),
                            }),
                    });
                })
                .Returns(Task.FromResult(true));

            var policy = new PackageLineupPolicy(new[]
            {
                new TaskItem("Example.Lineup", new Hashtable{ ["Version"] = "1.0.0-*" })
            },
            mockCommand.Object);

            var packageRef = new[]
            {
                new PackageReferenceInfo("Microsoft.NETCore.App", "2.0.0", isImplicitlyDefined: true, noWarn: false),
                new PackageReferenceInfo("Microsoft.AspNetCore", string.Empty, false, false),
                new PackageReferenceInfo("xunit", "2.2.0", false, false),
            };

            var framework = new[] { new ProjectFrameworkInfo(FrameworkConstants.CommonFrameworks.NetCoreApp20, packageRef) };
            var projectDir = AppContext.BaseDirectory;
            var project = new ProjectInfo(Path.Combine(projectDir, "Test.csproj"), null, framework, Array.Empty<DotNetCliReferenceInfo>());

            var context = new PolicyContext
            {
                Log = new TaskLoggingHelper(new MockEngine(), "TestTask"),
                Projects = new[] { project },
            };

            // act
            await policy.ApplyAsync(context, default(CancellationToken));

            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                project.TargetsExtension.Project.Save(writer);
            }

            var xml = sb.ToString();

            // assert

            Assert.Contains("<PackageLineup Include=\"SampleLineup\" Version=\"1.0.0\" />", xml);

            // Should pin for PackageRef that has no version on it
            Assert.Contains("<PackageReference Update=\"Microsoft.AspNetCore\" Version=\"2.0.0\" AutoVersion=\"true\" IsImplicitlyDefined=\"true\" />", xml);

            // Should not overwrite for PackageRef that have Version
            Assert.DoesNotContain("xunit", xml);

            // May overwrite for PackageRef that have Version but are defined by the SDK
            Assert.Contains("<PackageReference Update=\"Microsoft.NETCore.App\" Version=\"99.99.99\" AutoVersion=\"true\" IsImplicitlyDefined=\"true\" />", xml);
        }
    }
}
