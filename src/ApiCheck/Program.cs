// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace ApiCheck
{
    public class Program
    {
        private const int Ok = 0;
        private const int Error = 1;

        public static int Main(string[] args)
        {
            try
            {
                var app = new CommandLineApplication();

                var generateCommand = app.Command("generate", (c) =>
                {
                    var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var projectJson = c.Option("-p|--project", "Path to the project.json file", CommandOptionType.SingleValue);
                    var nugetPackages = c.Option("--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                    var configuration = c.Option("-c|--configuration", "Debug or Release", CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                    var outputPath = c.Option("-o|--out", "Output path for the generated ApiListing file", CommandOptionType.SingleValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnGenerate(c, assemblyPathOption, projectJson, nugetPackages, configuration, noPublicInternal, outputPath));
                });

                var compareCommand = app.Command("compare", (c) =>
                {
                    var apiListingPathOption = c.Option("-b|--ApiListing", "Path to the ApiListing file to use as reference.", CommandOptionType.SingleValue);
                    var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var projectJson = c.Option("-p|--project", "Path to the project.json file", CommandOptionType.SingleValue);
                    var nugetPackages = c.Option("--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                    var configuration = c.Option("-c|--configuration", "Debug or Release", CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnCompare(c, apiListingPathOption, assemblyPathOption, projectJson, nugetPackages, configuration, noPublicInternal));
                });

                app.HelpOption("-h|--help");

                app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return Ok;
                });

                return app.Execute(args);

            }
            catch (ReflectionTypeLoadException e)
            {
                foreach (var rtle in e.LoaderExceptions)
                {
                    Console.WriteLine(rtle.Message);
                    var innerException = rtle.InnerException;
                    while (innerException != null)
                    {
                        Console.WriteLine(innerException.Message);
                        innerException = innerException.InnerException;
                    }
                }

                var inner = e.InnerException;
                while (inner != null)
                {
                    Console.WriteLine(inner.InnerException.Message);
                    inner = inner.InnerException;
                }

                return Error;
            }
        }

        private static int OnGenerate(
            CommandLineApplication command,
            CommandOption assemblyPath,
            CommandOption projectJson,
            CommandOption packagesFolder,
            CommandOption configuration,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!assemblyPath.HasValue() || !output.HasValue() || !projectJson.HasValue() || !configuration.HasValue())
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

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                projectJson.Value(),
                configuration.Value(),
                resolvedPackagesFolder);

            var filters = new List<Func<MemberInfo, bool>>();
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
            CommandOption projectJson,
            CommandOption packagesFolder,
            CommandOption configuration,
            CommandOption excludeInternalNamespace)
        {
            if (!ApiListingPathOption.HasValue() ||
                !assemblyPath.HasValue() ||
                !projectJson.HasValue() ||
                !configuration.HasValue())
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

#if NETCOREAPP1_0
            var resolvedFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
#else
            var resolvedFramework = FrameworkConstants.CommonFrameworks.Net452;
#endif

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                projectJson.Value(),
                configuration.Value(),
                resolvedPackagesFolder);

            var newApiListingFilters = new List<Func<MemberInfo, bool>>();
            var oldApiListingFilters = new List<Func<ApiElement, bool>>();

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

            Console.WriteLine();
            foreach (var difference in differences)
            {
                Console.WriteLine($@"Missing class or member on:
    {difference}");
                Console.WriteLine();
            }

            if (differences.Count > 0)
            {
                Console.WriteLine("Returning error");
                return Error;
            }

            return Ok;
        }
    }
}
