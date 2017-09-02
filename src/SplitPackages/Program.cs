// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using PackageClassifier;

namespace SplitPackages
{
    class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        private ILogger _logger;

        private readonly CommandLineApplication _app;
        private readonly CommandOption _sourceOption;
        private readonly CommandOption _csvOption;
        private readonly CommandOption _destinationOption;
        private readonly CommandOption _whatIf;
        private readonly CommandOption _quiet;
        private readonly CommandOption _ignoreErrors;

        public ILogger Logger
        {
            get { return _logger ?? (_logger = CreateLogger()); }
        }

        public Program(
            CommandLineApplication app,
            CommandOption sourceOption,
            CommandOption csvOption,
            CommandOption destinationOption,
            CommandOption whatIfOption,
            CommandOption quiet,
            CommandOption ignoreErrors)
        {
            _app = app;
            _sourceOption = sourceOption;
            _csvOption = csvOption;
            _destinationOption = destinationOption;
            _whatIf = whatIfOption;
            _quiet = quiet;
            _ignoreErrors = ignoreErrors;
        }

        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--debug")
            {
                args = args.Skip(1).ToArray();
                Console.WriteLine($"Waiting for debugger to attach to {Process.GetCurrentProcess().Id}.");
                Console.ReadLine();
            }

            var app = new CommandLineApplication
            {
                Name = "SplitPackages"
            };
            app.HelpOption("-?|-h|--help");

            var sourceOption = app.Option(
                    "--source <DIR>",
                    "The directory containing the nuget packages to split in different folders",
                    CommandOptionType.SingleValue);

            var csvOption = app.Option(
                "--csv <PATH>",
                "Path to the CSV file containing the names and subfolders where each package must go",
                CommandOptionType.SingleValue);

            var destinationOption = app.Option(
                "--destination <DIR>",
                "The directory on which subdirectories will be created for the different destinations of the packages",
                CommandOptionType.SingleValue);

            var whatIfOption = app.Option(
                "--whatif",
                "Performs a dry run of the command with the given arguments without copying the packages",
                CommandOptionType.NoValue);

            var quiet = app.Option(
                "--quiet",
                "Avoids printing to the output anything other than warnings or errors",
                CommandOptionType.NoValue);

            var ignoreErrors = app.Option(
                "--ignore-errors",
                "Treats errors as warnings and allows the command to continue executing",
                CommandOptionType.NoValue);

