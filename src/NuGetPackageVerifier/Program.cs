﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;
using NuGetPackageVerifier.Rules;

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

            // TODO: Get this from the command line
            var ignoreAssistanceMode = IgnoreAssistanceMode.ShowAll;

            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine(@"USAGE: nugetverify c:\path\to\packages [c:\path\to\packages-to-scan.json]");
                Console.ReadLine();

                return ReturnBadArgs;
            }

            IPackageVerifierLogger logger;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
            {
                logger = new TeamCityLogger();
            }
            else
            {
                logger = new PackageVerifierLogger();
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

                logger.LogInfo("Read {0} package set(s) from {1}", packageSets.Count, packagesToScanJsonFilePath);
            }

            var totalTimeStopWatch = Stopwatch.StartNew();

            // TODO: Look this up using reflection or something
            var allRules = new IPackageVerifierRule[] {
                new AssemblyHasCommitHashAttributeRule(),
                new AssemblyHasCompanyAttributeRule(),
                new AssemblyHasCopyrightAttributeRule(),
                new AssemblyHasCorrectJsonNetVersionRule(),
                new AssemblyHasDescriptionAttributeRule(),
                new AssemblyHasDocumentFileRule(),
                new AssemblyHasNeutralResourcesLanguageAttributeRule(),
                new AssemblyHasProductAttributeRule(),
                new AssemblyHasServicingAttributeRule(),
                new AssemblyHasVersionAttributesRule(),
                new AssemblyStrongNameRule(),
                new AuthenticodeSigningRule(),
                new PowerShellScriptIsSignedRule(),
                new RequiredPackageMetadataRule(),
                new SatellitePackageRule(),
                new StrictSemanticVersionValidationRule(),
                //Composite rules
                new AdxVerificationCompositeRule(),
                new DefaultCompositeRule(),
                new NonAdxVerificationCompositeRule(),
                new SigningVerificationCompositeRule(),
            }.ToDictionary(t => t.GetType().Name, t => t);

            var nupkgsPath = args[0];
            var dirInfo = new DirectoryInfo(nupkgsPath);
            var allPackages = dirInfo.GetFiles("*.nupkg", SearchOption.TopDirectoryOnly);
            var nupkgs = allPackages.Where(n => !n.Name.EndsWith(".symbols.nupkg"));
            var packages = nupkgs.ToDictionary<FileInfo, IPackageMetadata>(file =>
            {
                var reader = new PackageArchiveReader(file.FullName);
                return new PackageBuilder(reader.GetNuspec(), basePath: null);
            });

            logger.LogInfo("Found {0} packages in {1}", nupkgs.Count(), nupkgsPath);

            var processedPackages = new HashSet<IPackageMetadata>();

            var totalErrors = 0;
            var totalWarnings = 0;

            var ignoreAssistanceData = new Dictionary<string, IDictionary<string, IDictionary<string, string>>>(
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


                foreach (var packageInfo in packageSet.Value.Packages)
                {
                    var packageId = packageInfo.Key;
                    var packageIgnoreInfo = packageInfo.Value;

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

                        var issues = analyzer.AnalyzePackage(packagePair.Value, package, logger).ToList();

                        var packageErrorsAndWarnings = ProcessPackageIssues(
                            ignoreAssistanceMode,
                            logger,
                            issueProcessor,
                            ignoreAssistanceData,
                            package,
                            issues);

                        totalErrors += packageErrorsAndWarnings.Item1;
                        totalWarnings += packageErrorsAndWarnings.Item2;

                        Console.WriteLine();

                        processedPackages.Add(package);
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

            var unprocessedPackages = packages.Keys.Except(processedPackages);

            if (unprocessedPackages.Any())
            {
                logger.LogWarning(
                    "Found {0} unprocessed packages. Every package in the repo should be listed in exactly " +
                    "one package set. Running default or all rules on unlisted packages.",
                    unprocessedPackages.Count());

                // For unprocessed packages we run the rules from 'Default' package set if present
                // or we run all rules (because we have no idea what exactly to run)
                var analyzer = new PackageAnalyzer();
                foreach (var ruleInstance in defaultRuleSet ?? allRules.Values)
                {
                    analyzer.Rules.Add(ruleInstance);
                }

                var issueProcessor = new IssueProcessor(issuesToIgnore: null);

                foreach (var unprocessedPackage in unprocessedPackages)
                {
                    logger.LogInfo("Analyzing {0} ({1})", unprocessedPackage.Id, unprocessedPackage.Version);

                    var issues = analyzer.AnalyzePackage(packages[unprocessedPackage], unprocessedPackage, logger).ToList();

                    var packageErrorsAndWarnings = ProcessPackageIssues(
                        ignoreAssistanceMode,
                        logger,
                        issueProcessor,
                        ignoreAssistanceData,
                        unprocessedPackage,
                        issues);

                    totalErrors += packageErrorsAndWarnings.Item1;
                    totalWarnings += packageErrorsAndWarnings.Item2;

                    Console.WriteLine();
                }
            }

            if (ignoreAssistanceMode != IgnoreAssistanceMode.None)
            {
                Console.WriteLine("Showing JSON for ignore content:");
                Console.WriteLine(JsonConvert.SerializeObject(ignoreAssistanceData, Formatting.Indented));
                Console.WriteLine();
            }

            var errorLevel = LogLevel.Info;
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
            logger.LogInfo("Total took {0}ms", totalTimeStopWatch.ElapsedMilliseconds);

            //Console.ReadLine();

            return (totalErrors + totalWarnings > 0) ? ReturnErrorsOrWarnings : ReturnOk;
        }

        private static Tuple<int, int> ProcessPackageIssues(
            IgnoreAssistanceMode ignoreAssistanceMode,
            IPackageVerifierLogger logger,
            IssueProcessor issueProcessor,
            Dictionary<string, IDictionary<string, IDictionary<string, string>>> ignoreAssistanceData,
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
                        IDictionary<string, IDictionary<string, string>> packageIgnoreInfo;
                        if (!ignoreAssistanceData.TryGetValue(package.Id, out packageIgnoreInfo))
                        {
                            packageIgnoreInfo = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                            ignoreAssistanceData.Add(package.Id, packageIgnoreInfo);
                        }
                        IDictionary<string, string> packageRuleInfo;
                        if (!packageIgnoreInfo.TryGetValue(issueToReport.PackageIssue.IssueId, out packageRuleInfo))
                        {
                            packageRuleInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            packageIgnoreInfo.Add(issueToReport.PackageIssue.IssueId, packageRuleInfo);
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

        private static IList<IssueIgnore> GetIgnoresFromFile(IDictionary<string, IDictionary<string, IDictionary<string, string>>> ignoresInFile)
        {
            var issuesToIgnore = new List<IssueIgnore>();
            if (ignoresInFile != null)
            {
                foreach (var packageIgnoreData in ignoresInFile)
                {
                    var packageId = packageIgnoreData.Key;
                    foreach (var ruleIgnoreData in packageIgnoreData.Value)
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
