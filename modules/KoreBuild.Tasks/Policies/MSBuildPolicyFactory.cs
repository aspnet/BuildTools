// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using KoreBuild.Tasks.Policies;

namespace KoreBuild.Tasks
{
    internal class MSBuildPolicyFactory
    {
        public virtual INuGetPolicy Create(string type, IEnumerable<ITaskItem> items, TaskLoggingHelper logger)
        {
            switch (type.ToLowerInvariant())
            {
                case "additionalrestoresource":
                    return new AdditionalProjectRestoreSourcePolicy(items.ToArray());
                case "lineup":
                    return new PackageLineupPolicy(items.ToArray(), logger);
                case "disallowpackagereferenceversion":
                    return new PackageVersionRestrictionPolicy(items.ToArray());
                default:
                    return null;
            }
        }
    }
}
