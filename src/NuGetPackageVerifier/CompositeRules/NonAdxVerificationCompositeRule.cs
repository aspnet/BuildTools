// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class NonAdxVerificationCompositeRule : IPackageVerifierRule
    {
        IPackageVerifierRule[] _rules = new IPackageVerifierRule[]
        {
            new AssemblyStrongNameRule(),
            new AuthenticodeSigningRule(),,
        };

        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            foreach (var rule in _rules)
            {
                foreach (var issue in rule.Validate(packageRepo, package, logger))
                {
                    yield return issue;
                }
            }
        }
    }
}
