// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetPackageVerifier.Rules
{
    public class PackageCopyrightRule : IPackageVerifierRule
    {
        private const string ExpectedCopyright = "© Microsoft Corporation. All rights reserved.";

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!string.Equals(context.Metadata.Copyright, ExpectedCopyright, StringComparison.Ordinal))
            {
                yield return PackageIssueFactory.CopyrightIsIncorrect(context.Metadata.Id, ExpectedCopyright, context.Metadata.Copyright);
            }
        }
    }
}
