// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using KoreBuild.Tasks.ProjectModel;

namespace KoreBuild.Tasks.Policies
{
    internal class PolicyContext
    {
        public string SolutionDirectory { get; set; }
        public TaskLoggingHelper Log { get; set; }

        public List<string> RestoreSources { get; set; }
        public List<string> RestoreAdditionalSources { get; set; }
        public bool RestoreDisableParallel { get; set; }
        public string RestorePackagesPath { get; set; }
        public string RestoreConfigFile { get; set; }
        public bool RestoreIgnoreFailedSources { get; set; }
        public bool RestoreNoCache { get; set; }

        public IReadOnlyList<ProjectInfo> Projects { get; set; } = Array.Empty<ProjectInfo>();
    }
}
