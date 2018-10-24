// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
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
        /// The path to MSBuild.exe (x86) if the 'visualstudio' toolset was specified in korebuild.json.
        /// It will be empty if not specified or if this file does not exist.
        /// </summary>
        [Output]
        public string VisualStudioMSBuildx86Path { get; set; }

        /// <summary>
        /// The path to MSBuild.exe (x64) if the 'visualstudio' toolset was specified in korebuild.json.
        /// It will be empty if not specified or if this file does not exist.
        /// </summary>
        [Output]
        public string VisualStudioMSBuildx64Path { get; set; }

        /// <summary>
        /// The path to NodeJS.exe if the 'nodejs' toolset was specified in korebuild.json.
        /// It will be empty if not specified.
        /// </summary>
        [Output]
        public string NodeJSPath { get; set; }

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
                    case KoreBuildSettings.NodeJSToolset node:
                        GetNode(node);
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
                if ((vsToolset.Required & ~KoreBuildSettings.RequiredPlatforms.Windows) != 0)
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
                    Log.LogError($"Could not find an installation of Visual Studio that satisifies the specified requirements in '{ConfigFile}'. " +
                        "Execute `./run.ps1 install vs` to update or install the current VS installation.");
                }
                return;
            }

            Log.LogMessage(MessageImportance.High, "Using {0} from {1}", vs.DisplayName, vs.InstallationPath);

            VisualStudioMSBuildx86Path = vs.GetMSBuildx86SubPath();
            VisualStudioMSBuildx64Path = vs.GetMSBuildx64SubPath();
        }

        private void GetNode(KoreBuildSettings.NodeJSToolset nodeToolset)
        {
            var nodePath = EnvironmentHelper.GetCommandOnPath("nodejs") ?? EnvironmentHelper.GetCommandOnPath("node");

            var required = IsRequiredOnThisPlatform(nodeToolset.Required);

            if (string.IsNullOrEmpty(nodePath))
            {
                 LogFailure(
                    isError: required,
                    message: $"Could not find NodeJS on PATH.");
                return;
            }

            Log.LogMessage(MessageImportance.Low, "Found NodeJS in " + nodePath);

            if (nodeToolset.MinVersion == null)
            {
                NodeJSPath = nodePath;
                return;
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
            });
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                LogFailure(
                    isError: required,
                    message: $"Found NodeJS in '{nodePath}', but could not determine the version of NodeJS installed. 'node --version' failed.");
                return;
            }

            var nodeVersionString = process.StandardOutput.ReadToEnd()?.Trim()?.TrimStart('v');
            if (!Version.TryParse(nodeVersionString, out var nodeVersion))
            {
                LogFailure(
                    isError: required,
                    message: $"Found NodeJS in '{nodePath}', but could not determine the version of NodeJS installed. 'node --version' returned '{nodeVersionString}'.");
                return;
            }

            if (nodeVersion < nodeToolset.MinVersion)
            {
                LogFailure(
                    isError: required,
                    message: $"Found NodeJS in '{nodePath}', but its version '{nodeVersionString}' did not meet the required minimum version '{nodeToolset.MinVersion}' as specified in '{ConfigFile}'");
                return;
            }

            Log.LogMessage(MessageImportance.High, "Using NodeJS {0} from {1}", nodeVersionString, nodePath);
            NodeJSPath = nodePath;
        }

        private void LogFailure(bool isError, string message)
        {
            if (isError)
            {
                Log.LogError(message);
            }
            else
            {
                Log.LogWarning(message);
            }
        }

        private bool IsRequiredOnThisPlatform(KoreBuildSettings.RequiredPlatforms platforms)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return (platforms & KoreBuildSettings.RequiredPlatforms.Windows) != 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return (platforms & KoreBuildSettings.RequiredPlatforms.Linux) != 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return (platforms & KoreBuildSettings.RequiredPlatforms.MacOS) != 0;
            }

            return platforms != KoreBuildSettings.RequiredPlatforms.None;
        }
    }
}
