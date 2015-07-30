using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasDocumentFileRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            if (!package.IsSatellitePackage())
            {
                IEnumerable<string> allXmlFiles =
                    from file in package.GetLibFiles()
                    select file.Path into path
                    where path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    select path;

                foreach (IPackageFile current in package.GetLibFiles())
                {
                    string assemblyPath = current.Path;
                    // TODO: Does this need to check for just managed code?
                    if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string docFilePath = Path.ChangeExtension(assemblyPath, ".xml");
                        if (!allXmlFiles.Contains(docFilePath, StringComparer.OrdinalIgnoreCase))
                        {
                            yield return PackageIssueFactory.AssemblyHasNoDocFile(assemblyPath);
                        }
                    }
                }
            }
            yield break;
        }
    }
}
