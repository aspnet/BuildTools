// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks.Policies
{
    internal class PackageVersionRestrictionPolicy : INuGetPolicy
    {
        private readonly ITaskItem[] _items;

        public PackageVersionRestrictionPolicy(ITaskItem[] items)
        {
            _items = items;
        }

        public Task ApplyAsync(PolicyContext context, CancellationToken cancellationToken)
        {
            foreach (var item in _items)
            {
                var config = item.ItemSpec;

                var propName = "error".Equals(item.GetMetadata("ErrorLevel"), StringComparison.OrdinalIgnoreCase)
                    ? "TreatPackageVersionAsError"
                    : "TreatPackageVersionAsWarning";


                foreach (var project in context.Projects)
                {
                    var propGroup = project.TargetsExtension.AddPropertyGroup(propName, "true");
                    propGroup.Add(new XAttribute("Condition", $" '$(Configuration)' == '{config}' "));
                }
            }

            var targets = Path.Combine(
                Path.GetDirectoryName(typeof(PackageVersionRestrictionPolicy).Assembly.Location),
                "Policy.VersionRestrictions.targets");

            foreach (var project in context.Projects)
            {
                project.TargetsExtension.AddImport(targets, required: false);
            }

            return Task.CompletedTask;
        }
    }
}
