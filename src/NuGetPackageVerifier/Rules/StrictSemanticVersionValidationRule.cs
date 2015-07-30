// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class StrictSemanticVersionValidationRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            SemanticVersion semanticVersion;
            if (SemanticVersion.TryParseStrict(package.Version.ToString(), out semanticVersion))
            {
                yield break;
            }
            else
            {
                yield return PackageIssueFactory.NotSemanticVersion(package.Version);
            }
        }
    }
}
