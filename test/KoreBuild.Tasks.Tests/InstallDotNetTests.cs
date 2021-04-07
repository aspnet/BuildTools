// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class InstallDotNetTests
    {
        private readonly ITestOutputHelper _output;

        public InstallDotNetTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void InstallsDotnetCoreRuntime()
        {
            var path = Path.Combine(AppContext.BaseDirectory, ".installtest");
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            var request = new TaskItem("2.1.26", new Hashtable
            {
                ["Runtime"] = "dotnet",
                ["InstallDir"] = path
            });

            var script = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(AppContext.BaseDirectory, "dotnet-install.cmd")
                : Path.Combine(AppContext.BaseDirectory, "dotnet-install.sh");

            var task = new InstallDotNet
            {
                BuildEngine = new MockEngine(_output),
                Assets = new[] { request },
                InstallScript = script,
            };

            var expected = Path.Combine(path, "shared", "Microsoft.NETCore.App", "2.1.26", ".version");
            Assert.False(File.Exists(expected), "Test folder should have been deleted");

            Assert.True(task.Execute(), "Task should pass");

            Assert.True(File.Exists(expected), "Runtime should have been installed");
        }

        [Fact]
        public void FailsForBadInstall()
        {
            var path = Path.Combine(AppContext.BaseDirectory, ".installtest");
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            var request = new TaskItem("999.999.999", new Hashtable
            {
                ["Runtime"] = "dotnet",
                ["InstallDir"] = path
            });

            var script = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(AppContext.BaseDirectory, "dotnet-install.cmd")
                : Path.Combine(AppContext.BaseDirectory, "dotnet-install.sh");

            var task = new InstallDotNet
            {
                BuildEngine = new MockEngine(_output) { ContinueOnError = true },
                Assets = new[] { request },
                InstallScript = script,
            };

            Assert.False(task.Execute(), "Task should not have passed");
        }
    }
}
