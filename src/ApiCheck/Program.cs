// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace ApiCheck
{
    public class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();

            var generateCommand = app.Command("generate", (c) =>
            {
                var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                var projectDirectory = c.Option("-pd|--project-directory", "Path to the project.json directory", CommandOptionType.SingleValue);
                var lockFile = c.Option("-l|--lock", "Path to the lock file with the assembly dependencies", CommandOptionType.SingleValue);
                var nugetPackages = c.Option("-p|--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                var framework = c.Option("-f|--framework", "netcoreapp1.0 or net452", CommandOptionType.SingleValue);
                var configuration = c.Option("-c|--configuration", "Debug or Release", CommandOptionType.SingleValue);
                var publicOnly = c.Option("-po|--public-only", "Report only types visible outside of the assembly", CommandOptionType.NoValue);
                var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                var outputPath = c.Option("-o|--out", "Output path for the generated ApiListing file", CommandOptionType.SingleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnGenerate(c, assemblyPathOption, projectDirectory, lockFile, nugetPackages, framework, configuration, publicOnly, noPublicInternal, outputPath));
            });

            var compareCommand = app.Command("compare", (c) =>
            {
                var ApiListingPathOption = c.Option("-b|--ApiListing", "Path to the ApiListing file to use as reference.", CommandOptionType.SingleValue);
                var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                var projectDirectory = c.Option("-pd|--project-directory", "Path to the project.json directory", CommandOptionType.SingleValue);
                var lockFile = c.Option("-l|--lock", "Path to the lock file with the assembly dependencies", CommandOptionType.SingleValue);
                var nugetPackages = c.Option("-p|--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                var framework = c.Option("-f|--framework", "netcoreapp1.0 or net452", CommandOptionType.SingleValue);
                var configuration = c.Option("-c|--configuration", "Debug or Release", CommandOptionType.SingleValue);
                var publicOnly = c.Option("-po|--public-only", "Report only types visible outside of the assembly", CommandOptionType.NoValue);
                var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                var outputPath = c.Option("-o|--out", "Output path for the generated ApiListing file", CommandOptionType.SingleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnCompare(c, ApiListingPathOption, assemblyPathOption, projectDirectory, lockFile, nugetPackages, framework, configuration, publicOnly, noPublicInternal, outputPath));
            });

            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return Ok;
            });

            app.Execute(args);
        }

        private static int OnGenerate(
            CommandLineApplication command,
            CommandOption assemblyPath,
            CommandOption projectDirectory,
            CommandOption lockFile,
            CommandOption packagesFolder,
            CommandOption framework,
            CommandOption configuration,
            CommandOption publicOnly,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!assemblyPath.HasValue() || !lockFile.HasValue() || !framework.HasValue() ||
                !output.HasValue() || !projectDirectory.HasValue() || !configuration.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            if (framework.Value() != "netcoreapp1.0" && framework.Value() != "net452")
            {
                command.ShowHelp();
                return Error;
            }

            if (configuration.Value() != "Debug" && configuration.Value() != "Release")
            {
                command.ShowHelp();
                return Error;
            }

            var resolvedPackagesFolder = packagesFolder.Value() ??
                $"{Environment.ExpandEnvironmentVariables("%userprofile%")}/.nuget/packages";

            var resolvedFramework = framework.Value() == "netcoreapp1.0" ?
                FrameworkConstants.CommonFrameworks.NetCoreApp10 :
                FrameworkConstants.CommonFrameworks.Net452;

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                projectDirectory.Value(),
                lockFile.Value(),
                resolvedFramework,
                configuration.Value(),
                resolvedPackagesFolder);

            var filters = new List<Func<MemberInfo, bool>>();
            if (publicOnly.HasValue())
            {
                filters.Add(ApiListingFilters.NonExportedMembers);
            }

            if (excludeInternalNamespace.HasValue())
            {
                filters.Add(ApiListingFilters.InternalNamespaceTypes);
            }

            var report = ApiListingGenerator.GenerateApiListingReport(assembly, filters);
            using (var writer = new JsonTextWriter(File.CreateText(output.Value())))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 2;
                writer.IndentChar = ' ';

                report.WriteTo(writer);
            }

            return Ok;
        }

        private static int OnCompare(
            CommandLineApplication command,
            CommandOption ApiListingPathOption,
            CommandOption assemblyPath,
            CommandOption projectDirectory,
            CommandOption lockFile,
            CommandOption packagesFolder,
            CommandOption framework,
            CommandOption configuration,
            CommandOption publicOnly,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!ApiListingPathOption.HasValue() ||
                !assemblyPath.HasValue() ||
                !lockFile.HasValue() ||
                !framework.HasValue() ||
                !output.HasValue() ||
                !projectDirectory.HasValue() ||
                !configuration.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            if (framework.Value() != "netcoreapp1.0" && framework.Value() != "net452")
            {
                command.ShowHelp();
                return Error;
            }

            if (configuration.Value() != "Debug" && configuration.Value() != "Release")
            {
                command.ShowHelp();
                return Error;
            }

            var logger = CreateLogger();

            var resolvedPackagesFolder = packagesFolder.Value() ??
                $"{Environment.ExpandEnvironmentVariables("%userprofile%")}/.nuget/packages";

            var resolvedFramework = framework.Value() == "netcoreapp1.0" ?
                FrameworkConstants.CommonFrameworks.NetCoreApp10 :
                FrameworkConstants.CommonFrameworks.Net452;

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                projectDirectory.Value(),
                lockFile.Value(),
                resolvedFramework,
                configuration.Value(),
                resolvedPackagesFolder);

            var newApiListingFilters = new List<Func<MemberInfo, bool>>();
            var oldApiListingFilters = new List<Func<ApiElement, bool>>();
            if (publicOnly.HasValue())
            {
                newApiListingFilters.Add(ApiListingFilters.NonExportedMembers);
                oldApiListingFilters.Add(ApiListingFilters.NonExportedMembers);
            }

            if (excludeInternalNamespace.HasValue())
            {
                newApiListingFilters.Add(ApiListingFilters.InternalNamespaceTypes);
                oldApiListingFilters.Add(ApiListingFilters.InternalNamespaceTypes);
            }

            var oldApiListing = ApiListingGenerator.LoadFrom(File.ReadAllText(ApiListingPathOption.Value()), oldApiListingFilters);

            var generator = new ApiListingGenerator(assembly, newApiListingFilters);
            var newApiListing = generator.GenerateApiListing();

            var comparer = new ApiListingComparer(oldApiListing, newApiListing);

            var differences = comparer.GetDifferences();
            foreach (var difference in differences)
            {
                logger.LogInformation($@"Missing class or member on {difference}");
            }

            if (differences.Count > 0)
            {
                return Error;
            }

            return Ok;
        }

        private static ILogger CreateLogger()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Information, includeScopes: false);

            return loggerFactory.CreateLogger<Program>();
        }
    }
}
