// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
        public void FullBuildCompletes()
        {
            var app = _fixture.CreateTestApp("SimpleRepo");

            Assert.Equal(0, app.ExecuteBuild(_output, "/p:BuildNumber=0001"));

            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "korebuild-lock.txt")), "Should have created the korebuild lock file");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Lib.1.0.0-beta-0001.nupkg")), "Build should have produced a lib nupkg");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Sources.1.0.0-beta-0001.nupkg")), "Build should have produced a sources nupkg");
        }
    }
}
