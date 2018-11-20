// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class CheckPackageReferenceTests : TaskTestBase
    {
        public CheckPackageReferenceTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture) : base(output, fixture)
        {
        }

        [Fact]
        public void ItAllowsPinnedAndUnpinnedVersions()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions: Latest`>
    <LatestPackageVersion>1.0.0</LatestPackageVersion>
  </PropertyGroup>

  <Import Project=`$(DotNetPackageVersionsPropsPath)` />

  <PropertyGroup Label=`Package Versions: Pinned`>
    <BaseLinePackageVersion>1.0.0</BaseLinePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`Latest` Version=`$(LatestPackageVersion)` />
    <PackageReference Include=`BaseLine`>
       <Version>$(BaseLinePackageVersion)</Version>
    </PackageReference>
  </ItemGroup>
</Project>
".Replace('`', '"'));

            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                DependenciesFile = depsProps,
                Projects = new[] { new TaskItem(csproj) }
            };

            Assert.True(task.Execute(), "Task is expected to pass");
        }

        [Fact]
        public void PassesWhenAllRequirementsAreSatisifed()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1.0.0</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
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

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                DependenciesFile = depsProps,
                Projects = new[] { new TaskItem(csproj) }
            };

            Assert.True(task.Execute(), "Task is expected to pass");
        }

        [Fact]
        public void IgnoresUpdateAndRemoveItems()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions` />
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Update=`Microsoft.AspNetCore.All` PrivateAssets=`All` />
    <PackageReference Remove=`Microsoft.AspNetCore.All` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                DependenciesFile = depsProps,
                Projects = new[] { new TaskItem(csproj) }
            };

            Assert.True(task.Execute(), "Task is expected to pass");
        }

        [Fact]
        public void FailsWhenDependenciesHasNoPropGroup()
        {
            var depsFile = Path.Combine(TempDir, "deps.props");
            File.WriteAllText(depsFile, $@"
<Project>
  <PropertyGroup>
    <AspNetCorePackageVersion>1.0.0</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                Projects = new[] { new TaskItem(depsFile) },
                DependenciesFile = depsFile,
            };

            Assert.True(task.Execute(), "Task is expected to pass");
            Assert.NotEmpty(MockEngine.Warnings);
            Assert.Contains(MockEngine.Warnings, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefPropertyGroupNotFound);
        }

        [Fact]
        public void FailsWhenVariableIsNotInDependenciesPropsFile()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(MockEngine.Errors);
            Assert.Contains(MockEngine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.VariableNotFoundInDependenciesPropsFile);
        }

        [Fact]
        public void FailsWhenPackageVersionFloat()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1.0.0-*</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(MockEngine.Errors);
            Assert.Contains(MockEngine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefHasFloatingVersion);
        }

        [Fact]
        public void FailsWhenPackageVersionIsInvalid()
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
  <PropertyGroup Label=`Package Versions`>
    <AspNetCorePackageVersion>1</AspNetCorePackageVersion>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`$(AspNetCorePackageVersion)` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(MockEngine.Errors);
            Assert.Contains(MockEngine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.InvalidPackageVersion);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("1.0.0-$(Suffix)")]
        [InlineData("$(Prefix)-1.0.0-$(Suffix)")]
        [InlineData("$(Prefix)-1.0.0")]
        public void FailsWhenPackagesReferenceVersionDoesNotCompletelyUseVariables(string version)
        {
            var depsProps = Path.Combine(TempDir, "dependencies.props");
            File.WriteAllText(depsProps, $@"
<Project>
   <PropertyGroup Label=`Package Versions` />
</Project>
".Replace('`', '"'));

            var csproj = Path.Combine(TempDir, "Test.csproj");
            File.WriteAllText(csproj, $@"
<Project>
  <ItemGroup>
    <PackageReference Include=`AspNetCore` Version=`{version}` />
  </ItemGroup>
</Project>
".Replace('`', '"'));

            MockEngine.ContinueOnError = true;
            var task = new CheckPackageReferences
            {
                BuildEngine = MockEngine,
                Projects = new[] { new TaskItem(csproj) },
                DependenciesFile = depsProps,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.NotEmpty(MockEngine.Errors);
            var error = Assert.Single(MockEngine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.PackageRefHasLiteralVersion);
            Assert.Equal(4, error.LineNumber);
        }
    }
}
