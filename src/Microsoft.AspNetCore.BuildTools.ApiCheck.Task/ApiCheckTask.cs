// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools.ApiCheck.Task
{
    /// <summary>
    /// An MSBuild task that acts as a shim to <c>Microsoft.AspNetCore.BuildTools.ApiCheck.exe compare ...</c> or
    /// <c>dotnet Microsoft.AspNetCore.BuildTools.ApiCheck.dll compare ...</c>.
    /// </summary>
    public class ApiCheckTask : ToolTask
    {
        private const string ApiCheckToolName = "Microsoft.AspNetCore.BuildTools.ApiCheck";

        public ApiCheckTask()
        {
            // Tool does not use stderr for anything. Treat everything that appears there as an error.
            LogStandardErrorAsError = true;
        }

        /// <summary>
        /// Path to the API listing file to use as reference.
        /// </summary>
        [Required]
        public string ApiListingPath { get; set; }

        /// <summary>
        /// Path to the assembly to compare with <see cref="ApiListingPath"/>.
        /// </summary>
        [Required]
        public string AssemblyPath { get; set; }

        /// <summary>
        /// The framework moniker for <see cref="AssemblyPath"/>.
        /// </summary>
        [Required]
        public string Framework { get; set; }

        /// <summary>
        /// Exclude types defined in .Internal namespaces from the comparison, ignoring breaking changes in such types.
        /// </summary>
        public bool ExcludePublicInternalTypes { get; set; }

        /// <summary>
        /// Path to the exclusions file that narrows <see cref="ApiListingPath"/>, ignoring listed breaking changes.
        /// </summary>
        public string ExclusionsPath { get; set; }

        /// <summary>
        /// Path to the project.assets.json file created when building <see cref="AssemblyPath"/>.
        /// </summary>
        [Required]
        public string ProjetAssetsPath { get; set; }

        /// <inheritdoc />
        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        /// <inheritdoc />
        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        /// <inheritdoc />
        protected override string ToolName
        {
            get
            {
                if (Framework.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiCheckToolName + ".exe";
                }

                var exeExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
                return "dotnet" + exeExtension;
            }
        }

        /// <inheritdoc />
        protected override bool ValidateParameters()
        {
            if (string.IsNullOrEmpty(ApiListingPath) || !File.Exists(ApiListingPath))
            {
                Log.LogError($"API listing file '{ApiListingPath}' not specified or does not exist.");
                return false;
            }

            if (string.IsNullOrEmpty(AssemblyPath) || !File.Exists(AssemblyPath))
            {
                Log.LogError($"Assembly '{AssemblyPath}' not specified or does not exist.");
                return false;
            }

            if (string.IsNullOrEmpty(Framework))
            {
                Log.LogError("Framework moniker must be specified.");
                return false;
            }

            if (string.IsNullOrEmpty(ProjetAssetsPath) || !File.Exists(ProjetAssetsPath))
            {
                Log.LogError($"Project assets file '{ProjetAssetsPath}' not specified or does not exist.");
                return false;
            }

            if (!string.IsNullOrEmpty(ExclusionsPath) && !File.Exists(ExclusionsPath))
            {
                Log.LogError($"Exclusions file '{ExclusionsPath}' does not exist.");
                return false;
            }

            return base.ValidateParameters();
        }

        /// <inheritdoc />
        protected override string GenerateFullPathToTool()
        {
            // ToolExe equals ToolName by default. Assume (as base class does) any user override is in the same directory.
            if (Framework.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
            {
                var taskAssemblyFolder = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
                return Path.GetFullPath(Path.Combine(taskAssemblyFolder, "..", "net46", ToolExe));
            }

            // If muxer does not find dotnet, fall back to system PATH and hope for the best.
            return DotNetMuxer.MuxerPath ?? ToolExe;
        }

        /// <inheritdoc />
        protected override string GenerateCommandLineCommands()
        {
            var arguments = string.Empty;
            if (!Framework.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
            {
                var taskAssemblyFolder = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
                var toolPath = Path.Combine(taskAssemblyFolder, "..", "netcoreapp2.0", ApiCheckToolName + ".dll");
                arguments = $@"""{Path.GetFullPath(toolPath)}"" ";
            }

            arguments += "compare";
            if (ExcludePublicInternalTypes)
            {
                arguments += " --exclude-public-internal";
            }

            arguments += $@" --assembly ""{AssemblyPath}"" --framework {Framework}";
            arguments += $@" --project ""{ProjetAssetsPath}"" --api-listing ""{ApiListingPath}""";
            if (!string.IsNullOrEmpty(ExclusionsPath))
            {
                arguments += $@" --exclusions ""{ExclusionsPath}""";
            }

            return arguments;
        }

        /// <inheritdoc />
        protected override string GetWorkingDirectory()
        {
            // Working directory should not matter. Use project folder because it is a well-known location.
            return Path.GetDirectoryName(ApiListingPath);
        }

        /// <inheritdoc />
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            // Since tool prints out formatted list of breaking changes,
            // anything that starts with Error considered an error; the rest is user information.
            if (singleLine.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError(singleLine, Array.Empty<object>());
            }
            else
            {
                base.LogEventsFromTextOutput(singleLine, messageImportance);
            }
        }
    }
}
