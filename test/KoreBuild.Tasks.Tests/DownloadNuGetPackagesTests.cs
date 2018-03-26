// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace KoreBuild.Tasks.Tests
{
    public class DownloadNuGetPackagesTest
    {
        private readonly ITestOutputHelper _output;

        public DownloadNuGetPackagesTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ItDownloadPackages()
        {
            var packages = new[]
            {
                new TaskItem("Newtonsoft.Json", new Hashtable
                {
                    ["Version"] = "9.0.1",
                    ["Source"] = "  https://api.nuget.org/v3/index.json  ; ;https://api.nuget.org/v3/index.json; https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json "
                }),
            };

            var task = new DownloadNuGetPackages
            {
                Packages = packages,
                DestinationFolder = AppContext.BaseDirectory,
                BuildEngine = new MockEngine(_output),
                TimeoutSeconds = 120,
            };
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "newtonsoft.json.9.0.1.nupkg").Replace('\\', '/');
            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }

            Assert.False(File.Exists(expectedPath), "The file should not exist yet");
            Assert.True(await task.ExecuteAsync(), "Task should pass");
            var file = Assert.Single(task.Files);
            Assert.Equal(expectedPath, file.ItemSpec.Replace('\\', '/'));
            Assert.True(File.Exists(expectedPath), "The file should exist");
        }

        [Fact]
        public async Task ItFailsForPackagesThatDoNotExist()
        {
            var packages = new[]
            {
                new TaskItem("SomePackage", new Hashtable { ["Version"] = "1.0.0", ["Source"] = AppContext.BaseDirectory }),
            };

            var engine = new MockEngine(_output) { ContinueOnError = true };
            var task = new DownloadNuGetPackages
            {
                Packages = packages,
                DestinationFolder = AppContext.BaseDirectory,
                BuildEngine = engine,
                TimeoutSeconds = 120,
            };

            Assert.False(await task.ExecuteAsync(), "Task should fail");
            Assert.NotEmpty(engine.Errors);
            Assert.Contains(engine.Errors, m => m.Message.Contains("SomePackage 1.0.0 is not available"));
        }

        [Fact]
        public async Task ItFindsPackageWhenMultipleFeedsAreSpecified()
        {
            var packages = new[]
            {
                new TaskItem("Newtonsoft.Json", new Hashtable { ["Version"] = "9.0.1", ["Source"] = $"{AppContext.BaseDirectory};https://api.nuget.org/v3/index.json"} ),
            };

            var task = new DownloadNuGetPackages
            {
                Packages = packages,
                DestinationFolder = AppContext.BaseDirectory,
                BuildEngine = new MockEngine(_output),
                TimeoutSeconds = 120,
            };
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "newtonsoft.json.9.0.1.nupkg").Replace('\\', '/');
            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }

            Assert.False(File.Exists(expectedPath), "The file should not exist yet");
            Assert.True(await task.ExecuteAsync(), "Task should pass");
            var file = Assert.Single(task.Files);
            Assert.Equal(expectedPath, file.ItemSpec.Replace('\\', '/'));
            Assert.True(File.Exists(expectedPath), "The file should exist");
        }
    }
}
