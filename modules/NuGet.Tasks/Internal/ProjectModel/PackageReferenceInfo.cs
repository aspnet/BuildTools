// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Tasks.ProjectModel
{
    internal class PackageReferenceInfo
    {
        public PackageReferenceInfo(string id, string version, bool isImplicitlyDefined, bool noWarn)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            Id = id;
            Version = version;
            IsImplicitlyDefined = isImplicitlyDefined;
            NoWarn = noWarn;
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsImplicitlyDefined { get; }
        public bool NoWarn { get; }
    }
}
