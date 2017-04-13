// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;

namespace NuGetPackageVerifier
{
    public class IssueProcessor
    {
        private readonly List<IssueIgnore> _allIssuesToIgnore;

        public IssueProcessor(IEnumerable<IssueIgnore> issuesToIgnore)
        {
            _allIssuesToIgnore = issuesToIgnore?.ToList();
            RemainingIssuesToIgnore = issuesToIgnore?.ToList();
        }

        public List<IssueIgnore> RemainingIssuesToIgnore { get; }

        public IssueReport GetIssueReport(PackageVerifierIssue packageIssue, IPackageMetadata package)
        {
            if (_allIssuesToIgnore != null)
            {
                // If there are issues to ignore, process them
                var ignoredRule = _allIssuesToIgnore.Find(
                    issueIgnore =>
                        string.Equals(issueIgnore.IssueId, packageIssue.IssueId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.Instance, packageIssue.Instance ?? "*", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));

                if (ignoredRule != null)
                {
                    RemainingIssuesToIgnore.Remove(ignoredRule);

                    return new IssueReport(
                        packageIssue,
                        ignore: true,
                        ignoreJustification: ignoredRule.Justification);
                }

                return new IssueReport(
                    packageIssue,
                    ignore: false,
                    ignoreJustification: null);
            }
            // If nothing to ignore, just report the issue as-is
            return new IssueReport(packageIssue, false, null);
        }
    }
}
