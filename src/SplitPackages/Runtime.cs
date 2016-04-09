// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SplitPackages
{
    public class Runtime
    {
        public string Name { get; set; }
        public static Runtime Win7x64 { get; } = new Runtime { Name = "win7-x64" };
        public static Runtime Win7x86 { get; } = new Runtime { Name = "win7-x86" };
    }
}
