// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace PackageClassifier
{
    public class ClassificationResult
    {
        public ClassificationResult(string trait)
        {
            Trait = trait;
        }

        public string Trait { get; }

        public IList<ClassifiedPackages> ClassifiedPackages { get; } = new List<ClassifiedPackages>();

        public void AddPackages(Trait mappingTrait, PackageInformation[] packages)
        {
            var classification = ClassifiedPackages.FirstOrDefault(cp => cp.Trait.Equals(mappingTrait));
            if (classification != null)
            {
                foreach (var package in packages)
                {
                    classification.Packages.Add(package);
                }
            }
            else
            {
                classification = new ClassifiedPackages(mappingTrait, packages.ToList());
                ClassifiedPackages.Add(classification);
            }
        }

        public IEnumerable<PackageInformation> GetPackagesForValue(string traitValue)
        {
            return ClassifiedPackages.FirstOrDefault(cp => cp.Trait.HasValue(traitValue))?.Packages ??
                Enumerable.Empty<PackageInformation>();
        }
    }
}
