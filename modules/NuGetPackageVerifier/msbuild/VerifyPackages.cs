// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private const string ConsoleAppExe = "NuGetPackageVerifier.dll";

        [Required]
        public string RuleFile { get; set; }

        [Required]
        public string ArtifactDirectory { get; set; }

        public string[] ExcludedRules { get; set; }

        public string SignRequestManifest { get; set; }

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
            var toolPath = Path.Combine(taskAssemblyFolder, "..", "..", ConsoleAppExe);
            if (!File.Exists(toolPath))
            {
                toolPath = Path.Combine(taskAssemblyFolder, ConsoleAppExe);
            }

            var dotnetMuxer = DotNetMuxer.MuxerPathOrDefault();
            var arguments = new List<string>
            {
                toolPath,
                "--rule-file",
                RuleFile,
                ArtifactDirectory,
            };

            if (!string.IsNullOrEmpty(SignRequestManifest))
            {
                if (!File.Exists(SignRequestManifest))
                {
                    Log.LogError($"SignRequestManifest file {SignRequestManifest} does not exist.");
                    return false;
                }

                arguments.Add("--sign-request");
                arguments.Add(SignRequestManifest);
            }

            foreach (var rule in ExcludedRules ?? Enumerable.Empty<string>())
            {
                arguments.Add("--excluded-rule");
                arguments.Add(rule);
            }

            var psi = new ProcessStartInfo
            {
                FileName = dotnetMuxer,
                Arguments = ArgumentEscaper.EscapeAndConcatenate(arguments),
            };

            Log.LogCommandLine($"Executing '{psi.FileName} {psi.Arguments}'");

            var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }
}
