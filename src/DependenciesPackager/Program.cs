// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace DependenciesPackager
{
    class Program
    {
        private static readonly IEnumerable<NuGetFramework> FrameworkConfigurations =
            new[] { FrameworkConstants.CommonFrameworks.NetCoreApp10 };

        private const int Ok = 0;
        private const int Error = 1;

        private readonly ILogger _logger;

        private readonly CommandLineApplication _app;
        private readonly CommandOption _projectJson;
        private readonly CommandOption _sourceFolders;
        private readonly CommandOption _fallbackFeeds;
        private readonly CommandOption _destination;
        private readonly CommandOption _version;
        private readonly CommandOption _cliPath;
        private readonly CommandOption _optRuntime;
        private readonly CommandOption _prefix;
        private readonly CommandOption _keepTemporaryFiles;
        private readonly CommandOption _quiet;
        private readonly CommandOption _skipZip;
        private readonly CommandOption _exclusionsFile;

        public Program(
            CommandLineApplication app,
            CommandOption projectJson,
            CommandOption sourceFolders,
            CommandOption fallbackFeeds,
            CommandOption destination,
            CommandOption packagesVersion,
            CommandOption cliPath,
            CommandOption quiet,
            CommandOption runtimes,
            CommandOption prefix,
            CommandOption keepTemporaryFiles,
            CommandOption skipZip,
            CommandOption exclusionsFile)
        {
            _app = app;
            _projectJson = projectJson;
            _sourceFolders = sourceFolders;
            _fallbackFeeds = fallbackFeeds;
            _destination = destination;
            _version = packagesVersion;
            _cliPath = cliPath;
            _optRuntime = runtimes;
            _prefix = prefix;
            _keepTemporaryFiles = keepTemporaryFiles;
            _quiet = quiet;
            _skipZip = skipZip;
            _exclusionsFile = exclusionsFile;

            // set up logger
            _logger = new LoggerFactory()
                .AddConsole(
                    quiet.HasValue() ? LogLevel.Warning : LogLevel.Information,
                    includeScopes: false)
                .CreateLogger<Program>();
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

            var quiet = app.Option(
                "--quiet",
                "Avoids printing to the output anything other than warnings or errors",
                CommandOptionType.NoValue);

            var keepTemporaryFiles = app.Option(
                "--keep-temporary-files",
                "Avoids deleting the package folders and the publish folder used for crossgen",
                CommandOptionType.NoValue);

            var runtimes = app.Option(
                "--runtime",
                "The runtimes for which to generate the cache",
                CommandOptionType.MultipleValue);

            var prefix = app.Option(
                "--prefix",
                "The prefix to use for the zip file name",
                CommandOptionType.SingleValue);

            var skipZip = app.Option(
                "--skip-zip",
                "Avoids zipping the packages cache",
                CommandOptionType.NoValue);

            var exclusionFile = app.Option(
                "--exclusion-file",
                "File with dll names that can fail to crossgen",
                CommandOptionType.SingleValue);


            var program = new Program(
                app,
                projectJson,
                sourceFolders,
                fallbackFeeds,
                destination,
                packagesVersion,
                useCli,
                quiet,
                runtimes,
                prefix,
                keepTemporaryFiles,
                skipZip,
                exclusionFile);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_projectJson.HasValue() ||
                    !_destination.HasValue() ||
                    !_optRuntime.HasValue() ||
                    !_version.HasValue())
                {
                    _app.ShowHelp();
                    return Error;
                }

                if (!RuntimeValidator.IsValid(_optRuntime.Values, _logger))
                {
                    return Error;
                }

                var disposables = new List<IDisposable>();

                var restoreContext = new PackageRestoreContext(
                    _projectJson.Value(),
                    _destination.Value(),
                    _cliPath.HasValue() ? _cliPath.Value() : "dotnet",
                    _sourceFolders.HasValue() ? _sourceFolders.Values : Enumerable.Empty<string>(),
                    _fallbackFeeds.HasValue() ? _fallbackFeeds.Values : Enumerable.Empty<string>(),
                    _logger)
                {
                    Quiet = _quiet.HasValue()
                };
                disposables.Add(restoreContext);

                if (!restoreContext.Restore())
                {
                    _logger.LogWarning("Error restoring nuget packages into destination folder");
                }

                var project = ProjectReader.GetProject(_projectJson.Value());
                var outputs = new List<OutputFilesContext>();

                foreach (var framework in FrameworkConfigurations)
                {
                    foreach (var runtime in _optRuntime.Values)
                    {
                        _logger.LogInformation($"Performing crossgen on {framework.GetShortFolderName()} for runtime {runtime}.");

                        var outputContext = new OutputFilesContext(_destination.Value(), _version.Value(), runtime, restoreContext.RestoreFolder, project, _logger);
                        disposables.Add(outputContext);
                        outputs.Add(outputContext);

                        var projectContext = CreateProjectContext(project, framework, runtime, restoreContext.RestoreFolder);

                        var crossgenContext = new CrossgenContext(projectContext, _destination.Value(), _logger, _exclusionsFile.Value());

                        crossgenContext.CollectAssets(runtime, restoreContext.RestoreFolder);
                        crossgenContext.CreateResponseFile();
                        crossgenContext.FetchCrossgenTools(runtime);
                        crossgenContext.FetchJitTools(runtime, restoreContext.RestoreFolder);
                        crossgenContext.Crossgen(outputContext.OutputPath);
                        crossgenContext.WriteCrossgenOutput();
                        outputContext.PrintStatistics();

                        if (!crossgenContext.AllPackagesSuccessfullyCrossgened())
                        {
                            crossgenContext.PrintFailedCrossgenedPackages();
                            return Error;
                        }
                    }
                }

                OutputFilesContext.Compress(outputs, _destination.Value(), _prefix.Value(), _optRuntime.Value(), _skipZip.HasValue(), _logger);
                CleanupIntermediateFiles(disposables);

                return Ok;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                _app.ShowHelp();

                return Error;
            }
        }

        private void CleanupIntermediateFiles(IEnumerable<IDisposable> contexts)
        {
            if (_keepTemporaryFiles.HasValue())
            {
                return;
            }

            foreach (var c in contexts)
            {
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        c.Dispose();
                        return;
                    }
                    catch
                    {
                        Thread.Sleep(5000);
                    }
                }
            }
        }

        private static ProjectContext CreateProjectContext(Project project, NuGetFramework framework, string runtime, string restoreFolder) =>
            new ProjectContextBuilder()
                .WithPackagesDirectory(restoreFolder)
                .WithProject(project)
                .WithLockFile(LockFileReader.Read(project.ProjectFilePath.Replace("project.json", "project.lock.json"), designTime: false))
                .WithTargetFramework(framework)
                .WithRuntimeIdentifiers(new[] { runtime })
                .Build();
    }
}
