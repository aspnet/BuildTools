// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.IO;
using Microsoft.Build.Framework;
using System.Reflection;

namespace Microsoft.AspNetCore.BuildTools.ApiCheck.Task
{
    /// <summary>
    /// An MSBuild task that acts as a shim to <c>Microsoft.AspNetCore.BuildTools.ApiCheck.exe generate ...</c> or
    /// <c>dotnet Microsoft.AspNetCore.BuildTools.ApiCheck.dll generate ...</c>.
    /// </summary>
    public class ApiCheckGenerateTask : ApiCheckTasksBase
    {
        public ApiCheckGenerateTask()
        {
            /// Tool does not use stderr for anything. Treat everything that appears there as an error.
            LogStandardErrorAsError = true;
        }

        /// <summary>
        /// Path to the project.assets.json file created when building <see cref="AssemblyPath"/>.
        /// </summary>
        [Required]
        public string ProjectAssetsPath { get; set; }

        /// <summary>
        /// Path to the API listing file to be generated.
        /// </summary>
        [Required]
        public string ApiListingDestination { get; set; }

        /// <summary>
        /// Exclude types defined in .Internal namespaces from the comparison, ignoring breaking changes in such types.
        /// </summary>
        public bool ExcludePublicInternalTypes { get; set; }

        /// <inheritdoc />
        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        /// <inheritdoc />
        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override bool ValidateParameters()
        {
            if (string.IsNullOrEmpty(ApiListingDestination) || !File.Exists(ApiListingDestination))
            {
                Log.LogError($"API listing file '{ApiListingDestination}' not specified or does not exist.");
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

            if (string.IsNullOrEmpty(ProjectAssetsPath) || !File.Exists(ProjectAssetsPath))
            {
                Log.LogError($"Project assets file '{ProjectAssetsPath}' not specified or does not exist.");
                return false;
            }

            return base.ValidateParameters();
        }

        protected override string GenerateCommandLineCommands()
        {
            var arguments = string.Empty;
            if (!Framework.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
            {
                var taskAssemblyFolder = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
                var toolPath = Path.Combine(taskAssemblyFolder, "..", "netcoreapp2.0", ApiCheckToolName + ".dll");
                arguments = $@"""{Path.GetFullPath(toolPath)}"" ";
            }

            arguments += "generate";
            if (ExcludePublicInternalTypes)
            {
                arguments += " --exclude-public-internal";
            }

            arguments += $@" --assembly ""{AssemblyPath}"" --framework {Framework}";
            arguments += $@" --project ""{ProjectAssetsPath}"" --api-listing ""{ApiListingDestination}""";
            
            return arguments;
        }
    }
}
