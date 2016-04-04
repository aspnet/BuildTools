// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;

namespace SplitPackages
{
    class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        private ILogger _logger;

        private CommandLineApplication _app;
        private CommandOption _sourceOption;
        private CommandOption _csvOption;
        private CommandOption _destinationOption;
        private CommandOption _whatIf;
        private CommandOption _quiet;
        private CommandOption _ignoreErrors;

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

                var packagesFromSource = GetPackagesFromSourceFolder(arguments.SourceFolder);
                var packagesFromCsv = GetPackagesFromCsvFile(arguments.CsvFile);
                var expandedPackages = ExpandPackages(packagesFromCsv, packagesFromSource, arguments.SourceFolder);

                EnsureNoExtraSourcePackagesFound(packagesFromSource, expandedPackages);

                var errors = new List<string>();
                EmitWarningsForMissingPackages(packagesFromSource, packagesFromCsv, errors);

                if (errors.Count > 0)
                {
                    WriteErrorMessage(errors);
                    if (!_ignoreErrors.HasValue())
                    {
                        return Error;
                    }
                }

                EnsureDestinationDirectories(
                    arguments.DestinationFolder,
                    expandedPackages.Select(e => e.DestinationFolderName).Distinct());

                SetDestinationPath(arguments, expandedPackages);

                CopyPackages(expandedPackages);

                CreateProjectJsonFiles(expandedPackages);

