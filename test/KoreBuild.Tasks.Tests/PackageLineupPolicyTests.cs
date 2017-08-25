// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading;
using BuildTools.Tasks.Tests;
using KoreBuild.Tasks.Lineup;
using KoreBuild.Tasks.Policies;
using KoreBuild.Tasks.ProjectModel;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace KoreBuild.Tasks.Tests
{
    public class PackageLineupPolicyTests : IDisposable
    {
        private readonly string _tempDir;
        private TaskLoggingHelper _logger;

        public PackageLineupPolicyTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _logger = new TaskLoggingHelper(new MockEngine(), "Test");
        }

        [Fact]
        public async Task CreatesRestoreRequests()
        {
            var requests = new[]
            {
                new TaskItem("Example.Lineup", new Hashtable{ ["Version"] = "1.0.0-*", ["LineupType"] = "Package" })
            };

            RestoreContext restoreArgs = null;
            var mockCommand = new Mock<IRestoreLineupCommand>();
            mockCommand.Setup(i => i.ExecuteAsync(It.IsAny<RestoreContext>(), It.IsAny<CancellationToken>()))
                .Callback<RestoreContext, CancellationToken>((ctx, _) =>
                {
                    restoreArgs = ctx;
                })
                .Returns(Task.FromResult(true));

            var logger = new TaskLoggingHelper(new MockEngine(), "TestTask");
            var policy = new PackageLineupPolicy(requests, mockCommand.Object, logger);
            await policy.ApplyAsync(new PolicyContext { Log = logger, }, default(CancellationToken));
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
                        new[]
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
                new TaskItem("Example.Lineup", new Hashtable{ ["Version"] = "1.0.0-*", ["LineupType"] = "Package" })
            },
            mockCommand.Object,
            _logger);

            var packageRef = new[]
            {
                new PackageReferenceInfo("Microsoft.NETCore.App", "2.0.0", isImplicitlyDefined: true, noWarn: Array.Empty<string>()),
                new PackageReferenceInfo("Microsoft.AspNetCore", string.Empty, false, Array.Empty<string>()),
                new PackageReferenceInfo("xunit", "2.2.0", false, Array.Empty<string>()),
            };

            var framework = new[] { new ProjectFrameworkInfo(FrameworkConstants.CommonFrameworks.NetCoreApp20, packageRef) };
            var projectDir = AppContext.BaseDirectory;
            var project = new ProjectInfo(Path.Combine(projectDir, "Test.csproj"), null, framework, Array.Empty<DotNetCliReferenceInfo>());

            var context = new PolicyContext
            {
                Log = _logger,
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
            Assert.Contains("<PackageReference Update=\"Microsoft.AspNetCore\" Version=\"2.0.0\" IsImplicitlyDefined=\"true\" />", xml);

            // Should not overwrite for PackageRef that have Version
            Assert.DoesNotContain("xunit", xml);

            // May overwrite for PackageRef that have Version but are defined by the SDK
            Assert.Contains("<PackageReference Update=\"Microsoft.NETCore.App\" Version=\"99.99.99\" IsImplicitlyDefined=\"true\" />", xml);
        }

        [Theory]
        [MemberData(nameof(NetCoreAppProjectData))]
        public async Task SelectsTheRightPackageBasedOnProjectFramework(NuGetFramework projectFramework, string expectedVersion)
        {
            // arrange
            var mockCommand = new Mock<IRestoreLineupCommand>();
            mockCommand.Setup(i => i.ExecuteAsync(It.IsAny<RestoreContext>(), It.IsAny<CancellationToken>()))
                .Callback<RestoreContext, CancellationToken>((ctx, _) =>
                {
                    ctx.VersionSource.AddPackagesFromLineup("SampleLineup",
                        NuGetVersion.Parse("1.0.0"),
                        new[]
                        {
                            new PackageDependencyGroup(
                                FrameworkConstants.CommonFrameworks.NetCoreApp10, new [] { new PackageDependency("Microsoft.NETCore.App", VersionRange.Parse("1.0.5")), }),
                            new PackageDependencyGroup(
                                FrameworkConstants.CommonFrameworks.NetCoreApp11, new [] { new PackageDependency("Microsoft.NETCore.App", VersionRange.Parse("1.1.2")), }),
                            new PackageDependencyGroup(
                                FrameworkConstants.CommonFrameworks.NetCoreApp20, new [] { new PackageDependency("Microsoft.NETCore.App", VersionRange.Parse("2.0.3")), }),
                            new PackageDependencyGroup(
                                MyFrameworks.NetCoreApp21, new [] { new PackageDependency("Microsoft.NETCore.App", VersionRange.Parse("2.1.9")), }),
                        });
                })
                .Returns(Task.FromResult(true));

            var policy = new PackageLineupPolicy(new[]
            {
                new TaskItem("Example.Lineup", new Hashtable{ ["Version"] = "1.0.0-*", ["LineupType"] = "Package" })
            },
            mockCommand.Object,
            _logger);

            var frameworks = new[] {
                new ProjectFrameworkInfo(projectFramework, new[] { new PackageReferenceInfo("Microsoft.NETCore.App", "99.99.99", isImplicitlyDefined: true, noWarn: Array.Empty<string>()) }),
            };
            var projectDir = AppContext.BaseDirectory;
            var project = new ProjectInfo(Path.Combine(projectDir, "Test.csproj"), null, frameworks, Array.Empty<DotNetCliReferenceInfo>());

            var context = new PolicyContext
            {
                Log = _logger,
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
            Assert.Contains($"<PackageReference Update=\"Microsoft.NETCore.App\" Version=\"{expectedVersion}\" IsImplicitlyDefined=\"true\" />", xml);
        }

        public static TheoryData<NuGetFramework, string> NetCoreAppProjectData
            => new TheoryData<NuGetFramework, string>
            {
                { FrameworkConstants.CommonFrameworks.NetCoreApp10, "1.0.5" },
                { FrameworkConstants.CommonFrameworks.NetCoreApp11, "1.1.2" },
                { FrameworkConstants.CommonFrameworks.NetCoreApp20, "2.0.3" },
                { MyFrameworks.NetCoreApp21, "2.1.9" },
            };

        [Fact]
        public async Task SupportsFolderLineups()
        {
            var packageDir = Path.Combine(_tempDir, "packages");
            Directory.CreateDirectory(packageDir);

            CreateTestNupkg("TestBundledPkg", "0.0.1", packageDir);
            CreateTestNupkg("AnotherBundledPkg", "0.0.1", packageDir);

            var policy = new PackageLineupPolicy(
                new[]
                {
                    new TaskItem(packageDir, new Hashtable { ["LineupType"] = "Folder" }),
                },
                _logger);

            var testProject = new ProjectInfo(
                        Path.Combine(_tempDir, "sample.csproj"), null,
                        new[]
                        {
                            new ProjectFrameworkInfo(FrameworkConstants.CommonFrameworks.NetCoreApp20, new[]
                            {
                                new PackageReferenceInfo("TestBundledPkg", string.Empty, false, Array.Empty<string>()),
                                new PackageReferenceInfo("AnotherBundledPkg", "1.0.0", false, Array.Empty<string>())
                            })
                        },
                        Array.Empty<DotNetCliReferenceInfo>());
            var context = new PolicyContext
            {
                Projects = new[] { testProject },
                Log = _logger,
            };

            await policy.ApplyAsync(context, default(CancellationToken));

            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                testProject.TargetsExtension.Project.Save(writer);
            }

            var xml = sb.ToString();

            // assert
            Assert.Contains("<PackageReference Update=\"TestBundledPkg\" Version=\"0.0.1\" IsImplicitlyDefined=\"true\" />", xml);
            Assert.DoesNotContain("AnotherBundledPkg", xml);

            Assert.Contains(packageDir + "</RestoreAdditionalProjectSources>", xml);
        }

        private FileInfo CreateTestNupkg(string id, string version, string packageDir)
        {
            var builder = new PackageBuilder
            {
                Id = id,
                Authors =
                {
                    "Test"
                },
                Description = "Test package",
                Version = new NuGetVersion(version),
                DependencyGroups = { new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] { new PackageDependency("Test") }) }
            };

            var fileInfo = new FileInfo(Path.Combine(packageDir, $"{id}.{version}.nupkg"));
            using (var stream = File.Create(fileInfo.FullName))
            {
                builder.Save(stream);
            }
            return fileInfo;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                Console.WriteLine("Failed to delete " + _tempDir);
            }
        }
    }
}
