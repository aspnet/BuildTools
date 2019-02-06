// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.CommandLineUtils;

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

            var build = app.ExecuteBuild(_output, "/p:BuildNumber=0001", "/p:DisableCodeSigning=true");

            Assert.Equal(0, build);

            // bootstrapper
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "korebuild-lock.txt")), "Should have created the korebuild lock file");

            // /t:Package
            var libPackage = Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Lib.1.0.0-beta-0001.nupkg");
            var libSymbolsPackage = Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Lib.1.0.0-beta-0001.symbols.nupkg");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.CliTool.1.0.0-beta-0001.nupkg")), "Build should have produced a lib nupkg");
            Assert.True(File.Exists(libPackage), "Build should have produced a lib nupkg");
            Assert.True(File.Exists(libSymbolsPackage), "Build should have produced a symbols lib nupkg");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "artifacts", "build", "Simple.Sources.1.0.0-beta-0001.nupkg")), "Build should have produced a sources nupkg");

            using (var reader = new PackageArchiveReader(libPackage))
            {
                Assert.Empty(reader.GetFiles().Where(p => Path.GetExtension(p).Equals(".pdb", StringComparison.OrdinalIgnoreCase)));
            }
        }

        [Fact(Skip = "Blocked on https://github.com/dotnet/sdk/issues/2867")]
        public void BuildOfGlobalCliToolIncludesShims()
        {
            var app = _fixture.CreateTestApp("RepoWithGlobalTool");

            var build = app.ExecuteBuild(_output, "/p:BuildNumber=0001");

            Assert.Equal(0, build);

            var artifactsDir = Path.Combine(app.WorkingDirectory, "artifacts", "build");

            var pkg = Path.Combine(artifactsDir, "GlobalConsoleTool.1.0.0.nupkg");
            using (var reader = new PackageArchiveReader(pkg))
            {
                var files = reader.GetFiles();
                foreach (var file in files)
                {
                    _output.WriteLine("pkg: " + file);
                }

                var winx86 = Assert.Single(files, f => f.StartsWith("tools/netcoreapp3.0/any/shims/win-x86/"));
                Assert.Equal("GlobalConsoleTool.exe", Path.GetFileName(winx86));

                var winx64 = Assert.Single(files, f => f.StartsWith("tools/netcoreapp3.0/any/shims/win-x64/"));
                Assert.Equal("GlobalConsoleTool.exe", Path.GetFileName(winx64));
            }

            var toolsDir = Path.Combine(app.WorkingDirectory, "artifacts", "tools");
            var installPsi = new ProcessStartInfo
            {
                FileName = DotNetMuxer.MuxerPathOrDefault(),
                Arguments = ArgumentEscaper.EscapeAndConcatenate(new[]
                {
                    "tool",
                    "install",
                    "--tool-path", toolsDir,
                    "GlobalConsoleTool",
                    "--add-source", artifactsDir
                }),
            };
            var install = app.Run(_output, installPsi);
            Assert.Equal(0, install);

            var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ".exe"
                : string.Empty;
            var run = app.Run(_output, new ProcessStartInfo
            {
                FileName = Path.Combine(toolsDir, "GlobalConsoleTool" + ext),
            });
            Assert.Equal(0, run);
        }

        [Fact]
        public void BuildShouldReturnNonZeroCode()
        {
            var app = _fixture.CreateTestApp("RepoThatShouldFailToBuild");

            var build = app.ExecuteBuild(_output);

            Assert.NotEqual(0, build);
        }
    }
}
