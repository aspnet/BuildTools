// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;
using NuGetPackageVerifier.Manifests;

namespace NuGetPackageVerifier
{
    public static class Program
    {
        private const int ReturnOk = 0;
        private const int ReturnBadArgs = 1;
        private const int ReturnErrorsOrWarnings = 2;

        public static int Main(string[] args)
        {
            var application = new CommandLineApplication();
            var verbose = application.Option("--verbose", "Verbose output and assistance", CommandOptionType.NoValue);
            var ruleFile = application.Option("--rule-file", "Path to NPV.json", CommandOptionType.SingleValue);
            var excludedRules = application.Option("--excluded-rule", "Rules to exclude. Calculcated after composite rules are evaluated.", CommandOptionType.MultipleValue);
            var signRequest = application.Option("--sign-request", "Sign request manifest file.", CommandOptionType.SingleValue);
            var packageDirectory = application.Argument("Package directory", "Package directory to scan for nupkgs");

            application.OnExecute(() =>
            {
                var totalTimeStopWatch = Stopwatch.StartNew();
                if (string.IsNullOrEmpty(packageDirectory.Value))
                {
                    application.Error.WriteLine($"Missing required argument {packageDirectory.Name}");
                    application.ShowHelp();
                    return ReturnBadArgs;
                }

                if (!ruleFile.HasValue())
                {
                    application.Error.WriteAsync($"Missing required option {ruleFile.Template}.");
                    application.ShowHelp();
                    return ReturnBadArgs;
                }

                var hideInfoLogs = verbose.HasValue();

                IPackageVerifierLogger logger;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
                {
                    logger = new TeamCityLogger(hideInfoLogs);
                }
                else
                {
                    logger = new PackageVerifierLogger(hideInfoLogs);
                }

                // TODO: Show extraneous packages, exclusions, etc.
                var ignoreAssistanceMode = verbose.HasValue() ? IgnoreAssistanceMode.ShowAll : IgnoreAssistanceMode.ShowNew;

                var ruleFileContent = File.ReadAllText(ruleFile.Value());
                var packageSets = JsonConvert.DeserializeObject<IDictionary<string, PackageSet>>(
                    ruleFileContent,
                    new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Error
                    });


                var signRequestManifest = signRequest.HasValue()
                    ? SignRequestManifest.Parse(signRequest.Value())
                    : default;

                logger.LogNormal("Read {0} package set(s) from {1}", packageSets.Count, ruleFile.Value());
                var nupkgs = new DirectoryInfo(packageDirectory.Value).EnumerateFiles("*.nupkg", SearchOption.TopDirectoryOnly)
                    .Where(p => !p.Name.EndsWith(".symbols.nupkg"))
                    .ToArray();
                logger.LogNormal("Found {0} packages in {1}", nupkgs.Length, packageDirectory.Value);
                var exitCode = Execute(packageSets, nupkgs, signRequestManifest, excludedRules.Values, logger, ignoreAssistanceMode);
                totalTimeStopWatch.Stop();
                logger.LogNormal("Total took {0}ms", totalTimeStopWatch.ElapsedMilliseconds);

                return exitCode;
            });

            return application.Execute(args);
        }

