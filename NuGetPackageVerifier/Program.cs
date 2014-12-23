using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet;
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
            // TODO: Take a switch saying whether to use TeamCity logger

            // TODO: Get this from the command line
            var ignoreAssistanceMode = IgnoreAssistanceMode.None;

            ignoreAssistanceMode = IgnoreAssistanceMode.ShowNew;

            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine(@"USAGE: NuGetSuperBVT.exe c:\path\to\packages [c:\path\to\ignore.json]");
                return ReturnBadArgs;
            }

            var logger = new PackageVerifierLogger();

            IList<IssueIgnore> issuesToIgnore = null;

            if (args.Length >= 2)
            {
                string ignoreJsonFilePath = args[1];
                if (!File.Exists(ignoreJsonFilePath))
                {
                    logger.LogError("Couldn't find JSON ignore file at {0}", ignoreJsonFilePath);
                    return ReturnBadArgs;
                }

                issuesToIgnore = GetIgnoresFromFile(ignoreJsonFilePath, logger);
            }

            var totalTimeStopWatch = Stopwatch.StartNew();


            var nupkgsPath = args[0];


            var issueProcessor = new IssueProcessor(issuesToIgnore);

            var analyzer = new PackageAnalyzer();
            analyzer.Rules.Add(new AssemblyHasDocumentFileRule());
            analyzer.Rules.Add(new AssemblyStrongNameRule());
            analyzer.Rules.Add(new AuthenticodeSigningRule());
            analyzer.Rules.Add(new PowerShellScriptIsSignedRule());
            analyzer.Rules.Add(new RequiredAttributesRule());
            analyzer.Rules.Add(new SatellitePackageRule());
            analyzer.Rules.Add(new StrictSemanticVersionValidationRule());


            // TODO: Switch this to a custom IFileSystem that has only the packages we want (maybe?)
            var localPackageRepo = new LocalPackageRepository(nupkgsPath);

            var numPackagesInRepo = localPackageRepo.GetPackages().Count();
            logger.LogInfo("Found {0} packages in {1}", numPackagesInRepo, nupkgsPath);

            bool anyErrorOrWarnings = false;


            var ignoreAssistanceData = new Dictionary<string, IDictionary<string, IDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in localPackageRepo.GetPackages())
            {
                var packageTimeStopWatch = Stopwatch.StartNew();

                logger.LogInfo("Analyzing {0} ({1})", package.Id, package.Version);
                var issues = analyzer.AnalyzePackage(localPackageRepo, package, logger).ToList();

                var issuesToReport = issues.Select(issue => issueProcessor.GetIssueReport(issue, package)).ToList();

                if (issuesToReport.Count > 0)
                {
                    var infos = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Info).ToList();
                    var warnings = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Warning).ToList();
                    var errors = issuesToReport.Where(issueReport => issueReport.IssueLevel == LogLevel.Error).ToList();

                    LogLevel errorLevel = LogLevel.Info;
                    if (warnings.Count > 0)
                    {
                        errorLevel = LogLevel.Warning;
                        anyErrorOrWarnings = true;
                    }
                    if (errors.Count > 0)
                    {
                        errorLevel = LogLevel.Error;
                        anyErrorOrWarnings = true;
                    }
                    logger.Log(
                        errorLevel,
                        "{0} error(s), {1} warning(s), and {2} info(s) found with package {3} ({4})",
                        errors.Count, warnings.Count, infos.Count, package.Id, package.Version);

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
                            packageRuleInfo.Add(issueToReport.PackageIssue.Instance ?? "*", issueToReport.IgnoreJustification ?? "Enter justification");
                        }

                        PrintPackageIssue(logger, issueToReport);
                    }
                }
                else
                {
                    logger.LogInfo("No issues found with package {0}", package.Id, package.Version);
                }

                packageTimeStopWatch.Stop();
                logger.LogInfo("Took {0}ms", packageTimeStopWatch.ElapsedMilliseconds);
                Console.WriteLine();
            }

            if (ignoreAssistanceMode != IgnoreAssistanceMode.None)
            {
                Console.WriteLine("Showing JSON for ignore content:");
                Console.WriteLine(JsonConvert.SerializeObject(ignoreAssistanceData, Formatting.Indented));
                Console.WriteLine();
            }

            // TODO: Show total errors and warnings here

            totalTimeStopWatch.Stop();
            logger.LogInfo("Total took {0}ms", totalTimeStopWatch.ElapsedMilliseconds);

            return anyErrorOrWarnings ? ReturnErrorsOrWarnings : ReturnOk;
        }

        private static IList<IssueIgnore> GetIgnoresFromFile(string ignoreJsonFilePath, IPackageVerifierLogger logger)
        {
            string ignoreJsonFileContent = File.ReadAllText(ignoreJsonFilePath);

            IDictionary<string, IDictionary<string, IDictionary<string, string>>> ignoresInFile = null;
            ignoresInFile = JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, IDictionary<string, string>>>>(ignoreJsonFileContent);

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

            logger.LogInfo("Read JSON ignore file from {0} with {1} ignore(s)", ignoreJsonFilePath, ignoresInFile.Sum(package => package.Value.Sum(issue => issue.Value.Count())));

            return issuesToIgnore;
        }

        private static void PrintPackageIssue(IPackageVerifierLogger logger, IssueReport issue)
        {
            // TODO: Support this: https://confluence.jetbrains.com/display/TCD8/Build+Script+Interaction+with+TeamCity

            var issueInfo = issue.PackageIssue.IssueId;
            if (issue.PackageIssue.Instance == null)
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
