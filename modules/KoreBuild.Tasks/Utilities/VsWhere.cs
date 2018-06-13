// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace KoreBuild.Tasks.Utilities
{
    internal class VsWhere
    {
        public static VsInstallation FindLatestInstallation(bool includePrerelease, TaskLoggingHelper log)
        {
            var args = new List<string>
            {
                "-latest",
            };
            if (includePrerelease)
            {
                args.Add("-prerelease");
            }

            return GetInstallations(args, log).FirstOrDefault();
        }

        public static VsInstallation FindLatestInstallation(bool includePrerelease, string vsProductVersion, TaskLoggingHelper log)
        {
            var args = new List<string>
            {
                "-latest",
            };
            if (includePrerelease)
            {
                args.Add("-prerelease");
            }
            args.Add("-products");
            args.Add(vsProductVersion);

            return GetInstallations(args, log).FirstOrDefault();
        }

        public static VsInstallation FindLatestCompatibleInstallation(KoreBuildSettings.VisualStudioToolset toolset, TaskLoggingHelper log)
        {
            var args = new List<string> { "-latest" };

            if (toolset.IncludePrerelease)
            {
                args.Add("-prerelease");
            }

            if (TryGetVersion(toolset, out var version))
            {
                args.Add("-version");
                args.Add(version);
            }

            if (toolset.RequiredWorkloads != null)
            {
                foreach (var workload in toolset.RequiredWorkloads)
                {
                    args.Add("-requires");
                    args.Add(workload);
                }
            }

            return GetInstallations(args, log).FirstOrDefault();
        }

        // Internal for testing
        internal static bool TryGetVersion(KoreBuildSettings.VisualStudioToolset toolset, out string version)
        {
            if (!string.IsNullOrEmpty(toolset.VersionRange))
            {
                // This is the same as MinVersion but the name indicates that a user can specify more than just
                // a min version. For example: [15.0,16.0) will find versions 15.*.
                version = toolset.VersionRange;
                return true;
            }

            if (!string.IsNullOrEmpty(toolset.MinVersion))
            {
                // Here for back compatibility.
                version = toolset.MinVersion;
                return true;
            }

            version = null;
            return false;
        }

        private static VsInstallation[] GetInstallations(List<string> args, TaskLoggingHelper log)
        {
            args.Add("-format");
            args.Add("json");

            var vswhere = GetVsWherePath();
            var process = new Process
            {
                StartInfo =
                {
                    FileName = vswhere,
                    Arguments = ArgumentEscaper.EscapeAndConcatenate(args),
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            log.LogCommandLine(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

            try
            {
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                log.LogError("vswhere failed." + ex.Message);
                return Array.Empty<VsInstallation>();

            }

            var output = process.StandardOutput.ReadToEnd();

            if (process.ExitCode != 0)
            {
                log.LogMessage(MessageImportance.Low, "vswhere output = " + output);
                log.LogError("vswhere failed.");
                return Array.Empty<VsInstallation>();
            }

            return JsonConvert.DeserializeObject<VsInstallation[]>(output);
        }

        private static string GetVsWherePath()
        {
            var searchPaths = new[]
            {
                Path.Combine(Path.GetDirectoryName(typeof(FindVisualStudio).Assembly.Location), "vswhere.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe"),
            };

            var file = searchPaths.FirstOrDefault(File.Exists);

            return file ?? "vswhere";
        }
    }
}
