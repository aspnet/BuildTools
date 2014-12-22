using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public interface IPackageVerifierRule
    {
        IEnumerable<MyPackageIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger);
    }
}

