// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using BuildTools.Tasks.Tests;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace KoreBuild.Tasks.Tests
{
    public class DownloadFileTests
    {
        private readonly ITestOutputHelper _output;

        public DownloadFileTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ItDownloadAFile()
        {
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "microsoft.com.2.html");
            var task = new DownloadFile
            {
                Uri = "http://example.org/index.html",
                DestinationPath = expectedPath,
                BuildEngine = new MockEngine(_output),
            };

            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }

            Assert.True(await task.ExecuteAsync(), "Task should pass");
            Assert.True(File.Exists(expectedPath), "The file should exist");
        }

        [Fact]
        public async Task ItDoesNotRedownloadDownloadAFileThatExists()
        {
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "microsoft.com.1.html");
            var task = new DownloadFile
            {
                Uri = "http://example.org/index.html",
                DestinationPath = expectedPath,
                BuildEngine = new MockEngine(_output),
            };

            const string placeholder = "Dummy content";
            File.WriteAllText(expectedPath, placeholder);

            Assert.True(await task.ExecuteAsync(), "Task should pass");

            Assert.Equal(placeholder, File.ReadAllText(expectedPath));

            task.Overwrite = true;

            Assert.True(await task.ExecuteAsync(), "Task should pass");
            Assert.NotEqual(placeholder, File.ReadAllText(expectedPath));
        }

        [Fact]
        public async Task ItFailsForFilesThatDoNotExist()
        {
            var engine = new MockEngine(_output) { ContinueOnError = true };
            var task = new DownloadFile
            {
                Uri = "http://localhost/this/file/does/not/exist",
                DestinationPath = Path.Combine(AppContext.BaseDirectory, "dummy.txt"),
                BuildEngine = engine,
            };

            Assert.False(await task.ExecuteAsync(), "Task should fail");
            Assert.NotEmpty(engine.Errors);
        }
    }
}
