// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if !NET46
using System.Runtime.InteropServices;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    public class GetOSPlatform : Task
    {
        [Output]
        public string PlatformName { get; set; }

        public override bool Execute()
        {
#if NET46
            // MSBuild.exe only runs on Windows. This task doesn't support xbuild, only dotnet-msbuild and MSBuild.exe.
            PlatformName = "Windows";
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlatformName = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                PlatformName = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                PlatformName = "macOS";
            }
            else
            {
                Log.LogError("Failed to determine the platform on which the build is running");
                return false;
            }
#endif
            return true;
        }
    }
}
