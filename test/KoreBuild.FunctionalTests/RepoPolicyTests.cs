// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.FunctionalTests
{
    [Collection(nameof(RepoTestCollection))]
    public class RepoPolicyTests
    {
        private readonly ITestOutputHelper _output;
        private readonly RepoTestFixture _fixture;

        public RepoPolicyTests(ITestOutputHelper output, RepoTestFixture fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        [Fact]
        public async Task BuildsRepoWithLineupPolicy()
        {
            var app = _fixture.CreateTestApp("RepoWithLineupPolicy");

            var build = app.ExecuteBuild(_output);
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(5)));

            Assert.Same(task, build);
            Assert.Equal(0, build.Result);

            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "TestLineup.1.0.0.nupkg")), "Should have produced a lineup package");
        }

        [Theory]
        [InlineData("Debug", 0)]
        [InlineData("Release", 1)]
        public async Task RepoWithVersionRestrictions(string config, int exitCode)
        {
            var app = _fixture.CreateTestApp("RepoWithVersionRestrictionsPolicy");
            var logFile = Path.Combine(app.WorkingDirectory, "artifacts", "msbuild", "test.log");

            var build = app.ExecuteBuild(_output, "/p:Configuration=" + config, "/flp:LogFile=" + logFile, "/flp:Verbosity=Minimal");
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(5)));

            Assert.Same(task, build);
            Assert.Equal(exitCode, build.Result);

            var logText = File.ReadAllText(logFile);
            Assert.Contains("KRB4002", logText);
            Assert.Contains("Newtonsoft.Json/", logText);
            Assert.DoesNotContain("Moq/", logText);
        }
    }
}
