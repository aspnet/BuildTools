// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Installs toolset information as listed in korebuild.json
    /// </summary>
    public class InstallToolsets : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The path to the korebuild.json file.
        /// </summary>
        [Required]
        public string ConfigFile { get; set; }

        /// <summary>
        /// Whether to install toolsets with or without user interation.
        /// It will default prompting users to confirm and see installation steps.
        /// </summary>
        public bool QuietInstallationOfToolsets { get; set; }

        /// <summary>
        /// Whether to upgrade existing toolsets.
        /// It will default to only adding tools that were not previously installed.
        /// </summary>
        public bool UpgradeVSInstallation { get; set; }

        /// <summary>
        /// Specifies what version of VS to install.
        /// Defaults to Enterprise.
        /// </summary>
        public string VSProductVersionType { get; set; } = "Enterprise";

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
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
                        await InstallVsComponents(vs);
                        break;
                    // TODO support NodeJSToolset
                    default:
                        Log.LogWarning("Toolset checks not implemented for " + toolset.GetType().Name);
                        break;
                }
            }

            return !Log.HasLoggedErrors;
        }

        private async Task InstallVsComponents(KoreBuildSettings.VisualStudioToolset vsToolset)
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
            var vsExePath = await VsInstallerHelper.DownloadVsExe(Log);
            var vsJsonFilePath = VsInstallerHelper.CreateVsFileFromRequiredToolset(vsToolset, Log, VSProductVersionType);

            var args = GetVisualStudioArgs(vs, vsJsonFilePath);

            var psi = new ProcessStartInfo
            {
                FileName = vsExePath,
                Arguments = args
            };

            Log.LogMessage($"Calling: {psi.FileName} {psi.Arguments}");

            var process = Process.Start(psi);

            process.WaitForExit();

            return;
        }

        private string GetVisualStudioArgs(VsInstallation vs, string vsJsonFilePath)
        {
            var args = new List<string>();

            if (vs != null)
            {
                if (UpgradeVSInstallation)
                {
                    args.Add("upgrade");
                }
                else
                {
                    args.Add("modify");
                }
            }

            args.Add("--installPath");
            args.Add($"{vs.InstallationPath}");
            args.Add("--in");
            args.Add($"{vsJsonFilePath}");
            args.Add("--wait");
            args.Add("--norestart");

            if (QuietInstallationOfToolsets)
            {
                args.Add("--quiet");
            }
            return ArgumentEscaper.EscapeAndConcatenate(args);
        }
    }
}
