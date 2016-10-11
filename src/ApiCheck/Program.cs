using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ApiCheck.Baseline;
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
                var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the baseline for", CommandOptionType.SingleValue);
                var lockFile = c.Option("-l|--lock", "Path to the lock file with the assembly dependencies", CommandOptionType.SingleValue);
                var nugetPackages = c.Option("-p|--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                var framework = c.Option("-f|--framework", "netcoreapp1.0 or net452", CommandOptionType.SingleValue);
                var publicOnly = c.Option("-po|--public-only", "Report only types visible outside of the assembly", CommandOptionType.NoValue);
                var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                var outputPath = c.Option("-o|--out", "Output path for the generated baseline file", CommandOptionType.SingleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnGenerate(c, assemblyPathOption, lockFile, nugetPackages, framework, publicOnly, noPublicInternal, outputPath));
            });

            var compareCommand = app.Command("compare", (c) =>
            {
                var baselinePathOption = c.Option("-b|--baseline", "Path to the baseline file to use as reference.", CommandOptionType.SingleValue);
                var assemblyPathOption = c.Option("-a|--assembly", "Path to the assembly to generate the baseline for", CommandOptionType.SingleValue);
                var lockFile = c.Option("-l|--lock", "Path to the lock file with the assembly dependencies", CommandOptionType.SingleValue);
                var nugetPackages = c.Option("-p|--packages", "Path to the nuget packages folder on the machine", CommandOptionType.SingleValue);
                var framework = c.Option("-f|--framework", "netcoreapp1.0 or net452", CommandOptionType.SingleValue);
                var publicOnly = c.Option("-po|--public-only", "Report only types visible outside of the assembly", CommandOptionType.NoValue);
                var noPublicInternal = c.Option("-epi|--exclude-public-internal", "Exclude types on the .Internal namespace from the generated report", CommandOptionType.NoValue);
                var outputPath = c.Option("-o|--out", "Output path for the generated baseline file", CommandOptionType.SingleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnCompare(c, baselinePathOption, assemblyPathOption, lockFile, nugetPackages, framework, publicOnly, noPublicInternal, outputPath));
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
            CommandOption lockFile,
            CommandOption packagesFolder,
            CommandOption framework,
            CommandOption publicOnly,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!assemblyPath.HasValue() || !lockFile.HasValue() || !framework.HasValue() || !output.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            if (framework.Value() != "netcoreapp1.0" && framework.Value() != "net452")
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
                lockFile.Value(),
                resolvedFramework,
                resolvedPackagesFolder);

            var filters = new List<Func<TypeInfo, bool>>();
            if (publicOnly.HasValue())
            {
                filters.Add(t => t.IsPublic || t.IsNestedPublic || t.IsNestedFamily);
            }

            if (excludeInternalNamespace.HasValue())
            {
                filters.Add(t => !t.Namespace.EndsWith("Internal"));
            }

            var report = BaselineGenerator.GenerateBaselineReport(assembly, filters);
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
            CommandOption baselinePathOption,
            CommandOption assemblyPath,
            CommandOption lockFile,
            CommandOption packagesFolder,
            CommandOption framework,
            CommandOption publicOnly,
            CommandOption excludeInternalNamespace,
            CommandOption output)
        {
            if (!baselinePathOption.HasValue() ||
                !assemblyPath.HasValue() ||
                !lockFile.HasValue() ||
                !framework.HasValue() ||
                !output.HasValue())
            {
                command.ShowHelp();
                return Error;
            }

            if (framework.Value() != "netcoreapp1.0" && framework.Value() != "net452")
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
                lockFile.Value(),
                resolvedFramework,
                resolvedPackagesFolder);

            var newBaselineFilters = new List<Func<TypeInfo, bool>>();
            var oldBaselineFilters = new List<Func<TypeBaseline, bool>>();
            if (publicOnly.HasValue())
            {
                newBaselineFilters.Add(t => t.IsPublic || t.IsNestedPublic || t.IsNestedFamily);
                oldBaselineFilters.Add(t =>
                    t.Visibility == BaselineVisibility.Public ||
                    t.Visibility == BaselineVisibility.Protected ||
                    t.Visibility == BaselineVisibility.ProtectedInternal);
            }

            if (excludeInternalNamespace.HasValue())
            {
                newBaselineFilters.Add(t => !t.Namespace.EndsWith(".Internal"));
                oldBaselineFilters.Add(t => !t.Name.Contains(".Internal"));
            }

            var oldBaseline = BaselineGenerator.LoadFrom(baselinePathOption.Value());
            foreach (var type in oldBaseline.Types)
            {
                if (oldBaselineFilters.Any(filter => filter(type)))
                {
                    oldBaseline.Types.Remove(type);
                }
            }

            var generator = new BaselineGenerator(assembly, newBaselineFilters);
            var newBaseline = generator.GenerateBaseline();

            var comparer = new BaselineComparer(
                oldBaseline,
                newBaseline,
                new List<Func<BreakingChangeCandidateContext, bool>>());

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
