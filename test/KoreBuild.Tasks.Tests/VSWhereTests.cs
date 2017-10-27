// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Runtime.InteropServices;
using BuildTools.Tasks.Tests;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class VSWhereTests
    {
        private readonly ITestOutputHelper _output;

        public VSWhereTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItFindsVisualStudio()
        {
            var engine = new MockEngine(_output) { ContinueOnError = true };
            var task = new FindVisualStudio
            {
                BuildEngine = engine,
            };

            var result = task.Execute();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!result)
                {
                    // Don't fail the test. aspnet/BuildTools might be building without a version of VS installed.
                    // We mostly want to make sure this task doesn't throw exceptions and fails gracefully
                    Assert.NotEmpty(engine.Errors);
                }
                else
                {
                    Assert.NotEmpty(task.InstallationBasePath);
                    Assert.True(Directory.Exists(task.InstallationBasePath), "VS installation was not found");

                    // these output properties are only set if the file exists
                    if (!string.IsNullOrEmpty(task.MSBuildx64Path))
                    {
                        Assert.True(File.Exists(task.MSBuildx64Path), "MSBuild x64 was not found");
                    }

                    // these output properties are only set if the file exists
                    if (!string.IsNullOrEmpty(task.MSBuildx86Path))
                    {
                        Assert.True(File.Exists(task.MSBuildx86Path), "MSBuild x86 was not found");
                    }
                }
            }
            else
            {
                Assert.False(result, "VSWhere should fail on non-windows platforms");
            }
        }
    }
}
