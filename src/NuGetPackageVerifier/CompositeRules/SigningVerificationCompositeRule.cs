// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class SigningVerificationCompositeRule : IPackageVerifierRule
    {
        IPackageVerifierRule[] _rules = new IPackageVerifierRule[]
        {
            new AssemblyHasCommitHashAttributeRule(),
            // new AssemblyIsBuiltInReleaseConfigurationRule(),
            new AuthenticodeSigningRule(),
            new PowerShellScriptIsSignedRule(),
            new RequiredNuSpecInfoRule(),
            new PackageOwnershipRule(),
            new DefaultCompositeRule(),
            new PrereleaseDependenciesVersionRule(),
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var rule in _rules)
            {
                foreach (var issue in rule.Validate(context))
                {
                    yield return issue;
                }
            }
        }
    }
}
