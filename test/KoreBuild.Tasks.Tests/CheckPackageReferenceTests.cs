// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class CheckPackageReferenceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly MockEngine _engine;

        public CheckPackageReferenceTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _engine = new MockEngine(output);
            fixture.InitializeEnvironment(output);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public void PassesWhenAllRequirementsAreSatisifed()
        {
            var depsProps = Path.Combine(_tempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1.0.0</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(_tempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
    <PackageReference Include=`AspNetCore`>
       <Version>$(AspNetCorePackageVersion)</Version>
    </PackageReference>
  </ItemGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                DependenciesFile = depsProps,
                Projects = new[] { new TaskItem(csproj) }
            };

            Assert.True(task.Execute(), "Task is expected to pass");
        }

        [Fact]
        public void FailsWhenDependenciesHasNoPropGroup()
        {
            var depsFile = Path.Combine(_tempDir, "deps.props");
            File.WriteAllText(depsFile, $@"
<Project>
  <PropertyGroup>
    <AspNetCorePackageVersion>1.0.0</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                Projects = new[] { new TaskItem(depsFile) },
                DependenciesFile = depsFile,
            };

            Assert.True(task.Execute(), "Task is expected to pass");
            Assert.NotEmpty(_engine.Warnings);
            Assert.Contains(_engine.Warnings, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefPropertyGroupNotFound);
        }

        [Fact]
        public void FailsWhenVariableIsNotInDependenciesPropsFile()
        {
            var depsProps = Path.Combine(_tempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(_tempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(_engine.Errors);
            Assert.Contains(_engine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.VariableNotFoundInDependenciesPropsFile);
        }

        [Fact]
        public void FailsWhenPackageVersionFloat()
        {
            var depsProps = Path.Combine(_tempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1.0.0-*</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(_tempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(_engine.Errors);
            Assert.Contains(_engine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefHasFloatingVersion);
        }

        [Fact]
        public void FailsWhenPackageVersionIsInvalid()
        {
            var depsProps = Path.Combine(_tempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(_tempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(_engine.Errors);
            Assert.Contains(_engine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.InvalidPackageVersion);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("1.0.0-$(Suffix)")]
        [InlineData("$(Prefix)-1.0.0-$(Suffix)")]
        [InlineData("$(Prefix)-1.0.0")]
        public void FailsWhenPackagesReferenceVersionDoesNotCompletelyUseVariables(string version)
        {
            var depsProps = Path.Combine(_tempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
   <PropertyGroup Label=`Package Versions` />
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(_tempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`{version}` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            _engine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = _engine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(_engine.Errors);
            var error = Assert.Single(_engine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefHasLiteralVersion);
            Assert.Equal(4, error.LineNumber);
        }
    }
}
