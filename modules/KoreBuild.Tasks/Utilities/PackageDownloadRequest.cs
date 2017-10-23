// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;

namespace KoreBuild.Tasks.Utilities
{
    public class PackageDownloadRequest
    {
        public PackageIdentity Identity { get; set; }
        public string OutputPath { get; set; }
        public string Source { get; set; }
    }
}
