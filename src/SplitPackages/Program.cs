// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace SplitPackages
{
    class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        private ILogger _logger;
        private bool _whatIf;

        private CommandLineApplication _app;
        private CommandOption _sourceOption;
        private CommandOption _csvOption;
        private CommandOption _destinationOption;

        public Program(
            CommandLineApplication app,
            CommandOption sourceOption,
            CommandOption csvOption,
            CommandOption destinationOption,
            CommandOption whatIfOption)
        {
            _app = app;
            _sourceOption = sourceOption;
            _csvOption = csvOption;
            _destinationOption = destinationOption;
            _whatIf = whatIfOption.HasValue();

            InitializeLogger();
        }

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "SplitPackages";

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

            var program = new Program(app, sourceOption, csvOption, destinationOption, whatIfOption);

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

                var packagesFromSource = GetPackagesFromSourceFolder(arguments.SourceFolder);
                var packagesFromCsv = GetPackagesFromCsvFile(arguments.CsvFile);
                var expandedPackages = ExpandPackages(packagesFromCsv, arguments.SourceFolder);

                EnsureNoExtraSourcePackagesFound(packagesFromSource, expandedPackages);

                EmitWarningsForMissingPackages(packagesFromSource, packagesFromCsv);

                EnsureDestinationDirectories(
                    arguments.DestinationFolder,
                    expandedPackages.Select(e => e.DestinationFolderName).Distinct());

                SetDestinationPath(arguments, expandedPackages);

                CopyPackages(expandedPackages);

                return Ok;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _app.ShowHelp();
                return Error;
            }
        }

        private void InitializeLogger()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();
            _logger = loggerFactory.CreateLogger<Program>();
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

        private IList<PackageItem> GetPackagesFromSourceFolder(string sourceFolder)
        {
            var packages = Directory.EnumerateFiles(sourceFolder);

            return packages.Select(p => new PackageItem
            {
                OriginPath = p,
                Name = Path.GetFileName(p),
                FoundInFolder = true
            }).ToList();
        }

        private IList<PackageItem> GetPackagesFromCsvFile(string csvFile)
        {
            var lines = File.ReadAllLines(csvFile);
            var parsedLines = lines.Select(l => l.Split(','));

            return parsedLines.Select(parsed => new PackageItem
            {
                Name = parsed[0].Trim(),
                DestinationFolderName = parsed[1].Trim(),
                FoundInCsv = true
            }).ToList();
        }

        private IList<PackageItem> ExpandPackages(IList<PackageItem> packagesFromCsv, string sourceFolder)
        {
            var allPackages = new HashSet<PackageItem>();

            for (var i = 0; i < packagesFromCsv.Count; i++)
            {
                var foundPackages = Directory.EnumerateFiles(sourceFolder, packagesFromCsv[i].Name);
                var expandedPackages = foundPackages.Select(fp => new PackageItem
                {
                    OriginPath = fp,
                    Name = Path.GetFileName(fp),
                    FoundInCsv = true,
                    FoundInFolder = true,
                    DestinationFolderName = packagesFromCsv[i].DestinationFolderName
                }).ToList();

                foreach (var package in expandedPackages)
                {
                    allPackages.Add(package);
                }

                if (expandedPackages.Count > 0)
                {
                    packagesFromCsv[i].FoundInFolder = true;
                }
            }

            return allPackages.ToList();
        }

        private void EnsureNoExtraSourcePackagesFound(IList<PackageItem> source, IList<PackageItem> expanded)
        {
            for (var i = 0; i < source.Count; i++)
            {
                for (var j = 0; j < expanded.Count; j++)
                {
                    if (source[i].Name.Equals(
                        expanded[j].Name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        source[i].FoundInCsv = true;
                    }
                }
            }
        }

        private void EmitWarningsForMissingPackages(IList<PackageItem> source, IList<PackageItem> csv)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!source[i].FoundInCsv)
                {
                    var msg = $@"No entry in the csv file matched the following package:
    {source[i].Name}";
                    _logger.LogWarning(msg);
                }
            }

            for (int i = 0; i < csv.Count; i++)
            {
                if (!csv[i].FoundInFolder)
                {
                    var msg = $@"No package found in the source folder for the following csv entry:
    {csv[i].Name}";
                    _logger.LogWarning(msg);
                }
            }
        }

        private void EnsureDestinationDirectories(string destinationFolder, IEnumerable<string> folders)
        {
            if (!Directory.Exists(destinationFolder))
            {
                var msg = $@"Destination folder does not exist, creating folder at:
    {destinationFolder}";
                _logger.LogInformation(msg);

                if (!_whatIf)
                {
                    Directory.CreateDirectory(destinationFolder);
                }
            }

            foreach (var destination in folders)
            {
                var path = Path.Combine(destinationFolder, destination);
                if (!Directory.Exists(path))
                {
                    _logger.LogInformation($@"Creating destination folder at:
{path}");

                    if (!_whatIf)
                    {
                        Directory.CreateDirectory(path);
                    }
                }
            }
        }

        private void SetDestinationPath(Arguments arguments, IList<PackageItem> expandedPackages)
        {
            foreach (var package in expandedPackages)
            {
                var path = Path.Combine(
                    arguments.DestinationFolder,
                    package.DestinationFolderName,
                    package.Name);

                package.DestinationPath = path;
            }
        }

        private void CopyPackages(IList<PackageItem> expandedPackages)
        {
            foreach (var package in expandedPackages)
            {
                _logger.LogInformation($@"Copying package {package.Name} from
    {package.OriginPath} to
    {package.DestinationPath}");
                if (!_whatIf)
                {
                    File.Copy(package.OriginPath, package.DestinationPath, overwrite: true);
                }
            }
        }

        private class PackageItem : IEquatable<PackageItem>
        {
            public string Name { get; set; }
            public string OriginPath { get; set; }
            public string DestinationFolderName { get; set; }
            public string DestinationPath { get; set; }
            public bool FoundInCsv { get; set; }
            public bool FoundInFolder { get; set; }

            public bool Equals(PackageItem other)
            {
                return string.Equals(OriginPath, other?.OriginPath);
            }

            public override int GetHashCode()
            {
                return OriginPath != null ? OriginPath.GetHashCode() : 1;
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
