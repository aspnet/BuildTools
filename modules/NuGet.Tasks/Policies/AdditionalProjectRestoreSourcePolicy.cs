// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace NuGet.Tasks.Policies
{
    internal class AdditionalProjectRestoreSourcePolicy : INuGetPolicy
    {
        private readonly ITaskItem[] _sources;

        public AdditionalProjectRestoreSourcePolicy(ITaskItem[] items)
        {
            _sources = items;
        }

        public Task ApplyAsync(PolicyContext context, CancellationToken cancellationToken)
        {
            foreach (var project in context.Projects)
            {
                var propGroup = project.TargetsExtension.AddPropertyGroup();

                foreach (var source in _sources)
                {
                    propGroup.Add(new XElement("RestoreAdditionalProjectSources", "$(RestoreAdditionalProjectSources);" + source.ItemSpec));
                }
            }

            return Task.CompletedTask;
        }
    }
}
