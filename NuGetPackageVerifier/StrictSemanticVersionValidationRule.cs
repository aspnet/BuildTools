using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class StrictSemanticVersionValidationRule : IPackageVerifierRule
    {
        public IEnumerable<MyPackageIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            SemanticVersion semanticVersion;
            if (SemanticVersion.TryParseStrict(package.Version.ToString(), out semanticVersion))
            {
                yield break;
            }
            else
            {
                yield return PackageIssueFactory.NotSemanticVersion(package.Version);
            }
        }
    }
}
