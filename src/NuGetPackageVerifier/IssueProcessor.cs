// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetPackageVerifier
{
    public class IssueProcessor
    {
        public IssueProcessor(IEnumerable<IssueIgnore> issuesToIgnore)
        {
            IssuesToIgnore = issuesToIgnore;
        }

        public IEnumerable<IssueIgnore> IssuesToIgnore { get; private set; }

        public IssueReport GetIssueReport(PackageVerifierIssue packageIssue, IPackage package)
        {
            if (IssuesToIgnore != null)
            {
                // If there are issues to ignore, process them
                var ignoredRules = IssuesToIgnore.Where(
                    issueIgnore =>
                        string.Equals(issueIgnore.IssueId, packageIssue.IssueId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.Instance, packageIssue.Instance ?? "*", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(issueIgnore.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));

                var firstIgnoreRule = ignoredRules.FirstOrDefault();

                return new IssueReport(
                    packageIssue,
                    firstIgnoreRule != null,
                    firstIgnoreRule == null ? null : firstIgnoreRule.Justification);
            }
            else
            {
                // If nothing to ignore, just report the issue as-is
                return new IssueReport(packageIssue, false, null);
            }
        }
    }
}
