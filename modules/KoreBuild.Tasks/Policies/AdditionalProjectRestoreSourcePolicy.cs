// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks.Policies
{
    internal class AdditionalProjectRestoreSourcePolicy : INuGetPolicy
    {
        private readonly ICollection<string> _sources;

        public AdditionalProjectRestoreSourcePolicy(ITaskItem[] items)
            : this(items.Select(i => i.ItemSpec).ToList())
        { }

        public AdditionalProjectRestoreSourcePolicy(ICollection<string> sources)
        {
            _sources = sources;
        }

        public Task ApplyAsync(PolicyContext context, CancellationToken cancellationToken)
        {
            foreach (var project in context.Projects)
            {

                foreach (var source in _sources)
                {
                    project.TargetsExtension.AddAdditionalRestoreSource(source);
                }
            }

            return Task.CompletedTask;
        }
    }
}
