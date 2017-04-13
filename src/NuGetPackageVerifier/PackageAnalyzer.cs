﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetPackageVerifier
{
    public class PackageAnalyzer
    {
        public IList<IPackageVerifierRule> Rules { get; } = new List<IPackageVerifierRule>();

        public IEnumerable<PackageVerifierIssue> AnalyzePackage(PackageAnalysisContext context)
        {
            var packageIssues = new List<PackageVerifierIssue>();
            foreach (var rule in Rules)
            {
                var issues = rule.Validate(context).ToList();
                packageIssues = packageIssues.Concat(issues).ToList();
            }

            return packageIssues;
        }
    }
}
