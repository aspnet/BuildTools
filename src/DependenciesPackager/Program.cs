// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        private ILogger _logger;

        private CommandLineApplication _app;
        private CommandOption _projectJson;
        private CommandOption _diagnosticsProjectJson;
        private CommandOption _sourceFolders;
        private CommandOption _fallbackFeeds;
        private CommandOption _destination;
        private CommandOption _version;
        private CommandOption _dotnetPath;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = CreateLogger();
                }

                return _logger;
            }
        }

        private ILogger CreateLogger()
        {
            var loggerFactory = new LoggerFactory();
            var logLevel = LogLevel.Information;

            loggerFactory.AddConsole(logLevel, includeScopes: false);

            return loggerFactory.CreateLogger<Program>();
        }

        public Program(
            CommandLineApplication app,
            CommandOption projectJson,
            CommandOption diagnosticsProjectJson,
            CommandOption sourceFolders,
            CommandOption fallbackFeeds,
            CommandOption destination,
            CommandOption packagesVersion,
            CommandOption dotnetPath)
        {
            _app = app;
            _projectJson = projectJson;
            _diagnosticsProjectJson = diagnosticsProjectJson;
            _sourceFolders = sourceFolders;
            _fallbackFeeds = fallbackFeeds;
            _destination = destination;
            _version = packagesVersion;
            _dotnetPath = dotnetPath;
        }

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "DependenciesPackager";

            app.HelpOption("-?|-h|--help");

            var projectJson = app.Option(
                "--project <PATH>",
                "The path to the project.json file from which to perform dotnet restore",
                CommandOptionType.SingleValue);

            var diagnosticsProjectJson = app.Option(
                "--diagnostics-project <PATH>",
                "The path to the project.json file from which to perform dotnet restore for diagnostic purposes",
                CommandOptionType.SingleValue);

            var sourceFolders = app.Option(
                "--sources <DIRS>",
                "Path to the directories containing the nuget packages",
                CommandOptionType.MultipleValue);

            var fallbackFeeds = app.Option(
                "--fallback <URLS>",
                "The list of URLs to use as fallback feeds in case the package can't be found on the source folders",
                CommandOptionType.MultipleValue);

            var destination = app.Option(
                "--destination <DIR>",
                "The folder on which the artifacts will be produced",
                CommandOptionType.SingleValue);

            var packagesVersion = app.Option(
                "--version <VERSION>",
                "The version of the artifacts produced",
                CommandOptionType.SingleValue);

            var useCli = app.Option(
                "--use-cli <PATH>",
                "The path to the dotnet CLI tools to use",
                CommandOptionType.SingleValue);

            var program = new Program(
                app,
                projectJson,
                diagnosticsProjectJson,
                sourceFolders,
                fallbackFeeds,
                destination,
                packagesVersion,
                useCli);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_projectJson.HasValue() ||
                    !_sourceFolders.HasValue() ||
                    !_fallbackFeeds.HasValue() ||
                    !_destination.HasValue() ||
                    !_version.HasValue())
                {
                    _app.ShowHelp();
                    return Error;
                }

                try
                {
                    if (_diagnosticsProjectJson.HasValue())
                    {
                        RunDotnetRestore(_diagnosticsProjectJson.Value());
                    }

                    if (RunDotnetRestore(_projectJson.Value()) != 0)
                    {
                        throw new InvalidOperationException("Error restoring nuget packages into destination folder");
                    }

                    RemoveUnnecessaryFiles();
                    CreateZipPackage();
                }
                catch (Exception e)
                {
                    Logger.LogError(e.Message);
                    return Error;
                }

                return Ok;
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                _app.ShowHelp();
                return Error;
            }
        }

        private void CreateZipPackage()
        {
            var packagesFolder = Path.Combine(_destination.Value(), _version.Value());
            var zipFileName = Path.Combine(_destination.Value(), $"AspNetCore.{_version.Value()}.zip");
            Logger.LogInformation($"Creating zip package on {zipFileName}");
            ZipFile.CreateFromDirectory(packagesFolder, zipFileName);
        }

        private void RemoveUnnecessaryFiles()
        {
            Logger.LogInformation($"Removing unnecessary files from folder");
            var packagesFolder = Path.Combine(_destination.Value(), _version.Value());
            var dlls = Directory.GetFiles(packagesFolder, "*.dll", SearchOption.AllDirectories);
            var shas = Directory.GetFiles(packagesFolder, "*.sha512", SearchOption.AllDirectories);
            var allEntries = Directory.GetFiles(packagesFolder, "*", SearchOption.AllDirectories);
            var entriesToRemove = allEntries.Except(dlls).Except(shas);

            foreach (var entry in entriesToRemove)
            {
                File.Delete(entry);
            }
        }

        private int RunDotnetRestore(string projectJson)
        {
            Logger.LogInformation($"Running dotnet restore on {Path.Combine(_destination.Value(), _version.Value())}");
            var sources = string.Join(" ", _sourceFolders.Values.Select(v => $"--source {v}"));
            var fallbackFeeds = string.Join(" ", _fallbackFeeds.Values.Select(v => $"--fallbacksource {v} "));
            var packages = $"--packages {Path.Combine(_destination.Value(), _version.Value())}";

            var dotnet = _dotnetPath.HasValue() ? _dotnetPath.Value() : "dotnet";

            var arguments = string.Join(" ",
                "restore",
                packages,
                projectJson,
                sources,
                fallbackFeeds,
                "--verbosity Verbose");

            var processInfo = new ProcessStartInfo(dotnet, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            var process = Process.Start(processInfo);

            string line = process.StandardOutput.ReadLine();
            while (line != null)
            {
                Console.WriteLine(line);
                line = process.StandardOutput.ReadLine();
            }

            Console.WriteLine(process.StandardError.ReadToEnd());
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}