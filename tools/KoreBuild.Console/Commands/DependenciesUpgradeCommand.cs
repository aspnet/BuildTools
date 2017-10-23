// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class DependenciesUpgradeCommand : SubCommandBase
    {
        private CommandOption _sourceOpt;
        private CommandOption _packageIdOpt;
        private CommandOption _packageVersionOpt;
        private CommandOption _fileOpt;

        public DependenciesUpgradeCommand(CommandContext context) : base(context)
        {
        }

        public override void Configure(CommandLineApplication application)
        {
            application.Description = "Upgrades the build/dependencies.props file to the latest package versions";
            application.ExtendedHelpText = @"
MORE INFO:

    The upgrade uses a 'lineup' package as the source of information about which versions to use.

    A lineup package is simply a nuget package that contains a file in build/dependencies.props.
    Just like the version of the file in this local repo, this file is an MSBuild project file
    with a list of MSBuild variables. Example:

    <Project>
      <PropertyGroup Label=""Package Versions"">
        <MyPackageVersion>1.0.0</MyPackageVersion>
      </PropertyGroup>
    </Project>
";

            _sourceOpt = application.Option("-s|--source <SOURCE>",
                "Specifies a NuGet package source to use to upgrade dependencies to the latest lineup package.", CommandOptionType.SingleValue);
            _packageIdOpt = application.Option("--id <PACKAGE_ID>", "Specifies the lineup package id to use.", CommandOptionType.SingleValue);
            _packageVersionOpt = application.Option("--version <PACKAGE_VERISON>", "Specifies the lineup package version to use.", CommandOptionType.SingleValue);
            _fileOpt = application.Option("--deps-file <FILEPATH>", "The dependencies.props file to upgrade.", CommandOptionType.SingleValue);

            base.Configure(application);
        }

        protected override int Execute()
        {
            var args = new List<string>
            {
                "msbuild",
                Path.Combine(Context.KoreBuildDir, "KoreBuild.proj"),
                "-t:UpgradeDependencies",
            };

            if (_sourceOpt.HasValue())
            {
                args.Add("-p:LineupPackageRestoreSource=" + _sourceOpt.Value());
            }

            if (_packageIdOpt.HasValue())
            {
                args.Add("-p:LineupPackageId=" + _packageIdOpt.Value());
            }

            if (_packageVersionOpt.HasValue())
            {
                args.Add("-p:LineupPackageVersion=" + _packageVersionOpt.Value());
            }

            if (_fileOpt.HasValue())
            {
                var filePath = _fileOpt.Value();
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.GetFullPath(filePath);
                }

                args.Add("-p:DependencyVersionsFile=" + filePath);
            }

            if (Reporter.IsVerbose)
            {
                args.Add("-v:n");
            }

            return RunDotnet(args, Context.RepoPath);
        }
    }
}
