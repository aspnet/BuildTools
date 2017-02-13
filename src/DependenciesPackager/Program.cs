// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NugetReferenceResolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
        private readonly CommandOption _projectCsproj;
        private readonly CommandOption _sourceFolders;
        private readonly CommandOption _destination;
        private readonly CommandOption _version;
        private readonly CommandOption _cliPath;
        private readonly CommandOption _optRuntime;
        private readonly CommandOption _prefix;
        private readonly CommandOption _keepTemporaryFiles;
        private readonly CommandOption _quiet;
        private readonly CommandOption _skipZip;
        private readonly CommandOption _exclusionsFile;
        private readonly CommandOption _preserveCasing;

        public Program(
            CommandLineApplication app,
            CommandOption projectCsproj,
            CommandOption sourceFolders,
            CommandOption destination,
            CommandOption packagesVersion,
            CommandOption cliPath,
            CommandOption quiet,
            CommandOption runtimes,
            CommandOption prefix,
            CommandOption keepTemporaryFiles,
            CommandOption skipZip,
            CommandOption exclusionsFile,
            CommandOption preserveCasing)
        {
            _app = app;
            _projectCsproj = projectCsproj;
            _sourceFolders = sourceFolders;
            _destination = destination;
            _version = packagesVersion;
            _cliPath = cliPath;
            _optRuntime = runtimes;
            _prefix = prefix;
            _keepTemporaryFiles = keepTemporaryFiles;
            _quiet = quiet;
            _skipZip = skipZip;
            _exclusionsFile = exclusionsFile;
            _preserveCasing = preserveCasing;

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

            var projectCsproj = app.Option(
                "--project <PATH>",
                "The path to the csproj file from which to perform dotnet restore",
                CommandOptionType.SingleValue);

            var sourceFolders = app.Option(
                "--sources <DIRS>",
                "Path to the directories containing the nuget packages",
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
                CommandOptionType.SingleValue);

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

            var preserveCasing = app.Option(
                "--preserve-casing",
                "Preserve casing in package ID. Required for legacy projects (project.json/NUGet 3.4)",
                CommandOptionType.NoValue);

            var program = new Program(
                app,
                projectCsproj,
                sourceFolders,
                destination,
                packagesVersion,
                useCli,
                quiet,
                runtimes,
                prefix,
                keepTemporaryFiles,
                skipZip,
                exclusionFile,
                preserveCasing);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_projectCsproj.HasValue() ||
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
                    _projectCsproj.Value(),
                    _destination.Value(),
                    _cliPath.HasValue() ? _cliPath.Value() : "dotnet",
                    _sourceFolders.HasValue() ? _sourceFolders.Values : Enumerable.Empty<string>(),
                    _logger)
                {
                    Quiet = _quiet.HasValue()
                };
                disposables.Add(restoreContext);

                if (!restoreContext.Restore())
                {
                    _logger.LogWarning("Error restoring nuget packages into destination folder");
                }

                var format = new LockFileFormat();
                var graph = PackageGraph.Create(format.Read(GetAssetsFile()), "netcoreapp1.1");
                var outputs = new List<OutputFilesContext>();

                foreach (var framework in FrameworkConfigurations)
                {
                    foreach (var runtime in _optRuntime.Values)
                    {
                        _logger.LogInformation($"Performing crossgen on {framework.GetShortFolderName()} for runtime {runtime}.");

                        var outputContext = new OutputFilesContext(_destination.Value(), _version.Value(), runtime, _logger);
                        disposables.Add(outputContext);
                        outputs.Add(outputContext);

                        var crossgenContext = new CrossgenContext(graph, _destination.Value(), _logger, _exclusionsFile.Value());

                        if (_preserveCasing.HasValue())
                        {
                            crossgenContext.PreserveOriginalPackageCasing();
                        }

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

        private string GetAssetsFile() =>
            Path.Combine(Path.GetDirectoryName(_projectCsproj.Value()), "obj", "project.assets.json");

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
    }
}
