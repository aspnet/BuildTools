// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using BuildTools.Tasks.Tests;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class UpgradeDependenciesTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ITestOutputHelper _output;

        public UpgradeDependenciesTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            fixture.InitializeEnvironment(output);
            _tempDir = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _output = output;
        }

        [Fact]
        public async Task WarnsWhenVariableIsNotInPackage()
        {
            // arrange
            var packageId = new PackageIdentity("Lineup", NuGetVersion.Parse("1.0.0"));
            var lineupPackagePath = CreateLineup(packageId);
            var depsFilePath = CreateProjectDepsFile(("PackageVersionVar", "1.0.0"));
            var engine = new MockEngine(_output);

            // act
            var task = new UpgradeDependencies
            {
                BuildEngine = engine,
                DependenciesFile = depsFilePath,
                LineupPackageId = packageId.Id,
                LineupPackageRestoreSource = _tempDir,
            };

            // assert
            Assert.True(await task.ExecuteAsync(), "Task is expected to pass");
            var warning = Assert.Single(engine.Warnings);
            Assert.Equal(KoreBuildErrors.Prefix + KoreBuildErrors.PackageVersionNotFoundInLineup, warning.Code);

            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("1.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
        }

        [Fact]
        public async Task ModifiesVariableValue()
        {
            // arrange
            var packageId = new PackageIdentity("Lineup", NuGetVersion.Parse("1.0.0"));
            var lineupPackagePath = CreateLineup(packageId, ("PackageVersionVar", "2.0.0"));
            var depsFilePath = CreateProjectDepsFile(("PackageVersionVar", "1.0.0"));

            // act
            var task = new UpgradeDependencies
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = depsFilePath,
                LineupPackageId = packageId.Id,
                LineupPackageRestoreSource = _tempDir,
            };

            // assert
            Assert.True(await task.ExecuteAsync(), "Task is expected to pass");
            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("2.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
        }

        [Fact]
        public async Task ModifiesVariableValueUsingDepsFile()
        {
            // arrange
            var depsFilePath = CreateProjectDepsFile(("PackageVersionVar", "1.0.0"));
            var updatedDepsFilePath = CreateProjectDepsFile(Path.Combine(_tempDir, "dependencies.props"), ("PackageVersionVar", "2.0.0"));

            // act
            var task = new UpgradeDependencies
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = depsFilePath,
                LineupDependenciesFile = updatedDepsFilePath
            };

            // assert
            Assert.True(await task.ExecuteAsync(), "Task is expected to pass");
            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("2.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
        }

        [Fact]
        public async Task SnapsInternalAspNetCoreSdkToBuildTools()
        {
            // arrange
            var packageId = new PackageIdentity("Lineup", NuGetVersion.Parse("1.0.0"));
            var lineupPackagePath = CreateLineup(packageId, ("InternalAspNetCoreSdkPackageVersion", "2.0.0"));
            var depsFilePath = CreateProjectDepsFile(("InternalAspNetCoreSdkPackageVersion", "1.0.0"));

            // act
            var task = new UpgradeDependencies
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = depsFilePath,
                LineupPackageId = packageId.Id,
                LineupPackageRestoreSource = _tempDir,
            };

            // assert
            Assert.True(await task.ExecuteAsync(), "Task is expected to pass");
            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal(KoreBuildVersion.Current, modifiedDepsFile.VersionVariables["InternalAspNetCoreSdkPackageVersion"]);
        }

        [Fact]
        public async Task DoesNotModifiesFileIfNoChanges()
        {
            // arrange
            var packageId = new PackageIdentity("Lineup", NuGetVersion.Parse("1.0.0"));
            var pkg = ("PackageVersionVar", "1.0.0");
            var lineupPackagePath = CreateLineup(packageId, pkg);
            var depsFilePath = CreateProjectDepsFile(pkg);
            var created = File.GetLastWriteTime(depsFilePath);

            // act
            var task = new UpgradeDependencies
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = depsFilePath,
                LineupPackageId = packageId.Id,
                LineupPackageRestoreSource = _tempDir,
            };

            // assert
            Assert.True(await task.ExecuteAsync(), "Task is expected to pass");
            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("1.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
            Assert.Equal(created, File.GetLastWriteTime(depsFilePath));
        }

        private string CreateProjectDepsFile(params (string varName, string version)[] variables)
        {
            return CreateProjectDepsFile(Path.Combine(_tempDir, "projectdeps.props"), variables);
        }

        private string CreateProjectDepsFile(string depsFilePath, params (string varName, string version)[] variables)
        {
            var proj = ProjectRootElement.Create(NewProjectFileOptions.None);
            var originalDepsFile = DependencyVersionsFile.Load(proj);
            foreach (var item in variables)
            {
                originalDepsFile.Set(item.varName, item.version);
            }
            originalDepsFile.Save(depsFilePath);
            return depsFilePath;
        }

        private string CreateLineup(PackageIdentity identity, params (string varName, string version)[] variables)
        {
            var output = Path.Combine(_tempDir, $"{identity.Id}.{identity.Version}.nupkg");

            var proj = ProjectRootElement.Create(NewProjectFileOptions.None);
            var depsFiles = DependencyVersionsFile.Load(proj);
            foreach (var item in variables)
            {
                depsFiles.Set(item.varName, item.version);
            }
            depsFiles.Save(Path.Combine(_tempDir, "dependencies.props"));

            var builder = new PackageBuilder
            {
                Id = identity.Id,
                Version = identity.Version,
                Owners = { "Test" },
                Authors = { "Test" },
                Description = "Test lineup package"
            };

            builder.AddFiles(_tempDir, "dependencies.props", "build/dependencies.props");

            using (var stream = File.Create(output))
            {
                builder.Save(stream);
            }

            return output;
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
