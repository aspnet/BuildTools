// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Finds toolset information as listed in korebuild.json
    /// </summary>
    public class GetPathToFullMSBuild : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The path to MSBuild.exe (x86).
        /// </summary>
        [Output]
        public string MSBuildx86Path { get; set; }

        public override bool Execute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.LogError("Full MSBuild is not available on non-Windows.");
                return false;
            }

            var vs = VsWhere.FindLatestInstallation(includePrerelease: true, Log);

            if (vs == null)
            {
                Log.LogError($"Could not find an installation of Visual Studio.");
                return false;
            }

            MSBuildx86Path = vs.GetMSBuildx86SubPath();

            return !Log.HasLoggedErrors;
        }
    }
}
