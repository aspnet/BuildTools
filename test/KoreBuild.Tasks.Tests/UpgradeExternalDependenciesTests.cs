// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using BuildTools.Tasks.Tests;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class UpgradeExternalDependenciesTests : DependenciesTestsBase
    {
        public UpgradeExternalDependenciesTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
            : base(output, fixture)
        {
        }

        [Fact]
        public void UpdatesVariableValue()
        {
            // Arrange
            var depsFilePath = CreateProjectDepsFile(("PackageVersionVar", "1.0.0"));
            var updateSource = CreateUpdateSource(("PackageVersionVar", "2.0.0"));

            var engine = new MockEngine(_output);

            // Act
            var task = new UpgradeExternalDependencies {
                BuildEngine = engine,
                DependenciesFile = depsFilePath,
                UpdateSource = updateSource
            };

            // Assert
            Assert.True(task.Execute(), "Task is expected to pass.");
            var warning = Assert.Single(engine.Warnings, r => r.Message == "Setting 'PackageVersionVar' to '2.0.0'");

            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("2.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
        }

        [Fact]
        public void DoesNotModifyFileIfNoChanges()
        {
            // Arrange
            var depsFilePath = CreateProjectDepsFile(("PackageVersionVar", "1.0.0"));
            var updateSource = CreateUpdateSource(("PackageVersionVar", "1.0.0"));
            var created = File.GetLastWriteTime(depsFilePath);

            var engine = new MockEngine(_output);

            // Act
            var task = new UpgradeExternalDependencies {
                BuildEngine = engine,
                DependenciesFile = depsFilePath,
                UpdateSource = updateSource
            };

            // Assert
            Assert.True(task.Execute(), "Task is expected to pass");
            var modifiedDepsFile = DependencyVersionsFile.Load(depsFilePath);
            Assert.Equal("1.0.0", modifiedDepsFile.VersionVariables["PackageVersionVar"]);
            Assert.Equal(created, File.GetLastWriteTime(depsFilePath));
        }

        private string CreateUpdateSource(params (string varName, string version)[] variables)
        {
            var output = Path.Combine(_tempDir, "packageversion.props");

            var proj = ProjectRootElement.Create(NewProjectFileOptions.IncludeAllOptions);

            var variableGroup = proj.AddPropertyGroup();
            foreach (var variable in variables)
            {
                variableGroup.AddProperty(variable.varName, variable.version);
            }

            proj.Save(output);

            return output;
        }
    }
}
