// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
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
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            var packageIssues = new List<PackageVerifierIssue>();
            foreach (var rule in Rules)
            {
                var issues = rule.Validate(nupkgFile, package, logger).ToList();
                packageIssues = packageIssues.Concat(issues).ToList();
            }

            return packageIssues;
        }
    }
}
