// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace KoreBuild.Tasks.Utilities
{
    /// <summary>
    /// A DTO that is deserialized from the output of 'vswhere.exe -format json'
    /// </summary>
    internal class VsInstallation
    {
        public string DisplayName { get; set; }
        public string InstallationPath { get; set; }

        // Add methods for additional info inferred from the vswhere.exe output.
        public string GetMSBuildx86SubPath()
        {
            var path = Path.Combine(InstallationPath, "MSBuild", "15.0", "Bin", "MSBuild.exe");

            return File.Exists(path)
                ? path
                : null;
        }

        public string GetMSBuildx64SubPath()
        {
            var path = Path.Combine(InstallationPath, "MSBuild", "15.0", "Bin", "amd64", "MSBuild.exe");

            return File.Exists(path)
                ? path
                : null;
        }
    }
}
