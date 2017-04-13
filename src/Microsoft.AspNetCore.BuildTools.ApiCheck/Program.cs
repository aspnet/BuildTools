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

                app.Command("generate", c =>
                {
                    var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var assetsJson = c.Option("-p|--project", "Path to the project.assets.json file", CommandOptionType.SingleValue);
                    var framework = c.Option("-f|--framework", "The moniker for the framework the assembly to analize was compiled against.", CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                    var outputPath = c.Option("-o|--out", "Output path for the generated ApiListing file", CommandOptionType.SingleValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnGenerate(c, assemblyPathOption, assetsJson, framework, noPublicInternal, outputPath));
                });

                app.Command("compare", c =>
                {
                    var apiListingPathOption = c.Option("-b|--api-listing", "Path to the API listing file to use as reference.", CommandOptionType.SingleValue);
                    var exclusionsPathOption = c.Option("-e|--exclusions", "Path to the exclusions file for the ApiListing", CommandOptionType.SingleValue);
                    var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var assetsJson = c.Option("-p|--project", "Path to the project.assets.json file", CommandOptionType.SingleValue);
                    var framework = c.Option("-f|--framework", "The moniker for the framework the assembly to analize was compiled against.", CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                    var compactOutputOption = c.Option("--compact-output", "Display an error on a single line (primarily for use within MSBuild)", CommandOptionType.NoValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnCompare(c, apiListingPathOption, exclusionsPathOption, assemblyPathOption, assetsJson, framework, noPublicInternal, compactOutputOption));
                });

                app.HelpOption("-h|--help");

                app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return Ok;
                });

                return app.Execute(args);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine(e.ToString());
            }
            catch (ReflectionTypeLoadException e)
            {
                // ReflectionTypeLoadException does not override ToString() to include LoaderExceptions.
                Console.WriteLine($"{e.GetType().FullName}: {e.Message}");

                var hadLoaderExceptions = false;
                foreach (var loaderException in e.LoaderExceptions)
                {
                    hadLoaderExceptions = true;
                    Console.WriteLine($"  {loaderException.GetType().FullName}: {loaderException.Message}");

                    var innerException = loaderException.InnerException;
                    while (innerException != null)
                    {
                        Console.WriteLine($"  {innerException.GetType().FullName}: {innerException.Message}");
                        innerException = innerException.InnerException;
                    }

                    if (loaderException.InnerException != null)
                    {
                        Console.WriteLine("  --- End of inner exceptions ---");
                    }
                }

                if (hadLoaderExceptions)
                {
                    Console.WriteLine("  --- End of loader exceptions ---");
                }

                var inner = e.InnerException;
                while (inner != null)
                {
                    Console.WriteLine($"  {inner.GetType().FullName}: {inner.Message}");
                    inner = inner.InnerException;
                }

                if (e.InnerException != null)
                {
                    Console.WriteLine("  --- End of inner exceptions ---");
                }

                Console.WriteLine(e.StackTrace);
            }

            return Error;
        }

        private static int OnGenerate(
            CommandLineApplication command,
            CommandOption assemblyPath,
            CommandOption assetsJson,
            CommandOption framework,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!assemblyPath.HasValue() ||
                !output.HasValue() ||
                !assetsJson.HasValue() ||
                !framework.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                assetsJson.Value(),
                framework.Value());

            var filters = new List<Func<MemberInfo, bool>>();
            if (excludeInternalNamespace.HasValue())
            {
                filters.Add(ApiListingFilters.IsInInternalNamespace);
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
            CommandOption apiListingPathOption,
            CommandOption exclusionsPathOption,
            CommandOption assemblyPath,
            CommandOption assetsJson,
            CommandOption framework,
            CommandOption excludeInternalNamespace,
            CommandOption compactOutputOption)
        {
            if (!apiListingPathOption.HasValue() ||
                !assemblyPath.HasValue() ||
                !assetsJson.HasValue() ||
                !framework.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            var assembly = AssemblyLoader.LoadAssembly(
                assemblyPath.Value(),
                assetsJson.Value(),
                framework.Value());

            var newApiListingFilters = new List<Func<MemberInfo, bool>>();
            var oldApiListingFilters = new List<Func<ApiElement, bool>>();

            if (excludeInternalNamespace.HasValue())
            {
                newApiListingFilters.Add(ApiListingFilters.IsInInternalNamespace);
                oldApiListingFilters.Add(ApiListingFilters.IsInInternalNamespace);
            }

            var oldApiListing = ApiListingGenerator.LoadFrom(File.ReadAllText(apiListingPathOption.Value()), oldApiListingFilters);

            var generator = new ApiListingGenerator(assembly, newApiListingFilters);
            var newApiListing = generator.GenerateApiListing();
            var exclusions = !exclusionsPathOption.HasValue() ?
                Enumerable.Empty<ApiChangeExclusion>() :
                JsonConvert.DeserializeObject<IEnumerable<ApiChangeExclusion>>(File.ReadAllText(exclusionsPathOption.Value()));

            var comparer = new ApiListingComparer(oldApiListing, newApiListing, exclusions.ToList());

            var result = comparer.GetDifferences();
            var differences = result.BreakingChanges;

            var compactOutput = compactOutputOption.HasValue();
            if (!compactOutput)
            {
                Console.WriteLine();
            }

            foreach (var difference in differences)
            {
                if (compactOutput)
                {
                    Console.WriteLine($@"ERROR: Missing type or member '{difference}'");
                }
                else
                {
                    Console.WriteLine($@"ERROR: Missing type or member
    {difference}");
                    Console.WriteLine();
                }
            }

            var remainingExclusions = result.RemainingExclusions;
            foreach (var exclusion in remainingExclusions)
            {
                if (compactOutput)
                {
                    Console.WriteLine($"ERROR: Exclusion is no longer necessary: Old type '{exclusion.OldTypeId}' member " +
                        $"'{exclusion.OldMemberId}, New type '{exclusion.NewTypeId}' member '{exclusion.NewMemberId}'");
                }
                else
                {
                    Console.WriteLine($@"ERROR: The following exclusion is in the exclusion file, but is no longer necessary:
    Old type: {exclusion.OldTypeId}
    Old member: {exclusion.OldMemberId}
    New type: {exclusion.NewTypeId}
    New member: {exclusion.NewMemberId}");

                    Console.WriteLine();
                }
            }

            if (differences.Count > 0 || remainingExclusions.Count > 0)
            {
                if (!compactOutput && differences.Count > 0)
                {
                    Console.WriteLine("The process for breaking changes is described in: https://github.com/aspnet/Home/wiki/Engineering-guidelines#breaking-changes");
                    Console.WriteLine("For how to add an exclusion to Api-check go to: https://github.com/aspnet/BuildTools/wiki/Api-Check#apicheck-exceptions");
                }

                return Error;
            }

            return Ok;
        }
    }
}
