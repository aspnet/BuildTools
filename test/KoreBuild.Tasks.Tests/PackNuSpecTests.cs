// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class PackNuSpecTests : IDisposable
    {
        private readonly string _tempDir;

        public PackNuSpecTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void CreatesPackage()
        {
            var outputPath = Path.Combine(_tempDir, $"TestPackage.1.0.0.nupkg");
            Directory.CreateDirectory(Path.Combine(_tempDir, "tools"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "lib", "netstandard2.0"));
            File.WriteAllText(Path.Combine(_tempDir, "tools", "test.sh"), "");
            File.WriteAllText(Path.Combine(_tempDir, "lib", "netstandard2.0", "TestPackage.dll"), "");

            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>TestPackage</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                  <files>
                    <file src=`tools\*` target=`tools/` />
                    <file src=`lib/netstandard2.0/TestPackage.dll` target=`lib/netstandard2.0/` />
                  </files>
                </package>
                ");

            var task = new PackNuSpec
            {
                NuspecPath = nuspec,
                BasePath = _tempDir,
                BuildEngine = new MockEngine(),
                DestinationFolder = _tempDir,
            };

            Assert.True(task.Execute(), "The task should have passed");
            Assert.True(File.Exists(outputPath), "Should have produced a nupkg file in " + _tempDir);
            var result = Assert.Single(task.Packages);
            Assert.Equal(outputPath, result.ItemSpec);
            using (var reader = new PackageArchiveReader(outputPath))
            {
                var libItems = reader.GetLibItems().ToList();
                var libItem = Assert.Single(libItems);
                Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItem.TargetFramework);
                var assembly = Assert.Single(libItem.Items);
                Assert.Equal("lib/netstandard2.0/TestPackage.dll", assembly);

                Assert.Contains(reader.GetFiles(), f => f == "tools/test.sh");
            }
        }

        [Fact]
        public void AppliesProperties()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasProperties</id>
                    <version>$version$</version>
                    <authors>Microsoft</authors>
                    <description>$description$</description>
                    <copyright>$copyright$</copyright>
                  </metadata>
                </package>
                ");

            var version = "1.2.3";
            var description = "A test package\n\n\nwith newlines";
            var outputPath = Path.Combine(_tempDir, $"HasProperties.{version}.nupkg");
            var task = new PackNuSpec
            {
                NuspecPath = nuspec,
                BasePath = _tempDir,
                BuildEngine = new MockEngine(),
                DestinationFolder = _tempDir,
                Properties = $"version={version};;description={description};copyright=;;;;",
            };

            Assert.True(task.Execute(), "The task should have passed");
            Assert.True(File.Exists(outputPath), "Should have produced a nupkg file in " + _tempDir);

            using (var reader = new PackageArchiveReader(outputPath))
            {
                var metadata = new PackageBuilder(reader.GetNuspec(), basePath: null);
                Assert.Equal(version, metadata.Version.ToString());
                Assert.Empty(metadata.Copyright);
                Assert.Equal(description, metadata.Description);
            }
        }

        [Fact]
        public void AddsDependencies()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasDependencies</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                    <dependencies>
                        <dependency id=`AlreadyInNuspec` version=`[2.0.0]` />
                    </dependencies>
                  </metadata>
                </package>
                ");

            var task = new PackNuSpec
            {
                NuspecPath = nuspec,
                BasePath = _tempDir,
                BuildEngine = new MockEngine(),
                DestinationFolder = _tempDir,
                Dependencies = new[]
                {
                    new TaskItem("OtherPackage", new Hashtable { ["Version"] = "[1.0.0, 2.0.0)"}),
                    new TaskItem("PackageInTfm", new Hashtable { ["TargetFramework"] = "netstandard1.0", ["Version"] = "0.1.0-beta" }),
                }
            };

            Assert.True(task.Execute(), "The task should have passed");
            var result = Assert.Single(task.Packages);

            using (var reader = new PackageArchiveReader(result.ItemSpec))
            {
                var metadata = new PackageBuilder(reader.GetNuspec(), basePath: null);

                var noTfmGroup = Assert.Single(metadata.DependencyGroups, d => d.TargetFramework.Equals(NuGetFramework.UnsupportedFramework));
                Assert.Equal(2, noTfmGroup.Packages.Count());
                Assert.Single(noTfmGroup.Packages, p => p.Id == "OtherPackage" && p.VersionRange.Equals(VersionRange.Parse("[1.0.0, 2.0.0)")));
                Assert.Single(noTfmGroup.Packages, p => p.Id == "AlreadyInNuspec" && p.VersionRange.Equals(VersionRange.Parse("[2.0.0]")));

                var netstandard10Group = Assert.Single(metadata.DependencyGroups, d => d.TargetFramework.Equals(FrameworkConstants.CommonFrameworks.NetStandard10));
                var package = Assert.Single(netstandard10Group.Packages);
                Assert.Equal("PackageInTfm", package.Id);
                Assert.Equal(VersionRange.Parse("0.1.0-beta"), package.VersionRange);
            }
        }

        private string CreateNuspec(string xml)
        {
            var nuspecPath = Path.Combine(_tempDir, Path.GetRandomFileName() + ".nuspec");
            File.WriteAllText(nuspecPath, xml.Replace('`', '"').TrimStart());
            return nuspecPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                Console.WriteLine("Failed to delete " + _tempDir);
            }
        }
    }
}
