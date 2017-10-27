// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Finds Visual Studio.
    /// </summary>
    public class FindVisualStudio : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The path to the korebuild.json file. (Optional)
        /// </summary>
        public string ConfigFile { get; set; }

        /// <summary>
        /// The base path to the installation of VS.
        /// </summary>
        [Output]
        public string InstallationBasePath { get; set; }

        /// <summary>
        /// The path to MSBuild.exe (x86). It will be empty if this file does not exist.
        /// </summary>
        [Output]
        public string MSBuildx86Path { get; set; }

        /// <summary>
        /// The path to MSBuild.exe (x64). It will be empty if this file does not exist.
        /// </summary>
        [Output]
        public string MSBuildx64Path { get; set; }

        public override bool Execute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.LogError("Visual Studio cannot be found on non-Windows platforms");
                return false;
            }

            VsInstallation vs;
            if (!string.IsNullOrEmpty(ConfigFile))
            {
                if (!File.Exists(ConfigFile))
                {
                    Log.LogError($"Could not load the korebuild config file from '{ConfigFile}'");
                    return false;
                }

                var settings = KoreBuildSettings.Load(ConfigFile);
                var vsToolset = settings.Toolsets?.OfType<KoreBuildSettings.VisualStudioToolset>().FirstOrDefault();
                if (vsToolset != null)
                {
                    vs = VsWhere.FindLatestCompatibleInstallation(vsToolset, Log);
                    if (vs == null)
                    {
                        Log.LogError($"Could not find an installation of Visual Studio that satisifies the specified requirements in {ConfigFile}.");
                        return false;
                    }
                }
                else
                {
                    vs = VsWhere.FindLatestInstallation(includePrerelease: true, log: Log);
                }
            }
            else
            {
                vs = VsWhere.FindLatestInstallation(includePrerelease: true, log: Log);
            }

            if (vs == null)
            {
                Log.LogError($"Could not find any installation of Visual Studio.");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "Found {0} in {1}", vs.DisplayName, vs.InstallationPath);

            InstallationBasePath = vs.InstallationPath;
            MSBuildx86Path = vs.GetMSBuildx86SubPath();
            MSBuildx64Path = vs.GetMSBuildx64SubPath();

            return !Log.HasLoggedErrors;
        }
    }
}
