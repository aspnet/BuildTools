// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier
{
    public class PackageSet
    {
        // Class names of rules to use
        public string[] Rules { get; set; }

        // List of packages(key), each with a set of rules to ignore(key), each with a set of instances(key),
        // each of which has a justification(value)
        public IDictionary<string, IDictionary<string, IDictionary<string, string>>> Packages { get; set; }
    }
}
