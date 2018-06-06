// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    [Collection(nameof(MSBuildTestCollection))]
    public class MergeXmlFilesTests : TaskTestBase
    {
        public MergeXmlFilesTests(ITestOutputHelper output, MSBuildTestCollectionFixture fixture) : base(output, fixture)
        {
        }

        [Fact]
        public void ItMergesXmlFiles()
        {
            var items = new[]
            {
                new TaskItem(Path.Combine(TempDir, "file1.xml")),
                new TaskItem(Path.Combine(TempDir, "file2.xml")),
            };
            File.WriteAllText(items[0].ItemSpec, "<Doc Attr1='1'><a/></Doc>");
            File.WriteAllText(items[1].ItemSpec, "<Doc Attr2='2' Attr1='3'><b/></Doc>");

            var sb = new StringBuilder();

            var task = new MergeXmlFiles
            {
                BuildEngine = MockEngine,
                Files = items,
            };
            Assert.True(task.Execute(() => new StringWriter(sb)), "Task should pass");

            Assert.Equal("<Doc Attr1=\"3\" Attr2=\"2\"><a /><b /></Doc>", sb.ToString());
        }

        [Fact]
        public void ItFailsIfRootNodeIsDifferent()
        {
            var items = new[]
           {
                new TaskItem(Path.Combine(TempDir, "file3.xml")),
                new TaskItem(Path.Combine(TempDir, "file4.xml")),
            };
            File.WriteAllText(items[0].ItemSpec, "<Apple><a/></Apple>");
            File.WriteAllText(items[1].ItemSpec, "<Oranges><b/></Oranges>");

            var sb = new StringBuilder();

            var task = new MergeXmlFiles
            {
                BuildEngine = new MockEngine(Output) { ContinueOnError = true },
                Files = items,
            };
            Assert.False(task.Execute(() => new StringWriter(sb)), "Task should fail");
        }
    }
}
