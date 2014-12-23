using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class RequiredAttributesRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            if (string.IsNullOrEmpty(package.Copyright))
            {
                yield return PackageIssueFactory.RequiredCopyright();
            }
            if (package.LicenseUrl == null)
            {
                yield return PackageIssueFactory.RequiredLicenseUrl();
            }
            if (package.IconUrl == null)
            {
                yield return PackageIssueFactory.RequiredIconUrl();
            }
            if (string.IsNullOrEmpty(package.Tags))
            {
                yield return PackageIssueFactory.RequiredTags();
            }
            if (string.IsNullOrEmpty(package.Title))
            {
                yield return PackageIssueFactory.RequiredTitle();
            }
            if (string.IsNullOrEmpty(package.Summary))
            {
                yield return PackageIssueFactory.RequiredSummary();
            }
            if (package.ProjectUrl == null)
            {
                yield return PackageIssueFactory.RequiredProjectUrl();
            }
            if (!package.RequireLicenseAcceptance)
            {
                yield return PackageIssueFactory.RequiredRequireLicenseAcceptanceTrue();
            }
            yield break;
        }
    }
}
