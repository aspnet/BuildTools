// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGetPackageVerifier.Rules
{
    public class StrictSemanticVersionValidationRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!SemanticVersion.TryParse(context.Metadata.Version.ToString(), out var _))
            {
                yield return PackageIssueFactory.NotSemanticVersion(context.Metadata.Version);
            }
        }
    }
}
