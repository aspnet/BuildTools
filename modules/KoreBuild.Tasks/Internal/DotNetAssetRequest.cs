// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace KoreBuild.Tasks
{
    internal class DotNetAssetRequest
    {
        public DotNetAssetRequest(string version)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public string Version { get; }
        public bool IsSharedRuntime { get; internal set; }
        public string Channel { get; internal set; }
        public string InstallDir { get; internal set; }
        public string Arch { get; internal set; }
    }
}
