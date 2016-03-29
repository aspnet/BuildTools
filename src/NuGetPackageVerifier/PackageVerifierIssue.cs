// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetPackageVerifier
{
    public class PackageVerifierIssue
    {
        public PackageVerifierIssue(string issueId, string issue, PackageIssueLevel level)
            : this(issueId, instance: null, issue: issue, level: level)
        {
        }

        public PackageVerifierIssue(string issueId, string instance, string issue, PackageIssueLevel level)
        {
            Instance = instance;
            IssueId = issueId;
            Issue = issue;
            Level = level;
        }

        public PackageIssueLevel Level
        {
            get;
            private set;
        }

        public string Issue
        {
            get;
            private set;
        }

        public string IssueId
        {
            get; private set;
        }

        public string Instance
        {
            get; set;
        }

        public override string ToString()
        {
            return string.Format("{0} @ {1}: {2}: {3}", IssueId, Instance, Level.ToString().ToUpperInvariant(), Issue);
        }
    }
}
