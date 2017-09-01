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
            application.FullName = "korebuild";

            application.Command("install-tools", new InstallToolsCommand().Configure, throwOnUnexpectedArg:false);
            application.Command("msbuild", new MSBuildCommand().Configure, throwOnUnexpectedArg:false);
            application.Command("docker-build", new DockerBuildCommand().Configure, throwOnUnexpectedArg: false);

            application.VersionOption("--version", GetVersion);

            base.Configure(application);
        }

        private static string GetVersion()
                => typeof(RootCommand).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }
}
