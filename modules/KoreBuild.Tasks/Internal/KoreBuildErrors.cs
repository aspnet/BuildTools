// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace KoreBuild.Tasks
{
    public static class KoreBuildErrors
    {
        // Warnings
        public const int DotNetAssetVersionIsFloating = 2000;

        // NuGet errors
        public const int InvalidNuspecFile = 4001;
        public const int NuspecMissingFilesNode = 4011;
        public const int InvalidPackagePathMetadata = 4012;

        // Other errors
        public const int MissingArtifactType = 5001;

        // not used in code, but reserved for MSBuild targets
        public const int ArtifactInfoMismatch = 5002;
    }
}
