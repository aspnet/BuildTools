// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGetPackageVerifier.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasDocumentFileRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            using (var reader = new PackageArchiveReader(nupkgFile.FullName))
            {
                PackageIdentity identity;
                string packageLanguage;
                if (!PackageHelper.IsSatellitePackage(reader, out identity, out packageLanguage))
                {
                    var allXmlFiles =
                        from item in reader.GetLibItems()
                        from file in item.Items
                        select file into path
                        where path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                        select path;

                    foreach (var current in reader.GetLibItems())
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

            yield break;
        }
    }
}
