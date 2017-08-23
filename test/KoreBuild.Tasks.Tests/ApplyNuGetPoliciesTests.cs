// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using KoreBuild.Tasks.Policies;
using KoreBuild.Tasks.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class ApplyNuGetPoliciesTests : IDisposable
    {
        private readonly string _tmpDir;
        private readonly ITestOutputHelper _output;

        public ApplyNuGetPoliciesTests(ITestOutputHelper output)
        {
            _output = output;
            _tmpDir = Path.Combine(Path.GetTempPath(), "korebuild", Path.GetRandomFileName());
            Directory.CreateDirectory(_tmpDir);
        }

        [Fact]
        public void CreatesPolicies()
        {
            var task = new ApplyNuGetPolicies
            {
                Policies = new ITaskItem[]
                {
                    new TaskItem("Example.Lineup", new Hashtable { ["PolicyType"] = "Lineup" }),
                    new TaskItem("Another.Lineup", new Hashtable { ["PolicyType"] = "Lineup" }),
                    new TaskItem("Release", new Hashtable { ["PolicyType"] = "DisallowPackageReferenceVersion" }),
                    new TaskItem("Debug", new Hashtable { ["PolicyType"] = "DisallowPackageReferenceVersion" }),
                    new TaskItem("https://api.nuget.org/v3/index.json", new Hashtable { ["PolicyType"] = "AdditionalRestoreSource" }),
                    new TaskItem("https://anotherfeed/v3/index.json", new Hashtable { ["PolicyType"] = "AdditionalRestoreSource" }),
                },
                BuildEngine = new MockEngine()
            };

            var policies = task.CreatePolicies();

            var lineup = Assert.Single(policies, p => p is PackageLineupPolicy);
            var sources = Assert.Single(policies, p => p is AdditionalProjectRestoreSourcePolicy);
            var restriction = Assert.Single(policies, p => p is PackageVersionRestrictionPolicy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("random")]
        public void FailsForPoliciesWithUnknownType(string policyType)
        {
            var items = new ITaskItem[]
            {
                new TaskItem("Hello", new Hashtable { ["PolicyType"] = policyType })
            };

            var engine = new MockEngine
            {
                ContinueOnError = true
            };

            var task = new ApplyNuGetPolicies
            {
                BuildEngine = engine,
                Policies = items,
            };

            Assert.False(task.Execute(), "Expected the task to fail");

            var error = Assert.Single(engine.Errors);
            Assert.Equal("KRB" + KoreBuildErrors.UnknownPolicyType, error.Code);
        }

        [Fact]
        public void ExecutesDesignTimeBuild()
        {
            var sln = Path.Combine(_tmpDir, "Test.sln");
            File.WriteAllText(sln, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Project(`{2150E333-8FDC-42A3-9474-1A3956D46DE8}`) = `src`, `src`, `{6BC8A037-601B-412E-B394-92F55C01C7A6}`
EndProject
Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `ClassLib1`, `src\ClassLib1\ClassLib1.csproj`, `{89EF0B05-98D4-4C4D-8870-718571091F79}`
EndProject
Project(`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) = `VsixProject`, `src\VsixProject\VsixProject.csproj`, `{86986537-8DF5-423F-A3A8-0CA568A9FFC4}`
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		ReleaseNoVSIX|Any CPU = ReleaseNoVSIX|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{89EF0B05-98D4-4C4D-8870-718571091F79}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{86986537-8DF5-423F-A3A8-0CA568A9FFC4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{86986537-8DF5-423F-A3A8-0CA568A9FFC4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.ReleaseNoVSIX|Any CPU.ActiveCfg = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.ReleaseNoVSIX|Any CPU.Build.0 = Debug|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{89EF0B05-98D4-4C4D-8870-718571091F79} = {6BC8A037-601B-412E-B394-92F55C01C7A6}
	EndGlobalSection
EndGlobal
".Replace('`', '"'));

            var projectPath = Path.Combine(_tmpDir, "src", "ClassLib1", "ClassLib1.csproj").Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, @"<Project Sdk=`Microsoft.NET.Sdk`>
    <PropertyGroup>
        <TargetFrameworks>netstandard2.0;net461</TargetFrameworks>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include=`Abc` Version=`1.1.0` NoWarn=`KRB4002` />
        <PackageReference Include=`Xyz` />
    </ItemGroup>
</Project>".Replace('`', '"'));

            var items = new[]
            {
                new TaskItem(sln, new Hashtable { ["AdditionalProperties"] = "Configuration=ReleaseNoVSIX" }),
            };

            MSBuildEnvironmentHelper.InitializeEnvironment(_output);
            var task = new ApplyNuGetPolicies
            {
                ProjectProperties = "BuildNumber=123;Configuration=Release",
                Projects = items,
                BuildEngine = new MockEngine(),
            };

            var projects = task.CreateProjectContext();
            Assert.NotNull(projects);
            var project = Assert.Single(projects);
            Assert.Equal(projectPath, project.FullPath);

            void AssertAbc(ProjectFrameworkInfo tfm)
            {
                Assert.True(tfm.Dependencies.TryGetValue("Abc", out var abc));
                Assert.Contains("KRB4002", abc.NoWarn);
                Assert.Equal("1.1.0", abc.Version);
            }

            void AssertXyz(ProjectFrameworkInfo tfm)
            {
                Assert.True(tfm.Dependencies.TryGetValue("Xyz", out var xyz));
                Assert.Empty(xyz.NoWarn);
                Assert.Empty(xyz.Version);
            }

            Assert.Collection(project.Frameworks,
                tfm =>
                {
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, tfm.TargetFramework);

                    Assert.Equal(3, tfm.Dependencies.Count);

                    AssertAbc(tfm);
                    AssertXyz(tfm);

                    Assert.True(tfm.Dependencies.TryGetValue("NETStandard.Library", out var nslibrary));
                    Assert.True(nslibrary.IsImplicitlyDefined, "NETStandard.Library should be implicitly defined");
                },
                tfm =>
                {
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net461, tfm.TargetFramework);

                    Assert.Equal(2, tfm.Dependencies.Count);

                    AssertAbc(tfm);
                    AssertXyz(tfm);
                });
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tmpDir, recursive: true);
            }
            catch { }
        }
    }
}
