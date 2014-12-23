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

        // TODO: Consider having an interface for smarter ignore rules (e.g. ignore rule X for all instances, ignore rule X on packages matching Foo.Bar.*)
        public IEnumerable<IssueIgnore> IssuesToIgnore
        {
            get;
            private set;
        }

        public IssueReport GetIssueReport(PackageVerifierIssue packageIssue, IPackage package)
        {
            if (IssuesToIgnore != null)
            {
                // If there are issues to ignore, process them
                var ignoredRules = IssuesToIgnore.Where(
                    issueIgnore =>
                        string.Equals(issueIgnore.IssueId, packageIssue.IssueId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.Instance, packageIssue.Instance ?? "*", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));

                var firstIgnoreRule = ignoredRules.FirstOrDefault();
                return new IssueReport(packageIssue, firstIgnoreRule != null, firstIgnoreRule?.Justification);
            }
            else
            {
                // If nothing to ignore, just report the issue as-is
                return new IssueReport(packageIssue, false, null);
            }
        }
    }
}
