// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace KoreBuild.Tasks.Utilities
{
    public class LineupPackage
    {
        public DependencyVersionsFile DepsFile { get; set; }
        public DependencyVersionsFile ToolsFile { get; set; }
    }
}