        private static int Execute(
            IDictionary<string, PackageSet> packageSets,
            IEnumerable<FileInfo> nupkgs,
            SignRequestManifest signRequestManifest,
            List<string> excludedRuleNames,
            IPackageVerifierLogger logger,
            IgnoreAssistanceMode ignoreAssistanceMode)
        {
            var allRules = typeof(Program).Assembly.GetTypes()
                .Where(t =>
                    typeof(IPackageVerifierRule).IsAssignableFrom(t) && !t.IsAbstract)
                .ToDictionary(
                    t => t.Name,
                    t =>
                    {
                        var rule = (IPackageVerifierRule)Activator.CreateInstance(t);
                        if (rule is CompositeRule compositeRule)
                        {
                            return compositeRule.GetRules();
                        }
                        else
                        {
                            return new[] { rule };
                        }
                    });

            var packages = nupkgs.ToDictionary(file =>
            {
                var reader = new PackageArchiveReader(file.FullName);
                return (IPackageMetadata)new PackageBuilder(reader.GetNuspec(), basePath: null);
            });

            var processedPackages = new HashSet<IPackageMetadata>();

            var totalErrors = 0;
            var totalWarnings = 0;

            var ignoreAssistanceData = new Dictionary<string, PackageVerifierOptions>(
                StringComparer.OrdinalIgnoreCase);

            PackageSet defaultPackageSet = null;
            IEnumerable<IPackageVerifierRule> defaultRuleSet = null;
            IEnumerable<IssueIgnore> defaultIssuesToIgnore = null;

            foreach (var packageSet in packageSets)
            {
                logger.LogInfo(
                    "Processing package set '{0}' with {1} package(s)",
                    packageSet.Key,
                    packageSet.Value.Packages?.Count ?? 0);

                var packageSetRuleInfo = packageSet.Value.Rules;

                var packageSetRules = packageSetRuleInfo.SelectMany(
                    ruleId => allRules.SingleOrDefault(
                        rule => string.Equals(rule.Key, ruleId, StringComparison.OrdinalIgnoreCase)).Value)
                    .Where(rule => !excludedRuleNames.Contains(rule.GetType().Name));

                if (string.Equals(packageSet.Key, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    defaultPackageSet = packageSet.Value;
                    defaultRuleSet = packageSetRules;
                    defaultIssuesToIgnore = GetIgnoresFromFile(packageSet.Value.Packages);
                    continue;
                }

                var analyzer = new PackageAnalyzer();
                foreach (var ruleInstance in packageSetRules)
                {
                    analyzer.Rules.Add(ruleInstance);
                }

                var issuesToIgnore = GetIgnoresFromFile(packageSet.Value.Packages);

                var issueProcessor = new IssueProcessor(issuesToIgnore);

                if (packageSet.Value.Packages != null)
                {
                    foreach (var packageInfo in packageSet.Value.Packages)
                    {
                        var packageId = packageInfo.Key;

                        var packagesWithId = packages.Where(p => p.Key.Id.Equals(packageId));
                        if (!packagesWithId.Any())
                        {
                            logger.LogError("Couldn't find package '{0}' in the repo", packageId);
                            totalErrors++;
                            continue;
                        }

                        foreach (var packagePair in packagesWithId)
                        {
                            var package = packagePair.Key;
                            logger.LogInfo("Analyzing {0} ({1})", package.Id, package.Version);

                            PackageSignRequest signRequest = null;
                            signRequestManifest?.PackageSignRequests.TryGetValue(packagePair.Value.FullName, out signRequest);

                            List<PackageVerifierIssue> issues;
                            using (var context = new PackageAnalysisContext
                            {
                                PackageFileInfo = packagePair.Value,
                                Metadata = package,
                                Logger = logger,
                                Options = packageInfo.Value,
                                SignRequest = signRequest,
                            })
                            {
                                issues = analyzer.AnalyzePackage(context).ToList();
                            }

                            var packageErrorsAndWarnings = ProcessPackageIssues(
                                ignoreAssistanceMode,
                                logger,
                                issueProcessor,
                                ignoreAssistanceData,
                                package,
                                issues);

                            totalErrors += packageErrorsAndWarnings.Item1;
                            totalWarnings += packageErrorsAndWarnings.Item2;

                            processedPackages.Add(package);
                        }
                    }
                }

                foreach (var issue in issueProcessor.RemainingIssuesToIgnore)
                {
                    // TODO: Don't show this for rules that we don't run.
                    logger.LogWarning(
                        "Unnecessary exclusion in {0}{3}Issue: {1}{3}Instance: {2}{3}",
                        issue.PackageId,
                        issue.IssueId,
                        issue.Instance,
                        Environment.NewLine);
                }
            }

            var unlistedPackages = packages.Keys.Except(processedPackages);

            if (unlistedPackages.Any())
            {
                // For unlisted packages we run the rules from 'Default' package set if present
                // or we run all rules (because we have no idea what exactly to run)
                var analyzer = new PackageAnalyzer();
                var unlistedPackageRules = defaultRuleSet ??
                    allRules.Values.SelectMany(f => f).Where(r => !excludedRuleNames.Contains(r.GetType().Name));

                foreach (var ruleInstance in unlistedPackageRules)
                {
                    analyzer.Rules.Add(ruleInstance);
                }

                var issueProcessor = new IssueProcessor(issuesToIgnore: defaultIssuesToIgnore);

                foreach (var unlistedPackage in unlistedPackages)
                {
                    logger.LogInfo("Analyzing {0} ({1})", unlistedPackage.Id, unlistedPackage.Version);

                    PackageSignRequest signRequest = null;
                    signRequestManifest?.PackageSignRequests.TryGetValue(packages[unlistedPackage].FullName, out signRequest);

                    List<PackageVerifierIssue> issues;
                    PackageVerifierOptions packageOptions = null;
                    defaultPackageSet?.Packages?.TryGetValue(unlistedPackage.Id, out packageOptions);

                    using (var context = new PackageAnalysisContext
                    {
                        PackageFileInfo = packages[unlistedPackage],
                        Metadata = unlistedPackage,
                        Logger = logger,
                        SignRequest = signRequest,
                        Options = packageOptions,
                    })
                    {
                        issues = analyzer.AnalyzePackage(context).ToList();
                    }

                    var packageErrorsAndWarnings = ProcessPackageIssues(
                        ignoreAssistanceMode,
                        logger,
                        issueProcessor,
                        ignoreAssistanceData,
                        unlistedPackage,
                        issues);

                    totalErrors += packageErrorsAndWarnings.Item1;
                    totalWarnings += packageErrorsAndWarnings.Item2;
                }

                foreach (var issue in issueProcessor.RemainingIssuesToIgnore)
                {
                    // TODO: Don't show this for rules that we don't run.
                    logger.LogWarning(
                        "Unnecessary exclusion in {0}{3}Issue: {1}{3}Instance: {2}{3}",
                        issue.PackageId,
                        issue.IssueId,
                        issue.Instance,
                        Environment.NewLine);
                }
            }

            if (ignoreAssistanceMode != IgnoreAssistanceMode.None && ignoreAssistanceData.Any())
            {
                Console.WriteLine("Showing JSON for ignore content:");
                Console.WriteLine(JsonConvert.SerializeObject(ignoreAssistanceData,
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }));
                Console.WriteLine();
            }

            var errorLevel = LogLevel.Normal;
            if (totalWarnings > 0)
            {
                errorLevel = LogLevel.Warning;
            }
            if (totalErrors > 0)
            {
                errorLevel = LogLevel.Error;
            }
            logger.Log(
                errorLevel,
                "SUMMARY: {0} error(s) and {1} warning(s) found",
                totalErrors, totalWarnings);



            return (totalErrors + totalWarnings > 0) ? ReturnErrorsOrWarnings : ReturnOk;
        }

