// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class ApiBaselinesGenerateCommand : SubCommandBase
    {
        public ApiBaselinesGenerateCommand(CommandContext context) : base(context)
        {
        }

        public override void Configure(CommandLineApplication application)
        {
            application.Description = "Generates baselines for all projects in this repo.";

            base.Configure(application);
        }

        protected override int Execute()
        {
            var args = new List<string>
            {
                "msbuild",
                "/nologo",
                "/m",
                $"/p:KoreBuildVersion={this.Context.KoreBuildVersion}",
                $"/p:RepositoryRoot=\"{this.Context.RepoPath}/\"",
                "\"/p:GenerateBaselines=true\"",
                "\"/p:SkipTests=true\"",
                "/clp:Summary",
                Path.Combine(Context.KoreBuildDir, "KoreBuild.proj")
            };

            if (Reporter.IsVerbose)
            {
                args.Add("\"/v:n\"");
            }
            else
            {
                args.Add("\"/v:m\"");
            }

            return RunDotnet(args, Context.RepoPath);
        }
    }
}
