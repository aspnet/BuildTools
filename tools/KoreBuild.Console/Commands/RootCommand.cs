// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class RootCommand : CommandBase
    {
        public override void Configure(CommandLineApplication application)
        {
            var context = new CommandContext(application);

            application.FullName = "korebuild";

            application.Command("install-tools", new InstallToolsCommand(context).Configure, throwOnUnexpectedArg: false);
            application.Command("install", c =>
            {
                c.HelpOption("-h|--help");
                c.Command("vs", new InstallToolsetsCommand(context).Configure, throwOnUnexpectedArg: false);
                c.OnExecute(() =>
                {
                    c.ShowHelp();
                    return 2;
                });
            });
            application.Command("msbuild", new MSBuildCommand(context).Configure, throwOnUnexpectedArg: false);
            application.Command("docker-build", new DockerBuildCommand(context).Configure, throwOnUnexpectedArg: false);

            // Commands that generate code and files
            application.Command("generate", c =>
            {
                c.HelpOption("-h|--help");

                c.Command("api-baselines", new ApiBaselinesGenerateCommand(context).Configure, throwOnUnexpectedArg: false);

                c.OnExecute(() =>
                {
                    c.ShowHelp();
                    return 2;
                });
            });

            application.VersionOption("--version", GetVersion);

            base.Configure(application);
        }

        private static string GetVersion()
                => typeof(RootCommand).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }
}
