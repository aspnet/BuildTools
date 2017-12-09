// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class PackageOwnershipRule : IPackageVerifierRule
    {
        // Prefixes explicitly reserved for the ASP.Net team.
        private static readonly string[] OwnedPrefixes = new[]
        {
            "Microsoft.AspNetCore.",
            "Microsoft.AspNet.",
            "Microsoft.CodeAnalysis.Razor",
            "Microsoft.Data.Sqlite.",
            "Microsof.Dotnet.",
            "Microsoft.Extensions.",
            "Microsoft.EntityFrameworkCore.",
            "Microsoft.Net.Http.",
            "Microsoft.Owin.",
            "Microsoft.VisualStudio.",
        };

        // Packages that are owned by ASP.Net but do not start with one of the reserved prefixes.
        private static readonly string[] OwnedPackageIds = new string[]
        {
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (OwnedPackageIds.Contains(context.Metadata.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield break;
            }

            if (OwnedPrefixes.Any(prefix => context.Metadata.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                yield break;
            }
        }
    }
}
