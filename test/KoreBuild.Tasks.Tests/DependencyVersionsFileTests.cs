// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using BuildTools.Tasks.Tests;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class DependencyVersionsFileTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly ITestOutputHelper _output;

        public DependencyVersionsFileTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            _output = output;
            fixture.InitializeEnvironment(output);
            _tempFile = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        [Fact]
        public void ItSortsVariablesAlphabetically()
        {
            var depsFile = DependencyVersionsFile.Create(addOverrideImport: true);
            depsFile.Update("XyzPackageVersion", "123");
            depsFile.Update("AbcPackageVersion", "456");
            depsFile.Save(_tempFile);

            var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            var versions = Assert.Single(project.PropertyGroups, p => p.Label == DependencyVersionsFile.AutoPackageVersionsLabel);
            Assert.Collection(versions.Properties,
                v => Assert.Equal("AbcPackageVersion", v.Name),
                v => Assert.Equal("XyzPackageVersion", v.Name));
        }

        [Fact]
        public void SetIsCaseInsensitive()
        {
            var depsFile = DependencyVersionsFile.Create(addOverrideImport: true);
            depsFile.Update("XunitRunnerVisualStudioVersion", "2.3.0");
            depsFile.Update("XunitRunnerVisualstudioVersion", "2.4.0");
            depsFile.Save(_tempFile);

            var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            var versions = Assert.Single(project.PropertyGroups, p => p.Label == DependencyVersionsFile.AutoPackageVersionsLabel);
            var prop = Assert.Single(versions.Properties);
            Assert.Equal("XunitRunnerVisualStudioVersion", prop.Name);
            Assert.Equal("2.4.0", prop.Value);
        }

        [Theory]
        [InlineData("Microsoft.Data.Sqlite", "MicrosoftDataSqlitePackageVersion")]
        [InlineData("SQLitePCLRaw.bundle_green", "SQLitePCLRawBundleGreenPackageVersion")]
        [InlineData("runtime.win-x64.Microsoft.NETCore", "RuntimeWinX64MicrosoftNETCorePackageVersion")]
        public void GeneratesVariableName(string id, string varName)
        {
            Assert.Equal(varName, DependencyVersionsFile.GetVariableName(id));
        }

        [Fact]
        public void AdditionalImportsAreAdded_WithOverrideImportFalse()
        {
            // Arrange
            var path = "obj/test.props";
            var depsFile = DependencyVersionsFile.Create(addOverrideImport: false, additionalImports: new[] { path });
            depsFile.Save(_tempFile);

            // Act
             var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            // Assert
            var import = Assert.Single(project.Imports);
            Assert.Equal(path, import.Project);
        }

        [Fact]
        public void AdditionalImportsAreAdded_WithOverrideImportTrue()
        {
            // Arrange
            var path = "obj/external.props";
            var depsFile = DependencyVersionsFile.Create(addOverrideImport: true, additionalImports: new[] { path });
            depsFile.Save(_tempFile);

            // Act
             var project = ProjectRootElement.Open(_tempFile);
            _output.WriteLine(File.ReadAllText(_tempFile));

            // Assert
            Assert.Collection(
                project.Imports,
                import => Assert.Equal(path, import.Project),
                import => Assert.Equal("$(DotNetPackageVersionPropsPath)", import.Project));
        }
    }
}