                return Ok;
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                _app.ShowHelp();
                return Error;
            }
        }

        private void WriteErrorMessage(List<string> errors)
        {
            var header = new [] {
                "The list of packages in the source folder is inconsistent with the list of packages in the CSV file:"
            };

            var message = string.Join($"{Environment.NewLine}    ", header.Concat(errors));

            if(_ignoreErrors.HasValue())
            {
                Logger.LogWarning(message);
            }
            else
            {
                Logger.LogError(message);
            }
        }

        private void CreateProjectJsonFiles(IEnumerable<PackageItem> packages)
        {
            var packagesByFolder = packages.GroupBy(p => Path.GetDirectoryName(p.DestinationPath));

            foreach (var path in packagesByFolder)
            {
                var dependenciesList = path.ToList();

                var jsonFileBuilder = new ProjectJsonFileBuilder(
                    Path.Combine(path.Key, "project.json"),
                    _whatIf.HasValue(),
                    _ignoreErrors.HasValue(),
                    Logger);

                jsonFileBuilder.AddDependencies(dependenciesList);
                jsonFileBuilder.AddFramework(Framework.Net451);
                jsonFileBuilder.Execute();
            }
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

        private IList<PackageItem> GetPackagesFromSourceFolder(string sourceFolder)
        {
            var packages = Directory.EnumerateFiles(sourceFolder)
                .Where(f => Path.GetExtension(f).Equals(".nupkg", StringComparison.OrdinalIgnoreCase));

            var result = packages.Select(p => new PackageItem
            {
                OriginPath = p,
                Name = Path.GetFileName(p),
                FoundInFolder = true
            }).ToList();

            foreach (var package in result)
            {
                SetNugetIdentityAndVersion(package);
            }

            Logger.LogInformation($@"Packages found on the source folder
{string.Join(Environment.NewLine, result.Select(p => p.ToString()))}");

            return result;
        }

        private IList<PackageItem> GetPackagesFromCsvFile(string csvFile)
        {
            var lines = File.ReadAllLines(csvFile).Skip(1); // Skip file header
            var parsedLines = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(','));

            // Order the entries so that most specific entries come first.
            return parsedLines
                .OrderByDescending(l => l[0], StringComparer.OrdinalIgnoreCase)
                .Select(parsed => new PackageItem
                {
                    Name = parsed[0].Trim(),
                    DestinationFolderName = parsed[1].Trim(),
                    FoundInCsv = true
                }).ToList();
        }

        private IList<PackageItem> ExpandPackages(
            IList<PackageItem> packagesFromCsv,
            IList<PackageItem> packagesFromSource,
            string source)
        {
            var allPackages = new HashSet<PackageItem>();

            var noPackages = new List<PackageItem>();

            for (var i = 0; i < packagesFromCsv.Count; i++)
            {
                IList<PackageItem> expandedPackages;
                var packageName = packagesFromCsv[i].Name;
                var destination = packagesFromCsv[i].DestinationFolderName;

                if (packageName.Contains("*"))
                {
                    expandedPackages = ExpandPackagesFromPattern(source, packagesFromSource, packageName, destination);

                    Logger.LogInformation($@"Packages extended for pattern '{packageName}'
{string.Join(Environment.NewLine, expandedPackages.Select(r => r.ToString()))}");
                }
                else if (LiteralPatternIsDependency(packagesFromSource, packageName))
                {
                    expandedPackages = ExpandPackagesFromLiteral(packagesFromSource, packageName, destination);
                    Logger.LogInformation($@"Packages extended for literal '{packageName}'
{string.Join(Environment.NewLine, expandedPackages.Select(r => r.ToString()))}");
                }
                else
                {
                    expandedPackages = noPackages;
                }

                foreach (var package in expandedPackages)
                {
                    package.FoundInFolder = true;
                    package.FoundInCsv = true;
                    allPackages.Add(package);
                }

                if (expandedPackages.Count > 0)
                {
                    packagesFromCsv[i].FoundInFolder = true;
                }
            }

            foreach (var item in allPackages)
            {
                SetNugetIdentityAndVersion(item);
            }

            return allPackages.ToList();
        }

        private static bool LiteralPatternIsDependency(IList<PackageItem> packages, string packageName)
        {
            return packages.Any(p => p.Identity.Equals(packageName, StringComparison.OrdinalIgnoreCase));
        }

        private static IList<PackageItem> ExpandPackagesFromLiteral(
            IList<PackageItem> packagesFromSource,
            string packageName,
            string destinationFolderName)
        {
            var packageFromSource = packagesFromSource
                .Single(p => p.Identity.Equals(packageName, StringComparison.OrdinalIgnoreCase));

            return new List<PackageItem>
            {
                new PackageItem
                {
                    OriginPath = packageFromSource.OriginPath,
                    Name = packageFromSource.Name,
                    DestinationFolderName = destinationFolderName,
                    Identity = packageFromSource.Identity,
                    Version = packageFromSource.Version
                }
            };
        }

        private static IList<PackageItem> ExpandPackagesFromPattern(
            string sourceFolder,
            IList<PackageItem> packagesFromSource,
            string pattern,
            string destinationFolderName)
        {
            return Directory.EnumerateFiles(sourceFolder, pattern)
                .Select(fp =>
                {
                    var packageFromSource = packagesFromSource.Single(p => p.OriginPath == fp);
                    return new PackageItem
                    {
                        OriginPath = fp,
                        Name = Path.GetFileName(fp),
                        DestinationFolderName = destinationFolderName,
                        Identity = packageFromSource.Identity,
                        Version = packageFromSource.Version
                    };
                })
                .ToList();
        }

        private static void SetNugetIdentityAndVersion(PackageItem item)
        {
            using (var reader = new PackageArchiveReader(item.OriginPath))
            {
                var identity = reader.GetIdentity();
                item.Identity = identity.Id;
                item.Version = identity.Version.ToString();
            }
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

        private void EmitWarningsForMissingPackages(IList<PackageItem> source, IList<PackageItem> csv, IList<string> errors)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!source[i].FoundInCsv)
                {
                    var msg = $@"No entry in the csv file matched the following package:
    {source[i].Name}";
                    LogWarning(msg, errors);
                }
            }

            for (int i = 0; i < csv.Count; i++)
            {
                if (!csv[i].FoundInFolder)
                {
                    var msg = $@"No package found in the source folder for the following csv entry:
    {csv[i].Name}";
                    LogWarning(msg, errors);
                }
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
                Logger.LogInformation($@"Copying package {package.Name} from
    {package.OriginPath} to
    {package.DestinationPath}");
                if (!_whatIf.HasValue())
                {
                    File.Copy(package.OriginPath, package.DestinationPath, overwrite: true);
                }
            }
        }

        private void LogWarning(string message, IList<string> errors)
        {
            if (_ignoreErrors.HasValue())
            {
                Logger.LogWarning(message);
            }

            errors.Add(message);
        }

        private class PackageItem : IEquatable<PackageItem>
        {
            public string Name { get; set; }
            public string OriginPath { get; set; }
            public string DestinationFolderName { get; set; }
            public string DestinationPath { get; set; }
            public bool FoundInCsv { get; set; }
            public bool FoundInFolder { get; set; }
            public string Identity { get; set; }
            public string Version { get; set; }

            public bool Equals(PackageItem other)
            {
                return string.Equals(OriginPath, other?.OriginPath);
            }

            public override int GetHashCode()
            {
                return OriginPath != null ? OriginPath.GetHashCode() : 1;
            }

            public override string ToString()
            {
                return $"Name {Name}, Identity {Identity}, Version {Version}, Csv {FoundInCsv}, Source {FoundInFolder}";
            }
        }

        private class Arguments
        {
            public string SourceFolder { get; set; }
            public string CsvFile { get; set; }
            public string DestinationFolder { get; set; }
        }

        private class ProjectJsonFileBuilder
        {
            private readonly string _path;
            private readonly bool _whatIf;
            private readonly bool _ignoreErrors;
            private readonly ILogger _logger;

            public ProjectJsonFileBuilder(
                string path,
                bool whatIf,
                bool ignoreErrors,
                ILogger logger)
            {
                _path = path;
                _whatIf = whatIf;
                _ignoreErrors = ignoreErrors;
                _logger = logger;
            }

            private IList<PackageItem> _dependencies;
            private IList<string> _frameworks = new List<string>();

            public void AddDependencies(IList<PackageItem> dependencies)
            {
                _dependencies = dependencies;
            }

            public void AddFramework(Framework fx)
            {
                switch (fx)
                {
                    case Framework.Net451:
                        _frameworks.Add("net451");
                        break;
                    default:
                        break;
                }
            }

            public void Execute()
            {
                var document = new JObject();
                document["dependencies"] = JObject.FromObject(CreateDependenciesDictionary());
                document["frameworks"] = JObject.FromObject(CreateFrameworksDictionary());

                var writer = _whatIf ? StreamWriter.Null : File.CreateText(_path);
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    _logger.LogInformation($"Writing project.json file to {_path}");
                    document.WriteTo(jsonWriter);

                    if (_whatIf)
                    {
                        _logger.LogInformation(document.ToString());
                    }
                }
            }

            private Dictionary<string, JObject> CreateFrameworksDictionary()
            {
                return _frameworks.ToDictionary(f => f, _ => new JObject());
            }

            private IDictionary<string, string> CreateDependenciesDictionary()
            {
                var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dependency in _dependencies)
                {
                    if (dictionary.ContainsKey(dependency.Identity))
                    {
                        var message = $"A duplicate dependency exists in {_path}, name = {dependency.Identity}";
                        if (_ignoreErrors)
                        {
                            throw new InvalidOperationException(message);
                        }

                        _logger.LogWarning(message);
                    }
                    else
                    {
                        dictionary[dependency.Identity] = dependency.Version;
                    }
                }

                return dictionary;
            }
        }

        public enum Framework
        {
            Net451
        }
    }
}
