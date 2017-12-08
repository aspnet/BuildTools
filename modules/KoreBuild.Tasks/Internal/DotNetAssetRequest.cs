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
        public bool IsSharedRuntime { get; set; }
        public string Channel { get; set; }
        public string InstallDir { get; set; }
        public string Arch { get; set; }
        public string Feed { get; set; }
        public string FeedCredential { get; set; }
    }
}
