// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public IEnumerable<PackageVerifierIssue> AnalyzePackage(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            var packageIssues = new List<PackageVerifierIssue>();
            foreach (var rule in Rules)
            {
                var issues = rule.Validate(packageRepo, package, logger).ToList();
                packageIssues = packageIssues.Concat(issues).ToList();
            }

            return packageIssues;
        }
    }
}
