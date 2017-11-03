// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class GenerateDependenciesPropsFileTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDir;

        public GenerateDependenciesPropsFileTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            _output = output;
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            fixture.InitializeEnvironment(output);
        }

        [Fact]
        public void GeneratesVariableName()
        {
            var generatedFile = Path.Combine(_tempDir, "deps.props");
            var csproj = Path.Combine(_tempDir, "test.csproj");
            CreateProject(csproj, "1.2.3");

            var task = new GenerateDependenciesPropsFile
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = generatedFile,
                Projects = new[] { new TaskItem(csproj) },
                Properties = Array.Empty<string>(),
            };

            Assert.True(task.Execute(), "Task is expected to pass");
            var depsFile = ProjectRootElement.Open(generatedFile);
            _output.WriteLine(File.ReadAllText(generatedFile));
            var pg = Assert.Single(depsFile.PropertyGroups, p => p.Label == "Package Versions");
            var prop = Assert.Single(pg.Properties, p => p.Name == "MyDependencyPackageVersion");
            Assert.Equal("1.2.3", prop.Value);
        }

        [Fact]
        public void IgnoresImplicitlyDefinedVariables()
        {
            var generatedFile = Path.Combine(_tempDir, "deps.props");
            var csproj = Path.Combine(_tempDir, "test.csproj");
            File.WriteAllText(csproj, $@"
<Project Sdk=`Microsoft.NET.Sdk`>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>".Replace('`', '"'));

            var task = new GenerateDependenciesPropsFile
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = generatedFile,
                Projects = new[] { new TaskItem(csproj) },
                Properties = Array.Empty<string>(),
            };

            Assert.True(task.Execute(), "Task is expected to pass");
            var depsFile = ProjectRootElement.Open(generatedFile);
            _output.WriteLine(File.ReadAllText(generatedFile));
            var pg = Assert.Single(depsFile.PropertyGroups, p => p.Label == "Package Versions");
            Assert.Empty(pg.Properties);
        }

        [Fact]
        public void FailsWhenConflictingVersions()
        {
            var generatedFile = Path.Combine(_tempDir, "deps.props");
            var csproj1 = Path.Combine(_tempDir, "test1.csproj");
            var csproj2 = Path.Combine(_tempDir, "test2.csproj");
            CreateProject(csproj1, "1.2.3");
            CreateProject(csproj2, "4.5.6");

            var engine = new MockEngine(_output) { ContinueOnError = true };
            var task = new GenerateDependenciesPropsFile
            {
                BuildEngine = engine,
                DependenciesFile = generatedFile,
                Projects = new[] { new TaskItem(csproj1), new TaskItem(csproj2) },
                Properties = Array.Empty<string>(),
            };

            Assert.False(task.Execute(), "Task is expected to fail");
            Assert.Single(engine.Errors, e => e.Code == KoreBuildErrors.Prefix + KoreBuildErrors.ConflictingPackageReferenceVersions);
        }

        [Fact]
        public void DoesNotFailWhenConflictingVersionsAreSuppressed()
        {
            var generatedFile = Path.Combine(_tempDir, "deps.props");
            var csproj1 = Path.Combine(_tempDir, "test1.csproj");
            var csproj2 = Path.Combine(_tempDir, "test2.csproj");
            CreateProject(csproj1, "1.2.3");

            File.WriteAllText(csproj2, $@"
<Project Sdk=`Microsoft.NET.Sdk`>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=`MyDependency` Version=`4.5.6` NoWarn=`KRB4002` />
  </ItemGroup>
</Project>".Replace('`', '"'));

            var task = new GenerateDependenciesPropsFile
            {
                BuildEngine = new MockEngine(_output),
                DependenciesFile = generatedFile,
                Projects = new[] { new TaskItem(csproj1), new TaskItem(csproj2) },
                Properties = Array.Empty<string>(),
            };

            Assert.True(task.Execute(), "Task is expected to oass");
        }

        private static void CreateProject(string csprojFilePath, string version)
        {
            File.WriteAllText(csprojFilePath, $@"
<Project Sdk=`Microsoft.NET.Sdk`>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=`MyDependency` Version=`{version}` />
  </ItemGroup>
</Project>".Replace('`', '"'));
        }


        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
