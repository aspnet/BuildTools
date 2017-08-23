// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using KoreBuild.Tasks.ProjectModel;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class SolutionInfoFactoryTests : IDisposable
    {
        private readonly string _slnFile;

        public SolutionInfoFactoryTests()
        {
            _slnFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        [Theory]
        [InlineData("", new[] { "ClassLib1", "VsixProject" })]
        [InlineData("Debug", new[] { "ClassLib1", "VsixProject" })]
        [InlineData("DebugNoVSIX", new[] { "ClassLib1" })]
        public void FindsProjectsByDefaultConfiguration(string config, string[] projects)
        {
            File.WriteAllText(_slnFile, @"
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
		DebugNoVSIX|Any CPU = DebugNoVSIX|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{89EF0B05-98D4-4C4D-8870-718571091F79}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{86986537-8DF5-423F-A3A8-0CA568A9FFC4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{86986537-8DF5-423F-A3A8-0CA568A9FFC4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.DebugNoVSIX|Any CPU.ActiveCfg = Debug|Any CPU
		{89EF0B05-98D4-4C4D-8870-718571091F79}.DebugNoVSIX|Any CPU.Build.0 = Debug|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{89EF0B05-98D4-4C4D-8870-718571091F79} = {6BC8A037-601B-412E-B394-92F55C01C7A6}
		{86986537-8DF5-423F-A3A8-0CA568A9FFC4} = {6BC8A037-601B-412E-B394-92F55C01C7A6}
	EndGlobalSection
EndGlobal
".Replace('`', '"'));

            var solution = SolutionInfoFactory.Create(_slnFile, config);
            Assert.Equal(projects.Length, solution.Projects.Count);
            Assert.All(projects, expected => Assert.Contains(solution.Projects, proj => Path.GetFileNameWithoutExtension(proj) == expected));
        }

        [Fact]
        public void ThrowsForBadConfigName()
        {
            File.WriteAllText(_slnFile, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
".Replace('`', '"'));

            Assert.Throws<InvalidOperationException>(() => SolutionInfoFactory.Create(_slnFile, "Release"));
        }

        public void Dispose()
        {
            try
            {
                File.Delete(_slnFile);
            }
            catch { }
        }
    }
}
