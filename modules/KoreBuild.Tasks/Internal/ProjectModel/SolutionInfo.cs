// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace KoreBuild.Tasks.ProjectModel
{
    internal class SolutionInfo
    {
        public SolutionInfo(string fullPath, IReadOnlyList<string> projects)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                throw new ArgumentException(nameof(fullPath));
            }

            FullPath = fullPath;
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        public string FullPath { get; }

        public IReadOnlyList<string> Projects { get; }
    }
}
