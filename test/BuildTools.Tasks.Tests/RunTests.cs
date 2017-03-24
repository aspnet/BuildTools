// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace BuildTools.Tasks.Tests
{
    public class RunTests : IDisposable
    {
        private readonly string _tempFile = Path.GetTempFileName();

        private static string _scriptExt =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "cmd"
                : "sh";
        private readonly ITestOutputHelper _output;

        public RunTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GetsExitCode()
        {
            string cmd;
            string filename;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filename = "cmd";
                cmd = "/c exit /b 43";
            }
            else
            {
                filename = "sh";
                cmd = "-c 'exit 43'";
            }

            var run = new Run
            {
                BuildEngine = new MockEngine(_output),
                IgnoreExitCode = true,
                Command = cmd,
                FileName = filename
            };

            Assert.True(run.Execute(), "Task should pass");
            Assert.Equal(43, run.ExitCode);
        }

        [Fact]
        public void CommandOrArgsButNotBoth()
        {
            var run = new RunDotNet
            {
                BuildEngine = new MockEngine(_output) { ThrowOnError = false },
                Command = "--version",
                Arguments = new[] { new TaskItem("--version") }
            };

            Assert.False(run.Execute(), "Task should have failed");
        }

        [Fact]
        public void RunDotNetCmd()
        {
            var runCmd = new RunDotNet
            {
                BuildEngine = new MockEngine(_output),
                Command = "--version",
            };
            Assert.True(runCmd.Execute(), "Task should succeed");
        }

        [Fact]
        public void RunDotNetArgs()
        {
            var runArgs = new RunDotNet
            {
                BuildEngine = new MockEngine(_output),
                Arguments = new[] { new TaskItem("--version") },
            };
            Assert.True(runArgs.Execute(), "Task should succeed");
        }

        [Fact]
        public void ItRetries()
        {
            var retries = 3;
            var run = new Run
            {
                FileName = "./scripts/fail-append." + _scriptExt,
                MaxRetries = retries,
                Arguments = new[] { new TaskItem(_tempFile) },
                BuildEngine = new MockEngine(_output) { ThrowOnError = false }
            };

            Assert.False(run.Execute(), "Task should fail");
            Assert.True(File.Exists(_tempFile), "File was not created");
            Assert.Equal(retries + 1, File.ReadAllLines(_tempFile).Length);
        }

        [Fact]
        public void Fails()
        {
            var run = new Run
            {
                FileName = "./scripts/fail-write." + _scriptExt,
                Arguments = new[] { new TaskItem(_tempFile) },
                BuildEngine = new MockEngine(_output) { ThrowOnError = false },
            };

            Assert.False(run.Execute(), "Task should fail");
            Assert.Equal(1, run.ExitCode);
            Assert.True(File.Exists(_tempFile), "File was not created");
            Assert.Equal("hello", File.ReadAllText(_tempFile).Trim());
        }


        [Fact]
        public void IgnoreExitCode()
        {
            var run = new Run
            {
                FileName = "./scripts/fail." + _scriptExt,
                IgnoreExitCode = true,
                BuildEngine = new MockEngine(_output),
            };

            Assert.True(run.Execute(), "Task should succeed");
        }

        [Theory]
        [InlineData("Abc=123", null, "Abc", "123")]
        [InlineData("Abc=123=xyz", null, "Abc", "123=xyz")]
        [InlineData("Abc", null, "Abc", "")]
        [InlineData("Abc", "123", "Abc", "123")]
        [InlineData("Abc=123", "xyz", "Abc", "123")]
        public void SplitsVariables(string itemSpec, string metadata, string name, string value)
        {
            var item = new TaskItem(itemSpec);
            item.SetMetadata("Value", metadata);

            var variables = Run.GetEnvVars(new[] { item });
            var var = Assert.Single(variables);
            Assert.Equal(name, var.Item1);
            Assert.Equal(value, var.Item2);
        }

        public void Dispose()
        {
            File.Delete(_tempFile);
        }
    }
}
