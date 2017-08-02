// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Mono.Cecil;
using Mono.Collections.Generic;

namespace NuGetPackageVerifier
{
    public class AssemblyAttributesData
    {
        public AssemblyNameDefinition AssemblyName { get; set; }

        public Collection<CustomAttribute> AssemblyAttributes { get; set; }

    }
}
