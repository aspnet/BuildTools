// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Runtime.InteropServices;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Finds toolset information as listed in korebuild.json
    /// </summary>
    public class GetToolsets : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The path to the korebuild.json file.
        /// </summary>
        [Required]
        public string ConfigFile { get; set; }

        /// <summary>
        /// The path to MSBuild.exe (x86), if the 'visualstudio' toolset was specified. It will be empty if this file does not exist.
        /// </summary>
        [Output]
        public string VisualStudioMSBuildx86Path { get; set; }

        /// <summary>
        /// The path to MSBuild.exe (x64), if the 'visualstudio' toolset was specified. It will be empty if this file does not exist.
        /// </summary>
        [Output]
        public string VisualStudioMSBuildx64Path { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ConfigFile))
            {
                Log.LogError($"Could not load the korebuild config file from '{ConfigFile}'");
                return false;
            }

            var settings = KoreBuildSettings.Load(ConfigFile);

            if (settings?.Toolsets == null)
            {
                Log.LogMessage(MessageImportance.Normal, "No recognized toolsets specified.");
                return true;
            }

            foreach (var toolset in settings.Toolsets)
            {
                switch (toolset)
                {
                    case KoreBuildSettings.VisualStudioToolset vs:
                        GetVisualStudio(vs);
                        break;
                    default:
                        Log.LogWarning("Toolset checks not implemented for " + toolset.GetType().Name);
                        break;
                }
            }

            return !Log.HasLoggedErrors;
        }

        private void GetVisualStudio(KoreBuildSettings.VisualStudioToolset vsToolset)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if ((vsToolset.Required & KoreBuildSettings.RequiredPlatforms.Windows) != 0)
                {
                    Log.LogError("Visual Studio is not available on non-Windows. Change korebuild.json to 'required: [\"windows\"]'.");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping Visual Studio verification on non-Windows platforms.");
                }
                return;
            }

            var vs = VsWhere.FindLatestCompatibleInstallation(vsToolset, Log);
            if (vs == null)
            {
                if (vsToolset.Required != KoreBuildSettings.RequiredPlatforms.None)
                {
                    Log.LogError($"Could not find an installation of Visual Studio that satisifies the specified requirements in {ConfigFile}. This is required to build.");
                }
                return;
            }

            Log.LogMessage(MessageImportance.High, "Using {0} from {1}", vs.DisplayName, vs.InstallationPath);

            VisualStudioMSBuildx86Path = vs.GetMSBuildx86SubPath();
            VisualStudioMSBuildx64Path = vs.GetMSBuildx64SubPath();
        }
    }
}
