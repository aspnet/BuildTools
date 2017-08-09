// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.Tasks.ProjectModel
{
    internal class ProjectFrameworkInfo
    {
        public ProjectFrameworkInfo(NuGetFramework targetFramework, IReadOnlyList<PackageReferenceInfo> dependencies)
        {
            TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
            Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public NuGetFramework TargetFramework { get; }
        public IReadOnlyList<PackageReferenceInfo> Dependencies { get; }
    }
}
