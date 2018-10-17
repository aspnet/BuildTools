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
                var app = new CommandLineApplication
                {
                    Name = "ApiCheck"
                };

                app.Command("generate", c =>
                {
                    var assemblyPathOption = c.Option("-a|--assembly",
                        "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var assetsJson = c.Option("-p|--project", "Path to the project.assets.json file",
                        CommandOptionType.SingleValue);
                    var framework = c.Option("-f|--framework",
                        "The moniker for the framework the assembly to analize was compiled against.",
                        CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal",
                        "Exclude types on the .Internal namespace from the generated report",
                        CommandOptionType.NoValue);
                    var outputPath = c.Option("-o|--out", "Output path for the generated ApiListing file",
                        CommandOptionType.SingleValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnGenerate(c, assemblyPathOption, assetsJson, framework, noPublicInternal,
                        outputPath));
                });

                app.Command("compare", c =>
                {
                    var apiListingPathOption = c.Option("-b|--api-listing",
                        "Path to the API listing file to use as reference.", CommandOptionType.SingleValue);
                    var exclusionsPathOption = c.Option("-e|--exclusions",
                        "Path to the exclusions file for the ApiListing", CommandOptionType.SingleValue);
                    var assemblyPathOption = c.Option("-a|--assembly",
                        "Path to the assembly to generate the ApiListing for", CommandOptionType.SingleValue);
                    var assetsJson = c.Option("-p|--project", "Path to the project.assets.json file",
                        CommandOptionType.SingleValue);
                    var framework = c.Option("-f|--framework",
                        "The moniker for the framework the assembly to analize was compiled against.",
                        CommandOptionType.SingleValue);
                    var noPublicInternal = c.Option("-epi|--exclude-public-internal",
                        "Exclude types on the .Internal namespace from the generated report",
                        CommandOptionType.NoValue);

                    c.HelpOption("-h|--help");

                    c.OnExecute(() => OnCompare(c, apiListingPathOption, exclusionsPathOption, assemblyPathOption,
                        assetsJson, framework, noPublicInternal));
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
                Console.Error.WriteLine("Missing required option.");
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
            CommandOption breakingChangesPathOption,
            CommandOption assemblyPath,
            CommandOption assetsJson,
            CommandOption framework,
            CommandOption excludeInternalNamespace)
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

            var oldApiListing = ApiListingGenerator.LoadFrom(File.ReadAllText(apiListingPathOption.Value()),
                oldApiListingFilters);

            var generator = new ApiListingGenerator(assembly, newApiListingFilters);
            var newApiListing = generator.GenerateApiListing();
            var knownBreakingChanges = (breakingChangesPathOption.HasValue()
                                           ? JsonConvert.DeserializeObject<IList<BreakingChange>>(
                                               File.ReadAllText(breakingChangesPathOption.Value()))
                                           : null) ?? new List<BreakingChange>();

            var comparer = new ApiListingComparer(oldApiListing, newApiListing);

            var breakingChanges = comparer.GetDifferences();
            var newBreakingChanges = breakingChanges.Except(knownBreakingChanges).ToList();
            var incorrectBreakingChanges = knownBreakingChanges.Except(breakingChanges).ToList();

            const string indent = "    ";

            var breakingChangesToPrint = new List<BreakingChange>();

            if (newBreakingChanges.Count > 0)
            {
                Console.WriteLine(
                    $"ERROR: Verifying breaking changes for framework {framework.Value()} failed.");
            }

            var removedTypes = newBreakingChanges.Where(b => b.MemberId == null).OrderBy(b => b.TypeId).ToList();
            if (removedTypes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("The following types have been removed.");
                Console.WriteLine();
                Console.WriteLine(string.Join(Environment.NewLine, removedTypes.Select(b => indent + b.TypeId)));
                Console.WriteLine();
            }

            breakingChangesToPrint.AddRange(removedTypes);

            var removedMembers = newBreakingChanges
                .Where(b => b.MemberId != null && b.Kind == ChangeKind.Removal)
                .OrderBy(b => b.MemberId)
                .GroupBy(b => b.TypeId)
                .ToList();
            if (removedMembers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("The following types have one or more members removed from them.");
                Console.WriteLine();

                foreach (var memberGrouping in removedMembers)
                {
                    Console.WriteLine(indent + memberGrouping.Key);
                    Console.WriteLine(string.Join(Environment.NewLine,
                        memberGrouping.Select(b => indent + indent + b.MemberId).ToList()));
                    Console.WriteLine();
                }
            }

            breakingChangesToPrint.AddRange(removedMembers.SelectMany(t => t.ToList()));

            var newMembersOnInterfaces = newBreakingChanges
                .Where(b => b.MemberId != null && b.Kind == ChangeKind.Addition)
                .OrderBy(b => b.MemberId)
                .GroupBy(b => b.TypeId)
                .ToList();
            if (newMembersOnInterfaces.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("The following interfaces have one or more members added to them.");

                foreach (var memberGrouping in newMembersOnInterfaces)
                {
                    Console.WriteLine(indent + memberGrouping.Key);
                    Console.WriteLine(string.Join(Environment.NewLine,
                        memberGrouping.Select(b => indent + indent + b.MemberId).ToList()));
                    Console.WriteLine();
                }
            }

            breakingChangesToPrint.AddRange(newMembersOnInterfaces.SelectMany(t => t.ToList()));

            foreach (var exclusion in incorrectBreakingChanges)
            {
                if (breakingChangesPathOption.HasValue())
                {
                    Console.Error.WriteLine(
                        $"ERROR: The following exclusion is in the exclusion file '{breakingChangesPathOption.Value()}', but is no longer necessary:");
                }
                else
                {
                    Console.Error.WriteLine(
                        "ERROR: The following exclusion is in the exclusion file, but is no longer necessary:");
                }
                Console.WriteLine(JsonConvert.SerializeObject(exclusion, Formatting.Indented));
                Console.WriteLine();
            }

            if (breakingChangesToPrint.Any())
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Following is the list of exclusions that either need to be added to the list of breaking changes, or the breaking changes themselves need to be reverted:");
                Console.WriteLine(
                    JsonConvert.SerializeObject(
                        breakingChangesToPrint,
                        Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        }));
                Console.WriteLine();
            }

            if (newBreakingChanges.Count > 0 || incorrectBreakingChanges.Count > 0)
            {
                Console.WriteLine(
                    "The process for breaking changes is described in: https://github.com/aspnet/AspNetCore/wiki/Engineering-guidelines#breaking-changes");
                Console.WriteLine(
                    "The process to add an exclusion to this tool is described in: https://github.com/aspnet/BuildTools/wiki/Api-Check#apicheck-exceptions");
                Console.WriteLine();

                return Error;
            }

            return Ok;
        }
    }
}
