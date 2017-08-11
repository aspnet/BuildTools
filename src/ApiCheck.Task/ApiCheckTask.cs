// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.BuildTools.ApiCheck.Task
{
    /// <summary>
    /// An MSBuild task that acts as a shim to <c>Microsoft.AspNetCore.BuildTools.ApiCheck.exe compare ...</c> or
    /// <c>dotnet Microsoft.AspNetCore.BuildTools.ApiCheck.dll compare ...</c>.
    /// </summary>
    public class ApiCheckTask : ApiCheckTasksBase
    {
        /// <summary>
        /// Path to the exclusions file that narrows <see cref="ApiListingPath"/>, ignoring listed breaking changes.
        /// </summary>
        public string ExclusionsPath { get; set; }

        /// <inheritdoc />
        protected override bool ValidateParameters()
        {
            if (string.IsNullOrEmpty(ApiListingPath) || !File.Exists(ApiListingPath))
            {
                Log.LogError($"API listing file '{ApiListingPath}' not specified or does not exist.");
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
        protected override string GenerateCommandLineCommands()
        {
            var arguments = GenerateCommandLineCommands("compare");

            if (!string.IsNullOrEmpty(ExclusionsPath))
            {
                arguments += $@" --exclusions ""{ExclusionsPath}""";
            }

            return arguments;
        }
    }
}
