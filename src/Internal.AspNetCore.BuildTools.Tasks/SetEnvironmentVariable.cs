// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.BuildTools
{
    public class SetEnvironmentVariable : Task
    {
        [Required]
        public string Variable { get; set; }

        [Required]
        public string Value { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Variable))
            {
                Log.LogError($"{nameof(Variable)} cannot be null or an empty string");
                return false;
            }

            var expandedValue = Environment.ExpandEnvironmentVariables(Value);

            Log.LogMessage("Setting environment variable '{0}' to '{1}'", Variable, expandedValue);

            Environment.SetEnvironmentVariable(Variable, expandedValue);

            return true;
        }
    }
}
