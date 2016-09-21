// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetPackageVerifier
{
    public class PackageVerifierOptions
    {
        // ignored warnings (with a justification)
        // key = issueid
        // values = <filename, justification>
        public IDictionary<string, IDictionary<string, string>> NoWarn { get; set; }
            = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public IList<string> PackageTypes { get; set; }
    }
}