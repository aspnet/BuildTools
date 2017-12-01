// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class GenerateBillOfMaterialsTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDir;

        public GenerateBillOfMaterialsTests(ITestOutputHelper output)
        {
            _output = output;
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void ItSerializesItemsToXml()
        {
            var items = new ITaskItem[]
            {
                new TaskItem("Test.txt", new Hashtable{ ["ArtifactType"] = "TextFile", ["Category"] = "ship", ["Dependencies"] ="a; b ;;c" }),
            };

            var sb = new StringBuilder();
            var writer = new StringWriter(sb);

            var task = new GenerateBillOfMaterials
            {
                Artifacts = items,
                BuildEngine = new MockEngine(_output),
            };

            Assert.True(task.Execute(() => writer), "Task should pass");

            var expected = $@"<Build>
  <Artifacts Category=`ship`>
    <Artifact Id=`Test.txt` Type=`TextFile` />
  </Artifacts>
  <Dependencies>
    <Link Source=`Test.txt` Target=`a` />
    <Link Source=`Test.txt` Target=`b` />
    <Link Source=`Test.txt` Target=`c` />
  </Dependencies>
</Build>".Replace('`', '"');

            _output.WriteLine(sb.ToString());
            Assert.Equal(expected, sb.ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }

        [Fact]
        public void ItReadsNupkgAndAddsDependencies()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>TestPackage</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                    <dependencies>
                      <dependency id=`Newtonsoft.Json` version=`[9.0.1, )` />
                      <!-- Dependencies without a lower bound should be ignored -->
                      <dependency id=`Other` version=`(, 10.0.1]` />
                      <dependency id=`Other2` version=`(, )` />
                    </dependencies>
                  </metadata>
                </package>
                ");

            var pack = new PackNuSpec
            {
                NuspecPath = nuspec,
                BasePath = _tempDir,
                BuildEngine = new MockEngine(),
                DestinationFolder = _tempDir,
            };

            pack.Execute();

            var items = new ITaskItem[]
            {
                new TaskItem(pack.Packages.Single().ItemSpec, new Hashtable{ ["ArtifactType"] = "NuGetPackage" }),
            };

            var sb = new StringBuilder();
            var writer = new StringWriter(sb);

            var task = new GenerateBillOfMaterials
            {
                Artifacts = items,
                BuildEngine = new MockEngine(_output),
            };

            Assert.True(task.Execute(() => writer), "Task should pass");

            var expected = $@"<Build>
  <Artifacts>
    <Artifact Id=`TestPackage.1.0.0.nupkg` Type=`NuGetPackage` />
  </Artifacts>
  <Dependencies>
    <Link Source=`TestPackage.1.0.0.nupkg` Target=`Newtonsoft.Json.9.0.1.nupkg` />
  </Dependencies>
</Build>".Replace('`', '"');

            _output.WriteLine(sb.ToString());
            Assert.Equal(expected, sb.ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
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
