// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasDocumentFileRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (context.Metadata.PackageTypes.Any(p => p == PackageType.DotnetCliTool))
            {
                yield break;
            }

            if (!context.PackageReader.IsSatellitePackage())
            {
                var allXmlFiles =
                    from item in context.PackageReader.GetLibItems()
                    from file in item.Items
                    select file into path
                    where path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    select path;

                foreach (var current in context.PackageReader.GetLibItems())
                {
                    foreach (var item in current.Items)
                    {
                        var assemblyPath = item;
                        // TODO: Does this need to check for just managed code?
                        if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            var docFilePath = Path.ChangeExtension(assemblyPath, ".xml");
                            if (!allXmlFiles.Contains(docFilePath, StringComparer.OrdinalIgnoreCase))
                            {
                                yield return PackageIssueFactory.AssemblyHasNoDocFile(assemblyPath);
                            }
                        }
                    }
                }
            }
        }
    }
}
