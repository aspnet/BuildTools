// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class SigningVerificationCompositeRule : IPackageVerifierRule
    {
        IPackageVerifierRule[] _rules = new IPackageVerifierRule[]
        {
            new AuthenticodeSigningRule(),
            new PowerShellScriptIsSignedRule(),
            new RequiredNuSpecInfoRule(),
            new PackageOwnershipRule(),
            new DefaultCompositeRule(),
        };

        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            foreach (var rule in _rules)
            {
                foreach (var issue in rule.Validate(nupkgFile, package, logger))
                {
                    yield return issue;
                }
            }
        }
    }
}
