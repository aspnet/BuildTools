// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class IssueReport
    {
        public IssueReport(PackageVerifierIssue packageIssue, bool ignore, string ignoreJustification)
        {
            PackageIssue = packageIssue;
            IssueLevel = ignore ? LogLevel.Info : packageIssue.Level == PackageIssueLevel.Warning ? LogLevel.Warning : LogLevel.Error;
            IgnoreJustification = ignoreJustification;
        }

        public PackageVerifierIssue PackageIssue { get; private set; }

        public LogLevel IssueLevel { get; private set; }

        public string IgnoreJustification { get; set; }
    }
}
