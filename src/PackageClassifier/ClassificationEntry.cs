// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace PackageClassifier
{
    public class ClassificationEntry
    {
        public ClassificationEntry(string identity, Trait[] traits)
        {
            Identity = identity;
            Traits = traits;
        }

        public string Identity { get; }

        public Trait[] Traits { get; }

        public bool HasIdentity(string pattern)
        {
            return string.Equals(Identity, pattern, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasTrait(string trait, string value)
        {
            return Traits.Any(t => t.HasName(trait) && t.HasValue(value));
        }

        public Trait GetTrait(string traitName)
        {
            foreach (var trait in Traits)
            {
                if (trait.HasName(traitName))
                {
                    return trait;
                }
            }

            return null;
        }
    }
}
