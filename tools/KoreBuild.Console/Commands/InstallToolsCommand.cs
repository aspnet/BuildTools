// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class InstallToolsCommand : SubCommandBase
    {
        public InstallToolsCommand(CommandContext context) : base(context)
        {
        }

        private string KoreBuildSkipRuntimeInstall => Environment.GetEnvironmentVariable("KOREBUILD_SKIP_RUNTIME_INSTALL");
        private string PathENV => Environment.GetEnvironmentVariable("PATH");
        private string DotNetInstallDir => Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");

        public override void Configure(CommandLineApplication application)
        {
            base.Configure(application);
        }

        protected override int Execute()
        {
            var installDir = Context.GetDotNetInstallDir();

            Reporter.Verbose($"Installing tools to '{installDir}'");

            if (DotNetInstallDir != null && DotNetInstallDir != installDir)
            {
                Reporter.Verbose($"installDir = {installDir}");
                Reporter.Verbose($"DOTNET_INSTALL_DIR = {DotNetInstallDir}");
                Reporter.Verbose("The environment variable DOTNET_INSTALL_DIR is deprecated. The recommended alternative is DOTNET_HOME.");
            }

            var dotnet = Context.GetDotNetExecutable();
            var dotnetOnPath = GetCommandFromPath("dotnet");

            // TODO: decide case sensitivity and handly symbolic links
            if (dotnetOnPath != null && (dotnetOnPath != dotnet))
            {
                Reporter.Warn($"dotnet found on the system PATH is '{dotnetOnPath}' but KoreBuild will use '{dotnet}'");
            }

            var pathPrefix = Directory.GetParent(dotnet);
            if (PathENV.StartsWith($"{pathPrefix}{Path.PathSeparator}", StringComparison.OrdinalIgnoreCase))
            {
                Reporter.Output($"Adding {pathPrefix} to PATH");
                Environment.SetEnvironmentVariable("PATH", $"{pathPrefix};{PathENV}");
            }

            if (KoreBuildSkipRuntimeInstall == "1")
            {
                Reporter.Output("Skipping runtime installation because KOREBUILD_SKIP_RUNTIME_INSTALL = 1");
                return 0;
            }

            var scriptExtension = Context.IsWindows() ? "ps1" : "sh";

            var scriptPath = Path.Combine(Context.KoreBuildDir, "dotnet-install." + scriptExtension);

            if (!Context.IsWindows())
            {
                var args = ArgumentEscaper.EscapeAndConcatenate(new string[] { "+x", scriptPath });
                var psi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = args
                };

                var process = Process.Start(psi);
                process.WaitForExit();
            }

            var architecture = Context.GetArchitecture();

            InstallCLI(scriptPath, installDir, architecture, Context.SDKVersion);

            return 0;
        }

        private void InstallCLI(string script, string installDir, string architecture, string version)
        {
            var sdkPath = Path.Combine(installDir, "sdk", version, "dotnet.dll");

            if (!File.Exists(sdkPath))
            {
                Reporter.Verbose($"Installing dotnet {version} to {installDir}");

                var args = ArgumentEscaper.EscapeAndConcatenate(new string[] {
                    "-Version", version,
                    "-Architecture", architecture,
                    "-InstallDir", installDir,
                    // workaround for https://github.com/dotnet/cli/issues/9143
                    // disable the CDN, which has non-deterministic behavior when multiple builds of the same SDK version exist
                    "-NoCdn",
                });

                var psi = new ProcessStartInfo
                {
                    FileName = script,
                    Arguments = args
                };

                var process = Process.Start(psi);
                process.WaitForExit();
            }
            else
            {
                Reporter.Output($".NET Core SDK {version} is already installed. Skipping installation.");
            }
        }

        private static string GetCommandFromPath(string command)
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(';'))
            {
                var fullPath = Path.Combine(path, command);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }
    }
}
