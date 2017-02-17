// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public static class Program
    {
        private const int ReturnOk = 0;
        private const int ReturnBadArgs = 1;
        private const int ReturnErrorsOrWarnings = 2;

        public static int Main(string[] args)
        {
            // TODO: Show extraneous packages, exclusions, etc.
            var ignoreAssistanceMode = IgnoreAssistanceMode.None;

            // TODO: Get this from the command line
            var hideInfoLogs = true;

            if (args.Length > 0 && string.Equals("--verbose", args[0], StringComparison.OrdinalIgnoreCase))
            {
                hideInfoLogs = false;
                ignoreAssistanceMode = IgnoreAssistanceMode.ShowAll;
                args = args.Skip(1).ToArray();
            }

            if (args.Length < 1 || args.Length > 2 || args.Any(a => a == "--help"))
            {
                Console.WriteLine(@"USAGE: nugetverify c:\path\to\packages [c:\path\to\packages-to-scan.json]");

                return ReturnBadArgs;
            }

            IPackageVerifierLogger logger;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
            {
                logger = new TeamCityLogger(hideInfoLogs);
            }
            else
            {
                logger = new PackageVerifierLogger(hideInfoLogs);
            }

            IDictionary<string, PackageSet> packageSets = null;

            if (args.Length >= 2)
            {
                var packagesToScanJsonFilePath = args[1];
                if (!File.Exists(packagesToScanJsonFilePath))
                {
                    Console.WriteLine(packagesToScanJsonFilePath);
                    logger.LogError("Couldn't find packages JSON file at {0}", packagesToScanJsonFilePath);
                    return ReturnBadArgs;
                }

                var packagesToScanJsonFileContent = File.ReadAllText(packagesToScanJsonFilePath);

                packageSets = JsonConvert.DeserializeObject<IDictionary<string, PackageSet>>(
                    packagesToScanJsonFileContent,
                    new JsonSerializerSettings()
                    {
                        MissingMemberHandling = MissingMemberHandling.Error
                    });

                logger.LogNormal("Read {0} package set(s) from {1}", packageSets.Count, packagesToScanJsonFilePath);
            }
            else
            {
                packageSets = new Dictionary<string, PackageSet>();
            }

            var totalTimeStopWatch = Stopwatch.StartNew();

            var allRules = typeof(Program)
                    .GetTypeInfo()
                    .Assembly
                    .GetTypes()
                    .Where(t =>
                        typeof(IPackageVerifierRule).IsAssignableFrom(t)
                        && !t.GetTypeInfo().IsAbstract)
                    .ToDictionary(
                        t => t.Name,
                        t => (IPackageVerifierRule)Activator.CreateInstance(t));

            var nupkgsPath = args[0];
            var dirInfo = new DirectoryInfo(nupkgsPath);
            var allPackages = dirInfo.GetFiles("*.nupkg", SearchOption.TopDirectoryOnly);
            var nupkgs = allPackages.Where(n => !n.Name.EndsWith(".symbols.nupkg"));
            var packages = nupkgs.ToDictionary<FileInfo, IPackageMetadata>(file =>
            {
                var reader = new PackageArchiveReader(file.FullName);
                return new PackageBuilder(reader.GetNuspec(), basePath: null);
            });

            logger.LogNormal("Found {0} packages in {1}", nupkgs.Count(), nupkgsPath);

            var processedPackages = new HashSet<IPackageMetadata>();

            var totalErrors = 0;
            var totalWarnings = 0;

            var ignoreAssistanceData = new Dictionary<string, PackageVerifierOptions>(
                StringComparer.OrdinalIgnoreCase);

            IEnumerable<IPackageVerifierRule> defaultRuleSet = null;

            foreach (var packageSet in packageSets)
            {
                logger.LogInfo(
                    "Processing package set '{0}' with {1} package(s)",
                    packageSet.Key,
                    packageSet.Value.Packages?.Count ?? 0);

                var packageSetRuleInfo = packageSet.Value.Rules;

                var packageSetRules = packageSetRuleInfo.Select(
                    ruleId => allRules.SingleOrDefault(
                        rule => string.Equals(rule.Key, ruleId, StringComparison.OrdinalIgnoreCase)).Value);

                if (string.Equals(packageSet.Key, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    defaultRuleSet = packageSetRules;
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

                            List<PackageVerifierIssue> issues;
                            using (var context = new PackageAnalysisContext
                            {
                                PackageFileInfo = packagePair.Value,
                                Metadata = package,
                                Logger = logger,
                                Options = packageInfo.Value
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
                logger.LogNormal(
                    "Found {0} unlisted packages. Every package in the repo should be listed in exactly " +
                    "one package set. Running default or all rules on unlisted packages.",
                    unlistedPackages.Count());

                // For unlisted packages we run the rules from 'Default' package set if present
                // or we run all rules (because we have no idea what exactly to run)
                var analyzer = new PackageAnalyzer();
                foreach (var ruleInstance in defaultRuleSet ?? allRules.Values)
                {
                    analyzer.Rules.Add(ruleInstance);
                }

                var issueProcessor = new IssueProcessor(issuesToIgnore: null);

                foreach (var unlistedPackage in unlistedPackages)
                {
                    logger.LogInfo("Analyzing {0} ({1})", unlistedPackage.Id, unlistedPackage.Version);

                    List<PackageVerifierIssue> issues;
                    using (var context = new PackageAnalysisContext
                    {
                        PackageFileInfo = packages[unlistedPackage],
                        Metadata = unlistedPackage,
                        Logger = logger
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
            }

            if (ignoreAssistanceMode != IgnoreAssistanceMode.None)
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

            totalTimeStopWatch.Stop();
            logger.LogNormal("Total took {0}ms", totalTimeStopWatch.ElapsedMilliseconds);

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
                var infos = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Info).ToList();
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
                        PackageVerifierOptions options;
                        if (!ignoreAssistanceData.TryGetValue(package.Id, out options))
                        {
                            options = new PackageVerifierOptions();
                            ignoreAssistanceData.Add(package.Id, options);
                        }

                        IDictionary<string, string> packageRuleInfo;
                        if (!options.Exclusions.TryGetValue(issueToReport.PackageIssue.IssueId, out packageRuleInfo))
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
            else
            {
                logger.LogInfo("No issues found with package {0} ({1})", package.Id, package.Version);
                return new Tuple<int, int>(0, 0);
            }
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
                            Justification = justification,
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
