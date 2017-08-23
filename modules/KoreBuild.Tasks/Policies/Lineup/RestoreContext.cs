// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using KoreBuild.Tasks.Policies;

namespace KoreBuild.Tasks.Lineup
{
    internal class RestoreContext
    {
        public ILogger Log { get; set; }

        public List<PackageLineupRequest> PackageLineups { get; set; }

        public PolicyContext Policy { get; set; }
        
        public string ProjectDirectory { get; set; }

        // collects a list of package versions that should be pinned
        public PackageVersionSource VersionSource { get; set; }
    }
}
