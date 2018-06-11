// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using BuildTools.Tasks.Tests;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class InstallToolsetsTests : IDisposable
    {
        private readonly string _configFile;

        public InstallToolsetsTests()
        {
            _configFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        public void Dispose()
        {
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
        }

        [Fact]
        public void FailsIfVsIsRequiredOnNonWindows()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""visualstudio"": {
      ""required"": [""macos"", ""linux""]
    },
  }
}");
            var task = new InstallToolsets
            {
                BuildEngine = new MockEngine(),
                ConfigFile = _configFile,
            };

            Assert.False(task.Execute(), "Task is expected to fail");
        }
    }
}
