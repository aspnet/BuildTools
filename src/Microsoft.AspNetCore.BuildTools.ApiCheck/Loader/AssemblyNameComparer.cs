// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ApiCheck
{
    internal class AssemblyNameComparer : IEqualityComparer<AssemblyName>
    {
        public static readonly IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

        public bool Equals(AssemblyName x, AssemblyName y)
        {
            // Ignore case because that's what Assembly.Load does.
            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.CultureName ?? string.Empty, y.CultureName ?? string.Empty, StringComparison.Ordinal);
        }

        public int GetHashCode(AssemblyName obj)
        {
            var hashCode = 0;
            if (obj.Name != null)
            {
                hashCode ^= obj.Name.GetHashCode();
            }

            hashCode ^= (obj.CultureName ?? string.Empty).GetHashCode();
            return hashCode;
        }
    }
}
