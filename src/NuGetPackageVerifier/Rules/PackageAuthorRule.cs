// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetPackageVerifier.Rules
{
    public class PackageAuthorRule : IPackageVerifierRule
    {
        private const string _expectedAuthor = "Microsoft";

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (context.Metadata.Authors == null || context.Metadata.Authors.Count() < 1)
            {
                yield return PackageIssueFactory.RequiredAuthor();
            }

            if (context.Metadata.Authors.Count() > 1)
            {
                yield return PackageIssueFactory.SingleAuthorOnly(context.Metadata.Id);
            }

            var author = context.Metadata.Authors.First();
            if (!string.Equals(author, _expectedAuthor, System.StringComparison.Ordinal))
            {
                yield return PackageIssueFactory.AuthorIsIncorrect(context.Metadata.Id, _expectedAuthor, author);
            }
        }
    }
}
