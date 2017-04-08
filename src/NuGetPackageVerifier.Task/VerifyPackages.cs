// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Extensions.CommandLineUtils;
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

            if (string.IsNullOrEmpty(ArtifactDirectory) || !Directory.Exists(ArtifactDirectory))
            {
                Log.LogError($"ArtifactDirectory '{ArtifactDirectory}' does not exist");
                return false;
            }

            var taskAssemblyFolder = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
            var toolPath = Path.Combine(taskAssemblyFolder, "../../NuGetPackageVerifier.dll");
            var dotnetMuxer = DotNetMuxer.MuxerPathOrDefault();
            var psi = new ProcessStartInfo
            {
                FileName = dotnetMuxer,
                Arguments = ArgumentEscaper.EscapeAndConcatenate(new[]
                {
                    toolPath,
                    ArtifactDirectory,
                    RuleFile,
                })
            };

            Log.LogMessage($"Executing '{psi.FileName} {psi.Arguments}'");
            var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }
}
