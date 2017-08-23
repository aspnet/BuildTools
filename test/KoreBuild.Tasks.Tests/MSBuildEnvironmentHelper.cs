// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    internal class MSBuildEnvironmentHelper
    {
        public static void InitializeEnvironment(ITestOutputHelper output)
        {
            var dotnet = Process.GetCurrentProcess().MainModule.FileName;
            var dotnetDir = Path.GetDirectoryName(dotnet);
            output.WriteLine($"dotnet = {dotnetDir}");
            string version = null;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var globalJson = dir.GetFiles("global.json").FirstOrDefault();
                if (globalJson != null)
                {
                    var settings = JsonConvert.DeserializeAnonymousType(File.ReadAllText(globalJson.FullName), new { sdk = new { version = "" } });
                    version = settings.sdk.version;
                    output.WriteLine($"global.json found in '{globalJson.FullName}' with SDK version '{version}'");
                    break;
                }
                dir = dir.Parent;
            }

            string sdkDir;
            if (string.IsNullOrEmpty(version))
            {
                var sdkDirs = new DirectoryInfo(Path.Combine(dotnetDir, "sdk"));
                var sdks = sdkDirs.GetDirectories().First();
                sdkDir = sdks.FullName;
            }
            else
            {
                sdkDir = Path.Combine(dotnetDir, "sdk", version);
            }

            output.WriteLine($"Setting MSBuild directory to {sdkDir}");
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdkDir, "MSBuild.dll"));
            Environment.SetEnvironmentVariable("MSBUILDEXTENSIONSPATH", sdkDir);
        }
    }
}
