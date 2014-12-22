using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetPackageVerifier
{
    public class IssueProcessor
    {
        public IssueProcessor(IEnumerable<IssueIgnore> issuesToIgnore)
        {
            IssuesToIgnore = issuesToIgnore;
        }

        public IEnumerable<IssueIgnore> IssuesToIgnore
        {
            get;
            private set;
        }

        public IssueReport GetIssueReport(MyPackageIssue packageIssue, IPackage package)
        {
            var ignoredRules = IssuesToIgnore.Where(
                issueIgnore =>
                    string.Equals(issueIgnore.IssueId, packageIssue.IssueId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(issueIgnore.Instance, packageIssue.Instance, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(issueIgnore.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));

            var firstIgnoreRule = ignoredRules.FirstOrDefault();

            return new IssueReport(packageIssue, firstIgnoreRule != null, firstIgnoreRule?.Justification);
        }
    }
}
