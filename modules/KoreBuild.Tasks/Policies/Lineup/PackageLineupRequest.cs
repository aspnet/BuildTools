// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace KoreBuild.Tasks.Lineup
{
    internal class PackageLineupRequest
    {
        public PackageLineupRequest(string id, VersionRange version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }
        public VersionRange Version { get;  }

        public string RestoreSpec => $"{Id}/{Version.OriginalString}";
    }
}
