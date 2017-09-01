// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal class DockerBuildCommand : SubCommandBase
    {
        private const string DockerIgnore = ".dockerignore";
        private const string DockerfileExtension = ".dockerfile";
        private const string Owner = "aspnetbuild";
        private const string ImageName = "korebuild";

        public CommandArgument ImageVariant { get; set; }

        public List<string> Arguments {get; set; }

        public string Tag => $@"{Owner}/{ImageName}:{ImageVariant.Value}";

        public override void Configure(CommandLineApplication application)
        {
            ImageVariant = application.Argument("image", "The docker image to run on.");
            Arguments = application.RemainingArguments;

            base.Configure(application);
        }

        protected override bool IsValid()
        {
            if(string.IsNullOrEmpty(ImageVariant?.Value))
            {
                Reporter.Error("Image is a required argument.");
                return false;
            }

            return true;
        }

        protected override int Execute()
        {
            var dockerFileName = GetDockerFileName(ImageVariant.Value);
            var dockerFileSource = GetDockerFileSource(dockerFileName);
            var dockerFileDestination = Path.Combine(RepoPath, GetDockerFileName(ImageVariant.Value));

            File.Copy(dockerFileSource, dockerFileDestination, overwrite: true);

            var dockerIgnoreSource = GetDockerFileSource(DockerIgnore);
            var dockerIgnoreDestination = Path.Combine(RepoPath, DockerIgnore);

            File.Copy(dockerIgnoreSource, dockerIgnoreDestination, overwrite: true);

            // If our ToolSource isn't http copy it to the docker context
            var dockerToolsSource = ToolsSource;
            string toolsSourceDestination = null;
            if (!ToolsSource.StartsWith("http"))
            {
                dockerToolsSource = "ToolsSource";
                toolsSourceDestination = Path.Combine(RepoPath, dockerToolsSource);
                DirectoryCopy(ToolsSource, toolsSourceDestination);
            }

            try
            {
                var buildArgs = new List<string> { "build" };

                buildArgs.AddRange(new string[] { "-t", Tag, "-f", dockerFileDestination, RepoPath });
                var buildResult = RunDockerCommand(buildArgs);

                if (buildResult != 0)
                {
                    return buildResult;
                }

                var containerName = $"{Owner}_{DateTime.Now.ToString("yyyyMMddHHmmss")}";

                var runArgs = new List<string> { "run", "--rm", "-it", "--name", containerName, Tag };

                runArgs.AddRange(new[] { "-ToolsSource", dockerToolsSource });

                if (Arguments?.Count > 0)
                {
                    runArgs.AddRange(Arguments);
                }

                Reporter.Verbose($"Running in container '{containerName}'");
                return RunDockerCommand(runArgs);
            }
            finally{
                // Clean up the stuff we dumped there in order to get it in the docker context.
                File.Delete(dockerFileDestination);
                File.Delete(dockerIgnoreDestination);
                if(toolsSourceDestination != null)
                {
                    Directory.Delete(toolsSourceDestination, recursive: true);
                }
            }
        }

        private string GetDockerFileSource(string fileName)
        {
            var executingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var source = Path.Combine(executingDir, "Commands", "DockerFiles", fileName);

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

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, overwrite: true);
            }

            // Copy subdirectories and their contents to the new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }
    }
}
