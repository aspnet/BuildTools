// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace PackageClassifier
{
    public class ClassifiedPackages
    {
        public ClassifiedPackages(Trait trait, IList<PackageInformation> packages)
        {
            Trait = trait;
            Packages = packages;
        }

        public Trait Trait { get; }

        public IList<PackageInformation> Packages { get; }
    }
}