            var program = new Program(
                app,
                sourceOption,
                csvOption,
                destinationOption,
                whatIfOption,
                quiet,
                ignoreErrors);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_sourceOption.HasValue() || !_csvOption.HasValue() || !_destinationOption.HasValue())
                {
                    _app.ShowHelp();
                    return Error;
                }

                var arguments = PrepareArguments(_sourceOption, _csvOption, _destinationOption);

                var cache = GetPackagesFromSourceFolder(arguments.SourceFolder);
                var packagesFromCsv = GetPackagesFromCsvFile(arguments.CsvFile);
                var classifier = new Classifier(cache, packagesFromCsv);

                LogPackageMappings(classifier);

                if (classifier.Diagnostics.Count > 0)
                {
                    WriteErrorMessage(classifier.Diagnostics);
                    if (!_ignoreErrors.HasValue())
                    {
                        return Error;
                    }
                }

                var categoryClassification = classifier.GetClassification("Category");

                EnsureDestinationDirectories(
                    arguments.DestinationFolder,
                    categoryClassification.ClassifiedPackages.Select(c => c.Trait.Value));

                CopyPackages(arguments.DestinationFolder, categoryClassification);

                CreateCsprojFiles(arguments.DestinationFolder, categoryClassification);

                var optimizedCacheClassification = classifier.GetClassification("OptimizedCache");

                CreateCsprojFilesForOptimizedCache(arguments.DestinationFolder, optimizedCacheClassification);

                return Ok;
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                _app.ShowHelp();
                return Error;
            }
        }

        private void CreateCsprojFilesForOptimizedCache(string destinationPath, ClassificationResult optimizedCacheClassification)
        {
            CreateCsprojFileWithTimeStamps(destinationPath, optimizedCacheClassification);
            CreateCsprojFileWithoutTimeStamps(destinationPath, optimizedCacheClassification);
        }

        private void CreateCsprojFileWithTimeStamps(string destinationPath, ClassificationResult optimizedCacheClassification)
        {
            var packages = GetPackagesForOptimizedCache(optimizedCacheClassification);
            var builder = CreateBaseCsprojFileBuilderForCache(destinationPath, "cache.csproj");
            builder.AddDependencies(packages);
            builder.Execute();
        }

        private void CreateCsprojFileWithoutTimeStamps(string destinationPath, ClassificationResult optimizedCacheClassification)
        {
            var packages = GetPackagesForOptimizedCache(optimizedCacheClassification);
            var correctedPackages = packages.Select(p => new PackageInformation(p.FullPath, p.Identity, GetFinalVersion(p.Version), p.SupportedFrameworks));

            var builder = CreateBaseCsprojFileBuilderForCache(destinationPath, "final.cache.csproj");
            builder.AddDependencies(correctedPackages);
            builder.Execute();
        }

        private IEnumerable<PackageInformation> GetPackagesForOptimizedCache(ClassificationResult optimizedCacheClassification)
        {
            var packages = optimizedCacheClassification.GetPackagesForValue("include");
            return packages.Where(p =>
            {
                var classification = Frameworks.ClassifyFramework(p.SupportedFrameworks);
                return classification.IsAll || classification.IsNetCoreApp10;
            });
        }

        private string GetFinalVersion(string version)
        {
            var frameworkVersion = new NuGetVersion(version);
            if (string.IsNullOrEmpty(frameworkVersion.Release) || frameworkVersion.Release.Contains("rtm"))
            {
                return new NuGetVersion(frameworkVersion.Version).ToNormalizedString();
            }
            else
            {
                var prefix = frameworkVersion.Release.Substring(0, frameworkVersion.Release.IndexOf('-'));
                var releaseLabel = $"{prefix}-final";
                return new NuGetVersion(frameworkVersion.Version, releaseLabel).ToNormalizedString();
            }
        }

        private CsprojFileBuilder CreateBaseCsprojFileBuilderForCache(string destinationPath, string projectName)
        {
            var builder = new CsprojFileBuilder(
                Path.Combine(destinationPath, projectName),
                _whatIf.HasValue(),
                _ignoreErrors.HasValue(),
                Logger);

            var hardcodedDependencies = new[]
            {
                new PackageInformation(null, "Microsoft.NetCore.App", "1.1.0", new[] { Frameworks.NetCoreApp10 })
            };

            builder.AddFramework(Frameworks.NetCoreApp10);
            builder.AddDependencies(hardcodedDependencies);
            builder.AddImports(Frameworks.NetCoreApp10, Frameworks.PortableNet451Win8);
            return builder;
        }

        private void WriteErrorMessage(IList<string> errors)
        {
            var header = new[] {
                "The list of packages in the source folder is inconsistent with the list of packages in the CSV file:"
            };

            var paddedErrors = errors.Select(e => string.Join(
                Environment.NewLine,
                e.Split(Environment.NewLine.ToCharArray()).Select(l => $"    {l}")));

            var message = string.Join($"{Environment.NewLine}    ", header.Concat(paddedErrors));
            if (_ignoreErrors.HasValue())
            {
                Logger.LogWarning(message);
            }
            else
            {
                Logger.LogError(message);
            }
        }

        private void CreateCsprojFiles(string destinationPath, ClassificationResult result)
        {
            foreach (var classification in result.ClassifiedPackages)
            {
                CreateCsprojFile(Path.Combine(destinationPath, classification.Trait.Value, "noimports.csproj"), classification);
                CreateCsprojFileWithImports(Path.Combine(destinationPath, classification.Trait.Value, "project.csproj"), classification);
            }
        }

        private void CreateCsprojFile(string path, ClassifiedPackages packages)
        {
            var csprojFileBuilder = CreateBaseCsprojFileBuilder(path, packages);
            csprojFileBuilder.Execute();
        }

        private void CreateCsprojFileWithImports(string path, ClassifiedPackages packages)
        {
            var csprojFileBuilder = CreateBaseCsprojFileBuilder(path, packages);

            csprojFileBuilder.AddImports(Frameworks.NetCoreApp10, Frameworks.DnxCore50);
            csprojFileBuilder.AddImports(Frameworks.NetCoreApp10, Frameworks.Dotnet56);
            csprojFileBuilder.AddImports(Frameworks.NetCoreApp10, Frameworks.PortableNet451Win8);
            csprojFileBuilder.Execute();
        }

        private CsprojFileBuilder CreateBaseCsprojFileBuilder(string path, ClassifiedPackages packages)
        {
            var csprojFileBuilder = new CsprojFileBuilder(
                path,
                _whatIf.HasValue(),
                _quiet.HasValue(),
                Logger);

            csprojFileBuilder.AddFramework(Frameworks.Net451);
            csprojFileBuilder.AddFramework(Frameworks.NetCoreApp10);
            csprojFileBuilder.AddDependencies(packages.Packages);
            return csprojFileBuilder;
        }

        private ILogger CreateLogger()
        {
            var loggerFactory = new LoggerFactory();
            var logLevel = _quiet.HasValue() ? LogLevel.Warning : LogLevel.Information;

            loggerFactory.AddConsole(logLevel, includeScopes: false);

            return loggerFactory.CreateLogger<Program>();
        }

        private Arguments PrepareArguments(
            CommandOption source,
            CommandOption csv,
            CommandOption destination)
        {
            return PrepareArguments(source.Value(), csv.Value(), destination.Value());
        }

        private Arguments PrepareArguments(params string[] args)
        {
            var sourcePackages = Path.GetFullPath(args[0]);
            var csvFile = Path.GetFullPath(args[1]);
            var destinationFolder = Path.GetFullPath(args[2]);

            if (!Directory.Exists(sourcePackages))
            {
                var msg = $@"Source packages folder does not exist or is not a folder
    {sourcePackages}";
                throw new InvalidOperationException(msg);
            }

            if (!File.Exists(csvFile))
            {
                var msg = $@"csv file does not exist or is not a file
    {csvFile}";
                throw new InvalidOperationException(msg);
            }

            return new Arguments
            {
                DestinationFolder = destinationFolder,
                CsvFile = csvFile,
                SourceFolder = sourcePackages
            };
        }

        private PackageSourcesCache GetPackagesFromSourceFolder(string sourceFolder)
        {
            var result = new PackageSourcesCache(new[] { sourceFolder });

            Logger.LogInformation($@"Packages found on the source folder
{string.Join(Environment.NewLine, result.Packages.Select(p => p.ToString()))}");

            return result;
        }

        private Classification GetPackagesFromCsvFile(string csvFile)
        {
            var classification = Classification.FromCsv(File.OpenText(csvFile));

            if (classification.Diagnostics != null)
            {
                Logger.LogError(classification.Diagnostics);
            }

            return classification;
        }

        private void LogPackageMappings(Classifier classifier)
        {
            foreach (var mapping in classifier.PackageMappings)
            {
                var packagePaths = string.Concat(mapping.Packages.Select(p => $"{Environment.NewLine}    {p}"));
                Logger.LogInformation(mapping.Entry.Identity.Contains("*")
                    ? $@"Packages extended for pattern '{mapping.Entry.Identity}':{packagePaths}"
                    : $@"Packages extended for literal '{mapping.Entry.Identity}':{packagePaths}");
            }
        }

        private void EnsureDestinationDirectories(string destinationFolder, IEnumerable<string> folders)
        {
            if (!Directory.Exists(destinationFolder))
            {
                var msg = $@"Destination folder does not exist, creating folder at:
    {destinationFolder}";
                Logger.LogInformation(msg);

                if (!_whatIf.HasValue())
                {
                    Directory.CreateDirectory(destinationFolder);
                }
            }

            foreach (var destination in folders)
            {
                var path = Path.Combine(destinationFolder, destination);
                if (!Directory.Exists(path))
                {
                    Logger.LogInformation($@"Creating destination folder at:
{path}");

                    if (!_whatIf.HasValue())
                    {
                        Directory.CreateDirectory(path);
                    }
                }
            }
        }

        private string GetDestinationPath(string destinationFolder, Trait categoryTrait, PackageInformation package)
        {
            return Path.Combine(destinationFolder, categoryTrait.Value, package.Name);
        }

        private void CopyPackages(string destinationFolder, ClassificationResult classification)
        {
            foreach (var classifiedValue in classification.ClassifiedPackages)
            {
                foreach (var package in classifiedValue.Packages)
                {
                    var destinationPath = GetDestinationPath(destinationFolder, classifiedValue.Trait, package);
                    Logger.LogInformation($@"Copying package {package.Name} from
    {package.FullPath} to
    {destinationPath}");

                    if (!_whatIf.HasValue())
                    {
                        File.Copy(package.FullPath, destinationPath, overwrite: true);
                    }
                }
            }
        }

        private class Arguments
        {
            public string SourceFolder { get; set; }
            public string CsvFile { get; set; }
            public string DestinationFolder { get; set; }
        }
    }
}
