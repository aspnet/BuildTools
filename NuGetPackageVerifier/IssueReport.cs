using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class IssueReport
    {
        public IssueReport(PackageVerifierIssue packageIssue, bool ignore, string ignoreJustification)
        {
            PackageIssue = packageIssue;
            IssueLevel = ignore ? LogLevel.Info : packageIssue.Level == MyPackageIssueLevel.Warning ? LogLevel.Warning : LogLevel.Error;
            IgnoreJustification = ignoreJustification;
        }

        public PackageVerifierIssue PackageIssue { get; private set; }

        public LogLevel IssueLevel { get; private set; }

        public string IgnoreJustification { get; set; }
    }
}
