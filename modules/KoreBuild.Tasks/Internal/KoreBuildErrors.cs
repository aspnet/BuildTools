// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace KoreBuild.Tasks
{
    public static class KoreBuildErrors
    {
        public const string Prefix = "KRB";

        // Warnings
        public const int DotNetAssetVersionIsFloating = 2000;
        public const int RepoVersionDoesNotMatchProjectVersion = 2001;
        public const int RepoPackageVersionDoesNotMatchProjectPackageVersion = 2002;
        public const int DuplicatePackageReference = 2003;

        // NuGet errors
        public const int InvalidNuspecFile = 4001;
        public const int ConflictingPackageReferenceVersions = 4002;
        public const int DependenciesFileDoesNotExist = 4003;
        public const int PackageVersionNotFoundInLineup = 4004;
        public const int PackageRefHasLiteralVersion = 4005;
        public const int VariableNotFoundInDependenciesPropsFile = 4006;
        public const int PackageRefHasFloatingVersion = 4007;
        public const int PackageRefPropertyGroupNotFound = 4008;
        public const int PackageReferenceDoesNotHaveVersion = 4009;
        public const int InvalidPackageVersion = 4010;
        public const int NuspecMissingFilesNode = 4011;
        public const int InvalidPackagePathMetadata = 4012;

        // Other errors
        public const int MissingArtifactType = 5001;

        // not used in code, but reserved for MSBuild targets
        public const int ArtifactInfoMismatch = 5002;
        public const int FilesToSignMismatchedWithArtifactInfo = 5003;
        public const int FilesToSignMissingCertInfo = 5004;
    }
}
