// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class DependenciesGenerateCommand : SubCommandBase
    {
        private CommandOption _configOpt;
        private CommandOption _fileOpt;

        public override void Configure(CommandLineApplication application)
        {
            application.Description = "Generates a build/dependencies.props file and updates csproj files to use variables";
            application.ExtendedHelpText = @"
MORE INFO:

    This command will generate a dependencies.props file and adjust all PackageReference's in csproj files
    to use the MSBuild variables it generates.

    Example output:

    <Project>
      <PropertyGroup Label=""Package Versions"">
        <MyPackageVersion>1.0.0</MyPackageVersion>
      </PropertyGroup>
    </Project>
";

            _configOpt = application.Option("-c|--configuration <CONFIG>", "The MSBuild configuration. Defaults to 'Debug'.", CommandOptionType.SingleValue);
            _fileOpt = application.Option("--deps-file <FILEPATH>", "The dependencies.props file to upgrade.", CommandOptionType.SingleValue);

            base.Configure(application);
        }

        protected override int Execute()
        {
            var args = new List<string>
            {
                "msbuild",
                Path.Combine(KoreBuildDir, "KoreBuild.proj"),
                "-t:GenerateDependenciesPropsFile",
            };

            if (_configOpt.HasValue())
            {
                args.Add("-p:Configuration=" + _configOpt.Value());
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

            return RunDotnet(args, RepoPath);
        }
    }
}
