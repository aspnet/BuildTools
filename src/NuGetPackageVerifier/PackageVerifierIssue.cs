namespace NuGetPackageVerifier
{
    public class PackageVerifierIssue
    {
        public PackageVerifierIssue(string issueId, string issue, MyPackageIssueLevel level)
            : this(issueId, instance: null, issue: issue, level: level)
        {
        }

        public PackageVerifierIssue(string issueId, string instance, string issue, MyPackageIssueLevel level)
        {
            Instance = instance;
            IssueId = issueId;
            Issue = issue;
            Level = level;
        }

        public MyPackageIssueLevel Level
        {
            get;
            private set;
        }

        public string Issue
        {
            get;
            private set;
        }

        public string IssueId
        {
            get; private set;
        }

        public string Instance
        {
            get; set;
        }

        public override string ToString()
        {
            return string.Format("{0} @ {1}: {2}: {3}", IssueId, Instance, Level.ToString().ToUpperInvariant(), Issue);
        }
    }
}
