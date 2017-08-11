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
        protected override bool ValidateParameters()
        {
            if (string.IsNullOrEmpty(ApiListingPath))
            {
                Log.LogError($"API listing file '{ApiListingPath}' not specified.");
                return false;
            }

            return base.ValidateParameters();
        }

        protected override string GenerateCommandLineCommands()
        {
            return GenerateCommandLineCommands("generate");
        }
    }
}
