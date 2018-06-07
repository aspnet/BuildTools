// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class InstallToolsetsCommand : SubCommandBase
    {
        private CommandOption _quietOpt;
        private CommandOption _upgradeOpt;

        public InstallToolsetsCommand(CommandContext context) : base(context)
        {
        }

        public override void Configure(CommandLineApplication application)
        {
            application.Description = "Installs the toolsets necessary to build the current project.";
            application.ExtendedHelpText = @"
MORE INFO:

    Uses the toolsets specified in korebuild.json to install all toolsets.
";
            _quietOpt = application.Option("-q|--quiet",
                "Install toolsets without requiring user interation.",
                CommandOptionType.NoValue);
            _upgradeOpt = application.Option("-u|--upgrade",
                "Upgrade existing toolsets.",
                CommandOptionType.NoValue);
            base.Configure(application);
        }

        protected override int Execute()
        {
            var args = new List<string>
            {
                "msbuild",
                Path.Combine(Context.KoreBuildDir, "KoreBuild.proj"),
                "-t:InstallToolsets",
            };

            if (_quietOpt.HasValue())
            {
                args.Add("-p:Upgrade=true");
            }

            if (_quietOpt.HasValue())
            {
                args.Add("-p:Quiet=true");
            }

            if (Reporter.IsVerbose)
            {
                args.Add("-v:n");
            }

            return RunDotnet(args, Context.RepoPath);
        }
    }
}
