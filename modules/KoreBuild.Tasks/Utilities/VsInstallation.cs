// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;

namespace KoreBuild.Tasks.Utilities
{
    /// <summary>
    /// A DTO that is deserialized from the output of 'vswhere.exe -format json'
    /// </summary>
    internal class VsInstallation
    {
        private static readonly string[] Versions = { "Current", "15.0", "16.0" };

        public string DisplayName { get; set; }
        public string InstallationPath { get; set; }

        // Add methods for additional info inferred from the vswhere.exe output.
        public string GetMSBuildx86SubPath()
        {
            return Versions
                .Select(v => Path.Combine(InstallationPath, "MSBuild", v, "Bin", "MSBuild.exe"))
                .FirstOrDefault(File.Exists);
        }

        public string GetMSBuildx64SubPath()
        {
            return Versions
                .Select(v => Path.Combine(InstallationPath, "MSBuild", v, "Bin", "amd64", "MSBuild.exe"))
                .FirstOrDefault(File.Exists);
        }
    }
}
