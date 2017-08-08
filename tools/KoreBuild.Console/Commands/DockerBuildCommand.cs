// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class DockerBuildCommand : SubCommandBase
    {
        private const string DockerfileExtension = ".dockerfile";

        public CommandArgument Platform { get; set; }

        public List<string> Arguments {get; set; }
        
        public string ContainerName { get; set; } = "testcontainer";

        public override void Configure(CommandLineApplication application)
        {
            Platform = application.Argument("platform", "The docker platform to run on.");
            Arguments = application.RemainingArguments;

            base.Configure(application);
        }

        protected override bool IsValid()
        {
            if(string.IsNullOrEmpty(Platform?.Value))
            {
                Reporter.Error("Platform is a required argument.");
                return false;
            }

            return true;
        }

        protected override int Execute()
        {
            var dockerFileSource = GetDockerFileSource(Platform.Value);
            var dockerFileDestination = Path.Combine(RepoPath, GetDockerFileName(Platform.Value));

            File.Copy(dockerFileSource, dockerFileDestination, true);

            try
            {
                var buildArgs = new List<string> { "build" };

                buildArgs.AddRange(new string[] { "-t", ContainerName, "-f", dockerFileDestination, RepoPath });
                var buildResult = RunDockerCommand(buildArgs);

                if (buildResult != 0)
                {
                    return buildResult;
                }

                var runArgs = new List<string> { "run", "--rm", "-it", "--name", ContainerName, ContainerName };

                if (Arguments?.Count > 0)
                {
                    var argString = string.Join(" ", Arguments);
                    runArgs.Add(argString);
                }

                return RunDockerCommand(runArgs);
            }
            finally{
                // Clean up the dockerfile we dumped there.
                File.Delete(dockerFileDestination);
            }
        }

        private string GetDockerFileSource(string platform)
        {
            var dockerFileName = GetDockerFileName(platform);

            var executingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var source = Path.Combine(executingDir, "Commands", "DockerFiles", dockerFileName);

            if(!File.Exists(source))
            {
                Reporter.Error($"DockerFile '{source}' doesn't exist.");
                throw new FileNotFoundException();
            }

            return source;
        }

        private string GetDockerFileName(string platform)
        {
            return $"{platform}{DockerfileExtension}";
        }

        private int RunDockerCommand(List<string> arguments)
        {
            var args = ArgumentEscaper.EscapeAndConcatenate(arguments.ToArray());
            Reporter.Verbose($"Running 'docker {args}'");

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardError = true
            };

            var process = Process.Start(psi);
            process.WaitForExit();

            if(process.ExitCode != 0)
            {
                Reporter.Error(process.StandardError.ReadToEnd());
            }

            return process.ExitCode;
        }
    }
}
