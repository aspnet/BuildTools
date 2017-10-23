// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class MSBuildCommand : SubCommandBase
    {
        public MSBuildCommand(CommandContext context) : base(context)
        {
        }

        private bool EnableBinaryLog => Environment.GetEnvironmentVariable("KOREBUILD_ENABLE_BINARY_LOG") == "1";

        private List<string> Arguments { get; set; }

        public override void Configure(CommandLineApplication application)
        {
            base.Configure(application);
            Arguments = application.RemainingArguments;
        }

        protected override int Execute()
        {
            Reporter.Verbose($"Building {Context.RepoPath}.");
            Reporter.Verbose($"dotnet = {Context.RepoPath}");

            if (Context.SDKVersion != "latest")
            {
                var globalFile = Path.Combine(Context.RepoPath, "global.json");
                File.WriteAllText(globalFile, $"{{ \"sdk\": {{ \"version\": \"{Context.SDKVersion}\" }} }}", System.Text.Encoding.ASCII);
            }
            else
            {
                Reporter.Verbose($"Skipping global.json generation because the SDKVersion = {Context.SDKVersion}");
            }

            var makeFileProj = Path.Combine(Context.KoreBuildDir, "KoreBuild.proj");
            var msBuildArtifactsDir = Path.Combine(Context.RepoPath, "artifacts", "msbuild");
            var msBuildResponseFile = Path.Combine(msBuildArtifactsDir, "msbuild.rsp");

            var msBuildLogArgument = string.Empty;


            if (EnableBinaryLog)
            {
                Reporter.Verbose("Enabling binary logging");
                var msBuildLogFilePath = Path.Combine(msBuildArtifactsDir, "msbuild.binlog");
                msBuildLogArgument = $"/bl:{msBuildLogFilePath}";
            }

            var msBuildArguments = string.Empty;

            foreach (var arg in Arguments)
            {
                msBuildArguments += Environment.NewLine + arg;
            }

            // TODO: naturalize newlines
            msBuildArguments += $@"
/nologo
/m
/p:RepositoryRoot={Context.RepoPath}\
{msBuildLogArgument}
/clp:Summary
""{makeFileProj}""
";

            Directory.CreateDirectory(msBuildArtifactsDir);

            var noop = msBuildArguments.IndexOf("/t:Noop", StringComparison.OrdinalIgnoreCase) >= 0
                || msBuildArguments.IndexOf("/t:Cow", StringComparison.OrdinalIgnoreCase) >= 0;

            File.WriteAllText(msBuildResponseFile, msBuildArguments, System.Text.Encoding.ASCII);
            Reporter.Verbose($"Noop = {noop}");
            var firstTime = Environment.GetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE");
            if (noop)
            {
                Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "true");
            }
            else
            {
                var buildTaskResult = BuildTaskProject(Context.RepoPath);
                if (buildTaskResult != 0)
                {
                    return buildTaskResult;
                }
            }

            return RunDotnet(new[] { "msbuild", $@"@""{msBuildResponseFile}""" });
        }

        private int BuildTaskProject(string path)
        {
            var taskFolder = Path.Combine(Context.RepoPath, "build", "tasks");
            var taskProj = Path.Combine(taskFolder, "RepoTasks.csproj");
            var publishFolder = Path.Combine(taskFolder, "bin", "publish");

            if (File.Exists(taskProj))
            {
                if (File.Exists(publishFolder))
                {
                    Directory.Delete(publishFolder, recursive: true);
                }

                var sdkPath = $"/p:RepoTasksSdkPath={Path.Combine(Context.KoreBuildDir, "msbuild", "KoreBuild.RepoTasks.Sdk", "Sdk")}";

                var restoreResult = RunDotnet(new[] { "restore", taskProj, sdkPath });
                if (restoreResult != 0)
                {
                    return restoreResult;
                }

                return RunDotnet(new[] { "publish", taskProj, "--configuration", "Release", "--output", publishFolder, "/nologo", sdkPath });
            }

            return 0;
        }
    }
}