        private static Tuple<int, int> ProcessPackageIssues(
            IgnoreAssistanceMode ignoreAssistanceMode,
            IPackageVerifierLogger logger,
            IssueProcessor issueProcessor,
            Dictionary<string, PackageVerifierOptions> ignoreAssistanceData,
            IPackageMetadata package,
            List<PackageVerifierIssue> issues)
        {
            var issuesToReport = issues.Select(issue => issueProcessor.GetIssueReport(issue, package)).ToList();

            if (issuesToReport.Count > 0)
            {
                var warnings = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Warning).ToList();
                var errors = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Error).ToList();

                var errorLevel = LogLevel.Info;
                if (warnings.Count > 0)
                {
                    errorLevel = LogLevel.Warning;
                }
                if (errors.Count > 0)
                {
                    errorLevel = LogLevel.Error;
                }
                logger.Log(
                    errorLevel,
                    "{0} error(s) and {1} warning(s) found with package {2} ({3})",
                    errors.Count,
                    warnings.Count,
                    package.Id,
                    package.Version);

                foreach (var issueToReport in issuesToReport)
                {
                    // If requested, track ignores to assist
                    if (ignoreAssistanceMode == IgnoreAssistanceMode.ShowAll ||
                        (ignoreAssistanceMode == IgnoreAssistanceMode.ShowNew && issueToReport.IgnoreJustification == null))
                    {
                        if (!ignoreAssistanceData.TryGetValue(package.Id, out var options))
                        {
                            options = new PackageVerifierOptions();
                            ignoreAssistanceData.Add(package.Id, options);
                        }

                        if (!options.Exclusions.TryGetValue(issueToReport.PackageIssue.IssueId, out var packageRuleInfo))
                        {
                            packageRuleInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            options.Exclusions.Add(issueToReport.PackageIssue.IssueId, packageRuleInfo);
                        }
                        if (packageRuleInfo.ContainsKey(issueToReport.PackageIssue.Instance ?? "*"))
                        {
                            Console.WriteLine("ALERT!!!!!!!!!!!!! Already added key {0}", issueToReport.PackageIssue.Instance);
                        }
                        else
                        {
                            packageRuleInfo.Add(issueToReport.PackageIssue.Instance ?? "*", issueToReport.IgnoreJustification ?? "Enter justification");
                        }
                    }

                    PrintPackageIssue(logger, issueToReport);
                }

                return new Tuple<int, int>(errors.Count, warnings.Count);
            }
            logger.LogInfo("No issues found with package {0} ({1})", package.Id, package.Version);
            return new Tuple<int, int>(0, 0);
        }

        private static IEnumerable<IssueIgnore> GetIgnoresFromFile(IDictionary<string, PackageVerifierOptions> ignoresInFile)
        {
            if (ignoresInFile == null)
            {
                return Enumerable.Empty<IssueIgnore>();
            }

            var issuesToIgnore = new List<IssueIgnore>();

            foreach (var packageIgnoreData in ignoresInFile)
            {
                var packageId = packageIgnoreData.Key;
                if (packageIgnoreData.Value == null)
                {
                    continue;
                }
                foreach (var ruleIgnoreData in packageIgnoreData.Value.Exclusions)
                {
                    var issueId = ruleIgnoreData.Key;
                    foreach (var instanceIgnoreData in ruleIgnoreData.Value)
                    {
                        var instance = instanceIgnoreData.Key;
                        var justification = instanceIgnoreData.Value;

                        issuesToIgnore.Add(new IssueIgnore
                        {
                            PackageId = packageId,
                            IssueId = issueId,
                            Instance = instance,
                            Justification = justification
                        });
                    }
                }
            }

            return issuesToIgnore;
        }

        private static void PrintPackageIssue(IPackageVerifierLogger logger, IssueReport issue)
        {
            var issueInfo = issue.PackageIssue.IssueId;
            if (issue.PackageIssue.Instance != null)
            {
                issueInfo += " @ " + issue.PackageIssue.Instance;
            }

            logger.Log(issue.IssueLevel, "{0}: {1}", issueInfo, issue.PackageIssue.Issue);
            if (issue.IgnoreJustification != null)
            {
                logger.Log(issue.IssueLevel, "Justification: {0}", issue.IgnoreJustification);
            }
        }
    }
}
