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

            var build = app.ExecuteBuild(_output, "/p:BuildNumber=0001");
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(5)));

            Assert.Same(task, build);
            Assert.Equal(0, build.Result);

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

            // /t:TestNuGetPush
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "obj", "tmp-nuget", "Simple.CliTool.1.0.0-beta-0001.nupkg")), "Build done a test push of all the packages");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "obj", "tmp-nuget", "Simple.Lib.1.0.0-beta-0001.nupkg")), "Build done a test push of all the packages");
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "obj", "tmp-nuget", "Simple.Sources.1.0.0-beta-0001.nupkg")), "Build done a test push of all the packages");

            // /t:GenerateSigningRequest
            var signRequest = Path.Combine(app.WorkingDirectory, "artifacts", "signrequest.xml");
            Assert.True(File.Exists(signRequest), "Sign requests should have been generated");

            var sign = XDocument.Load(signRequest);
            var excluded = Assert.Single(sign.Descendants("ExcludedFile"));
            Assert.Equal("build/Simple.Sources.1.0.0-beta-0001.nupkg", excluded.Attribute("Path")?.Value);
            Assert.Collection(sign.Descendants("Container"),
                pkg =>
                {
                    Assert.Equal("build/Simple.CliTool.1.0.0-beta-0001.nupkg", pkg.Attribute("Path")?.Value);
                    Assert.Null(pkg.Attribute("Certificate"));
                    Assert.Equal("nupkg", pkg.Attribute("Type")?.Value);
                    Assert.Collection(pkg.Descendants("File"),
                        a =>
                        {
                            Assert.Equal("tools/any/any/Newtonsoft.Json.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("Test3rdPartyCert", a.Attribute("Certificate")?.Value);
                        },
                        a =>
                        {
                            Assert.Equal("tools/any/any/cowsay.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("TestCert", a.Attribute("Certificate")?.Value);
                        });
                },
                pkg =>
                {
                    Assert.Equal("build/Simple.Lib.1.0.0-beta-0001.nupkg", pkg.Attribute("Path")?.Value);
                    Assert.Null(pkg.Attribute("Certificate"));
                    Assert.Equal("nupkg", pkg.Attribute("Type")?.Value);
                    Assert.Collection(pkg.Descendants("File"),
                        a =>
                        {
                            Assert.Equal("lib/net461/Simple.Lib.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("TestCert", a.Attribute("Certificate")?.Value);
                        },
                        a =>
                        {
                            Assert.Equal("lib/netstandard2.0/Simple.Lib.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("TestCert", a.Attribute("Certificate")?.Value);
                        });
                },
                pkg =>
                {
                    Assert.Equal("build/Simple.Lib.1.0.0-beta-0001.symbols.nupkg", pkg.Attribute("Path")?.Value);
                    Assert.Null(pkg.Attribute("Certificate"));
                    Assert.Equal("nupkg", pkg.Attribute("Type")?.Value);
                    Assert.Equal("nupkg", pkg.Attribute("Type")?.Value);
                    Assert.Collection(pkg.Descendants("File"),
                        a =>
                        {
                            Assert.Equal("lib/net461/Simple.Lib.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("TestCert", a.Attribute("Certificate")?.Value);
                        },
                        a =>
                        {
                            Assert.Equal("lib/netstandard2.0/Simple.Lib.dll", a.Attribute("Path")?.Value);
                            Assert.Equal("TestCert", a.Attribute("Certificate")?.Value);
                        });
                });
        }

        [Fact]
        public async Task BuildShouldReturnNonZeroCode()
        {
            var app = _fixture.CreateTestApp("RepoThatShouldFailToBuild");

            var build = app.ExecuteBuild(_output);
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(5)));

            Assert.Same(task, build);
            Assert.NotEqual(0, build.Result);
        }

        [DockerExistsFact(Skip = "winservercore currently fails on AppVeyor due to breaking changes in winservercore 1710")]
        public async Task DockerSuccessful()
        {
            var app = _fixture.CreateTestApp("SimpleRepo");
            var platform = "jessie";

            var dockerPlatform = GetDockerPlatform();
            if (dockerPlatform == OSPlatform.Windows)
            {
                platform = "winservercore";
            }

            var build = app.ExecuteRun(_output, new string[] { "docker-build", "-Path", app.WorkingDirectory }, platform, "/p:BuildNumber=0001");
            var task = await Task.WhenAny(build, Task.Delay(TimeSpan.FromMinutes(10)));

            Assert.Same(task, build);

            Assert.Equal(0, build.Result);
        }

        private static OSPlatform GetDockerPlatform()
        {
            var startInfo = new ProcessStartInfo("docker", @"version -f ""{{ .Server.Os }}""")
            {
                RedirectStandardOutput = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd().Trim();

                OSPlatform result;
                switch (output)
                {
                    case "windows":
                        result = OSPlatform.Windows;
                        break;
                    case "linux":
                        result = OSPlatform.Linux;
                        break;
                    default:
                        throw new NotImplementedException($"No default for docker platform {output}");
                }

                return result;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DockerExistsFactAttribute : FactAttribute
    {
        public DockerExistsFactAttribute()
        {
            if (!HasDocker())
            {
                Skip = "Docker must be installed to run this test.";
            }
        }

        private static bool HasDocker()
        {
            try
            {
                var startInfo = new ProcessStartInfo("docker", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process.Start(startInfo))
                {
                    return true;
                }
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }
}
