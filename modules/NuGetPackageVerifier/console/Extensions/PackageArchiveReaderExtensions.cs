// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier
{
    public static class PackageArchiveReaderExtensions
    {
        /// <summary>
        /// A package is deemed to be a satellite package if it has a language property set, the id of the package is
        /// of the format [.*].[Language]
        /// and it has at least one dependency with an id that maps to the runtime package .
        /// </summary>
        public static bool IsSatellitePackage(this IPackageCoreReader packageReader)
        {
            // A satellite package has the following properties:
            //     1) A package suffix that matches the package's language, with a dot preceding it
            //     2) A dependency on the package with the same Id minus the language suffix
            //     3) The dependency can be found by Id in the repository (as its path is needed for installation)
            // Example: foo.ja-jp, with a dependency on foo

            var nuspecReader = new NuspecReader(packageReader.GetNuspec());
            var packageId = nuspecReader.GetId();
            var packageLanguage = nuspecReader.GetLanguage();

            if (!string.IsNullOrEmpty(packageLanguage)
                &&
                packageId.EndsWith('.' + packageLanguage, StringComparison.OrdinalIgnoreCase))
            {
                // The satellite pack's Id is of the format <Core-Package-Id>.<Language>. Extract the core package id using this.
                // Additionally satellite packages have a strict dependency on the core package
                var localruntimePackageId = packageId.Substring(0, packageId.Length - packageLanguage.Length - 1);

                foreach (var group in nuspecReader.GetDependencyGroups())
                {
                    foreach (var dependencyPackage in group.Packages)
                    {
                        if (dependencyPackage.Id.Equals(localruntimePackageId, StringComparison.OrdinalIgnoreCase)
                            && dependencyPackage.VersionRange != null
                            && dependencyPackage.VersionRange.MaxVersion == dependencyPackage.VersionRange.MinVersion
                            && dependencyPackage.VersionRange.IsMaxInclusive
                            && dependencyPackage.VersionRange.IsMinInclusive)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
