// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace NuGetPackagerVerifier
{
    /// <summary>
    /// An MSBuild task that acts as a shim into NuGetPackageVerifier.dll
    /// </summary>
    public class VerifyPackages : MSBuildTask
    {
        [Required]
        public string RuleFile { get; set; }

        [Required]
        public string ArtifactDirectory { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(RuleFile) || !File.Exists(RuleFile))
            {
                Log.LogError($"RuleFile '{RuleFile}' does not exist");
                return false;
            }

            // prevent trailing slash from breaking the surrounding quotes
            ArtifactDirectory = ArtifactDirectory?.TrimEnd(new [] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            if (string.IsNullOrEmpty(ArtifactDirectory) || !Directory.Exists(ArtifactDirectory))
            {
                Log.LogError($"ArtifactDirectory '{ArtifactDirectory}' does not exist");
                return false;
            }

            var exeExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ".exe"
                : string.Empty;

            var taskAssemblyFolder = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
            var toolPath = Path.Combine(taskAssemblyFolder, "../../NuGetPackageVerifier.dll");
            var dotnetPath = new FileInfo(AppContext.GetData("FX_DEPS_FILE") as string)
                .Directory? // 1.0.1
                .Parent? // Microsoft.NETCore.App
                .Parent? // shared
                .Parent? // DOTNET_HOME
                .GetFiles("dotnet" + exeExtension)
                .SingleOrDefault();

            var psi = new ProcessStartInfo
            {
                FileName = dotnetPath?.Exists == true
                    ? dotnetPath.FullName
                    : "dotnet",  // Fallback to system PATH and hope for the best
                Arguments = $"\"{toolPath}\" \"{ArtifactDirectory}\" \"{RuleFile}\""
            };

            Log.LogMessage($"Executing '{psi.FileName} {psi.Arguments}'");
            var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }
}
