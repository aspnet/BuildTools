// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace SplitPackages
{
    public class Runtime
    {
        public string Name { get; set; }
        public static Runtime Win7x64 { get; } = new Runtime { Name = "win7-x64" };
        public static Runtime Win7x86 { get; } = new Runtime { Name = "win7-x86" };
        public static Runtime Debian8x64 { get; } = new Runtime { Name = "debian.8-x64" };
        public static Runtime Ubuntu1404x64 { get; } = new Runtime { Name = "ubuntu.14.04-x64" };
        public static Runtime[] AllRuntimes { get; } = { Win7x86, Win7x64, Debian8x64, Ubuntu1404x64 };
    }
}
