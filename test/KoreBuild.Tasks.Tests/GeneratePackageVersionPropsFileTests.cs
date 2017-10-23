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
    public class GeneratePackageVersionPropsFileTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempFile;

        public GeneratePackageVersionPropsFileTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            _output = output;
            _tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            fixture.InitializeEnvironment(output);
        }

        [Theory]
        [InlineData("Microsoft.Data.Sqlite", "MicrosoftDataSqlitePackageVersion")]
        [InlineData("SQLitePCLRaw.bundle_green", "SQLitePCLRawBundleGreenPackageVersion")]
        [InlineData("runtime.win-x64.Microsoft.NETCore", "RuntimeWinX64MicrosoftNETCorePackageVersion")]
        public void GeneratesVariableName(string id, string varName)
        {
            Assert.Equal(varName, GeneratePackageVersionPropsFile.GetVariableName(id));
        }

        [Fact]
        public void GeneratesFile()
        {
            var engine = new MockEngine(_output);
            var task = new GeneratePackageVersionPropsFile
            {
                BuildEngine = engine,
                Packages = new[]
                {
                    // Order is important. These are intentionally reverse sorted to ensure the generated file sorts properties by prop name
                    new TaskItem("Newtonsoft.Json", new Hashtable{["Version"] = "10.0.3", ["VariableName"] = "JsonNetVersion"}),
                    new TaskItem("Microsoft.Azure", new Hashtable{["Version"] = "1.2.0"}),
                    new TaskItem("Another.Package", new Hashtable{["Version"] = "0.0.1", ["TargetFramework"] = "netstandard1.0"}),
                },
                OutputPath = _tempFile,
            };

            Assert.True(task.Execute(), "Task is expected to pass");

            var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            Assert.Empty(project.Imports);
            Assert.Empty(project.ImportGroups);

            var defaultPropGroup = Assert.Single(project.PropertyGroups, pg => string.IsNullOrEmpty(pg.Label));
            var allProjectsProp = Assert.Single(defaultPropGroup.Properties);
            Assert.Equal("MSBuildAllProjects", allProjectsProp.Name);
            Assert.Empty(allProjectsProp.Condition);
            Assert.Equal("$(MSBuildAllProjects);$(MSBuildThisFileFullPath)", allProjectsProp.Value);

            var versions = Assert.Single(project.PropertyGroups, pg => pg.Label == "Package Versions");

            // Order is important. These should be sorted.
            Assert.Collection(versions.Properties,
                p =>
                {
                    Assert.Equal("AnotherPackagePackageVersion", p.Name);
                    Assert.Equal("Another.Package", p.Label);
                    Assert.Equal("0.0.1", p.Value);
                    Assert.Empty(p.Condition);
                },
                p =>
                {
                    Assert.Equal("JsonNetVersion", p.Name);
                    Assert.Equal("Newtonsoft.Json", p.Label);
                    Assert.Equal("10.0.3", p.Value);
                    Assert.Empty(p.Condition);
                },
                p =>
                {
                    Assert.Equal("MicrosoftAzurePackageVersion", p.Name);
                    Assert.Equal("Microsoft.Azure", p.Label);
                    Assert.Equal("1.2.0", p.Value);
                    Assert.Empty(p.Condition);
                });
        }

        [Fact]
        public void GeneratesImport()
        {
            var task = new GeneratePackageVersionPropsFile
            {
                BuildEngine = new MockEngine(_output),
                Packages = Array.Empty<ITaskItem>(),
                AddOverrideImport = true,
                OutputPath = _tempFile,
            };

            Assert.True(task.Execute(), "Task is expected to pass");
            var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            var import = Assert.Single(project.Imports);
            Assert.Equal("$(DotNetPackageVersionPropsPath)", import.Project);
            Assert.Equal(" '$(DotNetPackageVersionPropsPath)' != '' ", import.Condition);
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
    }
}
