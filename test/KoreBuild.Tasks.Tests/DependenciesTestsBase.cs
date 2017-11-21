// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using System;
using System.IO;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class DependenciesTestsBase : IDisposable
    {
        protected readonly string _tempDir;
        protected readonly ITestOutputHelper _output;

        public DependenciesTestsBase(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            fixture.InitializeEnvironment(output);
            _tempDir = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _output = output;
        }

        protected string CreateProjectDepsFile(params (string varName, string version)[] variables)
        {
            var depsFilePath = Path.Combine(_tempDir, "projectdeps.props");
            var proj = ProjectRootElement.Create(NewProjectFileOptions.None);
            var originalDepsFile = DependencyVersionsFile.Load(proj);
            foreach (var item in variables)
            {
                originalDepsFile.Set(item.varName, item.version);
            }
            originalDepsFile.Save(depsFilePath);
            return depsFilePath;
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
