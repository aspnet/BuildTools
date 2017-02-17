// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;

namespace NuGetPackageVerifier.Rules
{
    public class BuildItemsRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            var dependencyFrameworks = new HashSet<NuGetFramework>(
                context.Metadata.DependencyGroups.Select(g => g.TargetFramework));

            var buildItems = context
                .PackageReader
                .GetBuildItems()
                .Where(f => f.Items.Any(i => IsCandidateMSBuildItem(i, context.Metadata.Id)));

            foreach (var buildItem in buildItems)
            {
                if (!dependencyFrameworks.Contains(buildItem.TargetFramework))
                {
                    yield return PackageIssueFactory
                        .BuildItemsDoNotMatchFrameworks(context.Metadata.Id, buildItem.TargetFramework);
                }
            }
        }

        private bool IsCandidateMSBuildItem(string file, string packageId)
        {
            if (!packageId.Equals(Path.GetFileNameWithoutExtension(file),
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var ext = Path.GetExtension(file);
            return ".props".Equals(ext, StringComparison.OrdinalIgnoreCase)
                || ".targets".Equals(ext, StringComparison.OrdinalIgnoreCase);
        }
    }
}
