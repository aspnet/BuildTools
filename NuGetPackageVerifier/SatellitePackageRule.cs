using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class SatellitePackageRule : IPackageVerifierRule
    {
        public IEnumerable<MyPackageIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            if (package.IsSatellitePackage())
            {
                if (package.Summary.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageSummaryNotLocalized();
                }
                if (package.Title.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageTitleNotLocalized();
                }
                if (package.Description.Contains("{"))
                {
                    yield return PackageIssueFactory.Satellite_PackageDescriptionNotLocalized();
                }
            }
            yield break;
        }
    }
}
