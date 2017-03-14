// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    public class GetAssemblyFileVersion : Task
    {
        [Required]
        public string AssemblyVersion { get; set; }

        [Required]
        public int AssemblyRevision { get; set; }

        [Output]
        public string AssemblyFileVersion { get; set; }

        public override bool Execute()
        {
            if (!Version.TryParse(AssemblyVersion, out var assemblyVersionValue))
            {
                Log.LogError("Invalid value '{0}' for {1}.", AssemblyVersion, nameof(AssemblyVersion));
                return false;
            }

            var assemblyFileVersionValue = assemblyVersionValue;
            if (assemblyFileVersionValue.Revision <= 0)
            {
                assemblyFileVersionValue = new Version(
                    assemblyFileVersionValue.Major,
                    assemblyFileVersionValue.Minor,
                    assemblyFileVersionValue.Build,
                    AssemblyRevision);
            }

            AssemblyFileVersion = assemblyFileVersionValue.ToString();
            return true;
        }
    }
}
