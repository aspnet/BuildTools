using System.Collections.Generic;
using System.Linq;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class PackageAnalyzer
    {
        private IList<IPackageVerifierRule> _rules = new List<IPackageVerifierRule>();

        public IList<IPackageVerifierRule> Rules
        {
            get
            {
                return _rules;
            }
        }

        public IEnumerable<MyPackageIssue> AnalyzePackage(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            IEnumerable<MyPackageIssue> packageIssues = new List<MyPackageIssue>();
            foreach (var rule in Rules)
            {
                var issues = rule.Validate(packageRepo, package, logger).ToList();
                packageIssues = packageIssues.Concat(issues);
            }
            return packageIssues;
        }
    }
}
