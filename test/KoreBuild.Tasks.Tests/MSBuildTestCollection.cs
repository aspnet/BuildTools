// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    /// <summary>
    /// Use this for any test that invokes Microsoft.Build.Construction APIs
    /// </summary>
    [CollectionDefinition(nameof(MSBuildTestCollection))]
    public class MSBuildTestCollection : ICollectionFixture<MSBuildTestCollectionFixture>
    { }

    public class MSBuildTestCollectionFixture
    {
        private readonly string _dotnetDir;
        private readonly string _sdkDir;

        public MSBuildTestCollectionFixture()
        {
            var dotnet = Process.GetCurrentProcess().MainModule.FileName;
            _dotnetDir = Path.GetDirectoryName(dotnet);
            string version = null;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var globalJson = dir.GetFiles("global.json").FirstOrDefault();
                if (globalJson != null)
                {
                    var settings = JsonConvert.DeserializeAnonymousType(File.ReadAllText(globalJson.FullName), new { sdk = new { version = "" } });
                    version = settings.sdk.version;
                    break;
                }
                dir = dir.Parent;
            }

            if (string.IsNullOrEmpty(version))
            {
                var sdkDirs = new DirectoryInfo(Path.Combine(_dotnetDir, "sdk"));
                var sdks = sdkDirs.GetDirectories().First();
                _sdkDir = sdks.FullName;
            }
            else
            {
                _sdkDir = Path.Combine(_dotnetDir, "sdk", version);
            }

            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(_sdkDir, "MSBuild.dll"));
            Environment.SetEnvironmentVariable("MSBUILDEXTENSIONSPATH", _sdkDir);
        }

        public void InitializeEnvironment(ITestOutputHelper output)
        {
            output.WriteLine($"dotnet = {_dotnetDir}");
            output.WriteLine($"Setting MSBuild directory to {_sdkDir}");
        }
    }
}
