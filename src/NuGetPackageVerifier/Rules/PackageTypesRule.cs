// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetPackageVerifier.Rules
{
    public class PackageTypesRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            var discoveredTypes = context.Metadata.PackageTypes.Select(t => t.Name);
            var expectedTypes = context.Options.PackageTypes ?? Enumerable.Empty<string>();

            foreach (var missing in expectedTypes.Except(discoveredTypes))
            {
                yield return PackageIssueFactory.PackageTypeMissing(missing);
            }

            foreach (var unexpected in discoveredTypes.Except(expectedTypes))
            {
                yield return PackageIssueFactory.PackageTypeUnexpected(unexpected);
            }
        }
    }
}
