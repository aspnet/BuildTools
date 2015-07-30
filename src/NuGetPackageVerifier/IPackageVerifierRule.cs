using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public interface IPackageVerifierRule
    {
        IEnumerable<PackageVerifierIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger);
    }
}

