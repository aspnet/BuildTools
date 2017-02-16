// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier.Rules
{
    public class DotNetCliToolPackageRule : IPackageVerifierRule
    {
        private static readonly NuGetFramework _expectedFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!context.Metadata.PackageTypes.Any(p => p == PackageType.DotnetCliTool))
            {
                yield break;
            }

            var libItems = context.PackageReader.GetLibItems()
                .Where(f => f.TargetFramework == _expectedFramework)
                .FirstOrDefault();

            if (libItems == null)
            {
                yield return PackageIssueFactory.DotNetCliToolMustTargetFramework(_expectedFramework);
                yield break;
            }

            var assembly = libItems.Items.Where(f =>
                Path.GetFileName(f).StartsWith("dotnet-")
                && Path.GetExtension(f) == ".dll");

            if (!assembly.Any())
            {
                yield return PackageIssueFactory.DotNetCliToolMissingDotnetAssembly();
            }

            foreach (var tool in assembly)
            {
                var expected = Path.GetFileNameWithoutExtension(tool) + ".runtimeconfig.json";
                if (!libItems.Items.Any(f => Path.GetFileName(f) == expected))
                {
                    yield return PackageIssueFactory.DotNetCliToolMissingRuntimeConfig();
                }
            }

            if (!context.PackageReader.GetFiles().Any(f => f == "prefercliruntime"))
            {
                yield return PackageIssueFactory.DotNetCliToolMissingPrefercliRuntime();
            }
        }
    }
}
