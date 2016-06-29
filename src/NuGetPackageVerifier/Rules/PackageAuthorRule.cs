// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class PackageAuthorRule : IPackageVerifierRule
    {
        private const string _expectedAuthor = "Microsoft";

        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            if (package.Authors == null || package.Authors.Count() < 1)
            {
                yield return PackageIssueFactory.RequiredAuthor();
            }

            if (package.Authors.Count() > 1)
            {
                yield return PackageIssueFactory.SingleAuthorOnly(package.Id);
            }

            if (string.Equals(package.Authors.First(), _expectedAuthor, System.StringComparison.Ordinal))
            {
                yield return PackageIssueFactory.AuthorIsIncorrect(package.Id, _expectedAuthor);
            }
        }
    }
}
