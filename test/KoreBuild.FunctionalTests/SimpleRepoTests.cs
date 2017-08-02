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
    public class SimpleRepoTests
    {
        private readonly ITestOutputHelper _output;
        private readonly RepoTestFixture _fixture;

        public SimpleRepoTests(ITestOutputHelper output, RepoTestFixture fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        [Fact]
        public async Task FullBuildCompletes()
        {
            var app = _fixture.CreateTestApp("SimpleRepo");

            var build = app.ExecuteBuild(_output);
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(5)));

            Assert.Same(task, build);
            Assert.Equal(0, build.Result);

            // bootstrapper
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "korebuild-lock.txt")), "Should have created the korebuild lock file");

            // /t:Package
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Lib.1.0.0-beta-t000.nupkg")), "Build should have produced a lib nupkg");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Sources.1.0.0-beta-t000.nupkg")), "Build should have produced a sources nupkg");

            // /t:TestNuGetPush
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "obj", "tmp-nuget", "Simple.Lib.1.0.0-beta-t000.nupkg")), "Build done a test push of all the packages");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "obj", "tmp-nuget", "Simple.Sources.1.0.0-beta-t000.nupkg")), "Build done a test push of all the packages");
        }
    }
}
