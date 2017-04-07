// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace ApiCheck
{
    public class ApiComparisonResult
    {
        public ApiComparisonResult(IList<BreakingChange> breakingChanges, IList<ApiChangeExclusion> exclusions)
        {
            BreakingChanges = breakingChanges;
            RemainingExclusions = exclusions;
        }

        public IList<BreakingChange> BreakingChanges { get; }
        public IList<ApiChangeExclusion> RemainingExclusions { get; }
    }
}
