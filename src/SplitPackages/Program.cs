// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SplitPackages
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("help", StringComparer.OrdinalIgnoreCase) ||
                args.Length < 3 ||
                args.Length > 4)
            {
                PrintUsage();
                return;
            }

            bool whatif = args.Length == 4 && args[3].Equals("whatif", StringComparison.OrdinalIgnoreCase);

            try
            {
                var arguments = NormalizeArguments(args);
                var packagesFromSource = GetPackagesFromSourceFolder(arguments.SourceFolder);
                var packagesFromCsv = GetPackagesFromCsvFile(arguments.CsvFile);
                var expandedPackages = ExpandPackages(packagesFromCsv, arguments.SourceFolder);

                EnsureNoExtraSourcePackagesFound(packagesFromSource, expandedPackages);

                EmitWarningsForMissingPackages(packagesFromSource, packagesFromCsv);

                EnsureDestinationDirectories(
                    arguments.DestinationFolder,
                    expandedPackages.Select(e => e.DestinationFolderName).Distinct());

                SetDestinationPath(arguments, expandedPackages);

                CopyPackages(expandedPackages, whatif);
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return;
            }
        }

        private static Arguments NormalizeArguments(string[] args)
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

            if (!Directory.Exists(destinationFolder))
            {
                var msg = $@"Destination folder does not exist, creating folder at:
{destinationFolder}";
                WriteInformational(msg);
                Directory.CreateDirectory(destinationFolder);
            }

            return new Arguments
            {
                DestinationFolder = destinationFolder,
                CsvFile = csvFile,
                SourceFolder = sourcePackages
            };
        }

        private static IList<PackageItem> GetPackagesFromSourceFolder(string sourceFolder)
        {
            var packages = Directory.EnumerateFiles(sourceFolder);

            return packages.Select(p => new PackageItem
            {
                OriginPath = p,
                Name = Path.GetFileName(p),
                FoundInFolder = true
            }).ToList();
        }

        private static IList<PackageItem> GetPackagesFromCsvFile(string csvFile)
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

        private static IList<PackageItem> ExpandPackages(IList<PackageItem> packagesFromCsv, string sourceFolder)
        {
            var allPackages = new HashSet<PackageItem>();

            for (int i = 0; i < packagesFromCsv.Count; i++)
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

        private static void EnsureNoExtraSourcePackagesFound(IList<PackageItem> source, IList<PackageItem> expanded)
        {
            for (int i = 0; i < source.Count; i++)
            {
                for (int j = 0; j < expanded.Count; j++)
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

        private static void EmitWarningsForMissingPackages(IList<PackageItem> source, IList<PackageItem> csv)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!source[i].FoundInCsv)
                {
                    var msg = $@"No entry in the csv file matched the following package:
{source[i].Name}";
                    WriteWarning(msg);
                }
            }

            for (int i = 0; i < csv.Count; i++)
            {
                if (!csv[i].FoundInFolder)
                {
                    var msg = $@"No package found in the source folder for the following csv entry:
{csv[i].Name}";
                    WriteWarning(msg);
                }
            }
        }

        private static void EnsureDestinationDirectories(string destinationFolder, IEnumerable<string> folders)
        {
            foreach (var destination in folders)
            {
                var path = Path.Combine(destinationFolder, destination);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        private static void SetDestinationPath(Arguments arguments, IList<PackageItem> expandedPackages)
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

        private static void CopyPackages(IList<PackageItem> expandedPackages, bool whatif)
        {
            foreach (var package in expandedPackages)
            {
                WriteInformational($@"Copying package {package.Name} from
{package.OriginPath} to
{package.DestinationPath}");
                if (!whatif)
                {
                    File.Copy(package.OriginPath, package.DestinationPath, overwrite: true);
                }
            }
        }

        private static void WriteInformational(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[informational]: {msg}");
            Console.ResetColor();
        }

        private static void WriteWarning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[warning]: {msg}");
            Console.ResetColor();
        }

        private static void WriteError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($@"There was an error executing the command:
{error}");
            Console.ResetColor();
            PrintUsage();
        }

        private static void PrintUsage()
        {
            var text = @"
SplitPackages [help] [source packages] [csv path] [destination folder]

-help Prints out this summary.

-source packages: The path to the folder containing the built packages.

-csv path: The path to the CSV containing the packages and the destination
           folder for each package.

-destination folder: The path to the folder in which the destination folder for
           individual package categories will be created.";

            Console.WriteLine(text);
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
                return OriginPath?.GetHashCode() * 7 ?? 1;
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
