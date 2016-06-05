// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace PackageClassifier
{
    public class PackageMapping
    {
        public PackageMapping(ClassificationEntry entry, PackageInformation[] packages)
        {
            Entry = entry;
            Packages = packages;
        }

        public ClassificationEntry Entry { get; }

        public PackageInformation[] Packages { get; }

        public bool HasPackage(PackageInformation package)
        {
            return Packages.Any(p => string.Equals(p.FullPath, package.FullPath, StringComparison.Ordinal));
        }
    }
}
