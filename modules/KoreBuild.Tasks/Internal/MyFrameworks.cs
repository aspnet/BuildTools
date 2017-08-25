// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Frameworks;

namespace KoreBuild.Tasks
{
    internal static class MyFrameworks
    {
        // Until NuGet adds this to FrameworkConstants
        public static readonly NuGetFramework NetCoreApp21 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version("2.1"));
    }
}
