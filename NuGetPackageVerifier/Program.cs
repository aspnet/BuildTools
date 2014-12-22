using System;
using System.Diagnostics;
using System.Linq;
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

            // TODO: Add a way to read ignores from file

            if (args.Length != 1)
            {
                Console.WriteLine(@"USAGE: NuGetSuperBVT.exe c:\path\to\packages");
                return ReturnBadArgs;
            }

            var totalTimeStopWatch = Stopwatch.StartNew();

            var nupkgsPath = args[0];

            var issuesToIgnore = new[]
            {
                new IssueIgnore { IssueId = "NUSPEC_TAGS", Instance = null, PackageId = "EntityFramework", Justification = "Because no tags" },
                new IssueIgnore { IssueId = "NUSPEC_SUMMARY", Instance = null, PackageId = "EntityFramework", Justification = "Because no summary" },
                new IssueIgnore { IssueId = "XYZ", Instance = "ZZZ", PackageId = "ABC", Justification = "Because" },
                new IssueIgnore { IssueId = "XYZ", Instance = "ZZZ", PackageId = "ABC", Justification = "Because" },
                new IssueIgnore { IssueId = "XYZ", Instance = "ZZZ", PackageId = "ABC", Justification = "Because" },
                new IssueIgnore { IssueId = "XYZ", Instance = "ZZZ", PackageId = "ABC", Justification = "Because" },
            };
            var issueProcessor = new IssueProcessor(issuesToIgnore);

            var analyzer = new PackageAnalyzer();
            analyzer.Rules.Add(new AssemblyHasDocumentFileRule());
            analyzer.Rules.Add(new AssemblyStrongNameRule());
            analyzer.Rules.Add(new AuthenticodeSigningRule());
            analyzer.Rules.Add(new PowerShellScriptIsSignedRule());
            analyzer.Rules.Add(new RequiredAttributesRule());
            analyzer.Rules.Add(new SatellitePackageRule());
            analyzer.Rules.Add(new StrictSemanticVersionValidationRule());

            var logger = new PackageVerifierLogger();

            // TODO: Switch this to a custom IFileSystem that has only the packages we want (maybe?)
            var localPackageRepo = new LocalPackageRepository(nupkgsPath);

            var numPackagesInRepo = localPackageRepo.GetPackages().Count();
            logger.LogInfo("Found {0} packages in {1}", numPackagesInRepo, nupkgsPath);

            bool anyErrorOrWarnings = false;

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

                    foreach (var current in issuesToReport)
                    {
                        PrintPackageIssue(logger, current);
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

            totalTimeStopWatch.Stop();
            logger.LogInfo("Total took {0}ms", totalTimeStopWatch.ElapsedMilliseconds);


            return anyErrorOrWarnings ? ReturnErrorsOrWarnings : ReturnOk;
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
