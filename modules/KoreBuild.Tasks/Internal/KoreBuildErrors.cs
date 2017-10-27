// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace KoreBuild.Tasks
{
    public static class KoreBuildErrors
    {
        public const string Prefix = "KRB";

        // Typically used in repos in Directory.Build.targets
        public const int PackagesHaveNotYetBeenPinned = 1001;

        // Warnings
        public const int DotNetAssetVersionIsFloating = 2000;
        public const int RepoVersionDoesNotMatchProjectVersion = 2001;
        public const int RepoPackageVersionDoesNotMatchProjectPackageVersion = 2002;
        public const int DuplicatePackageReference = 2003;

        // NuGet errors
        public const int InvalidNuspecFile = 4001;
        public const int ConflictingPackageReferenceVersions = 4002;
        public const int DotNetCliReferenceReferenceHasVersion = 4003;
        public const int PackageVersionNotFoundInLineup = 4004;
        public const int PackageRefHasLiteralVersion = 4005;
        public const int VariableNotFoundInDependenciesPropsFile = 4006;
        public const int PackageRefHasFloatingVersion = 4007;
        public const int PackageRefPropertyGroupNotFound = 4008;
        public const int PackageReferenceDoesNotHaveVersion = 4009;
        public const int InvalidPackageVersion = 4010;

        // Other unknown errors
        public const int PolicyFailedToApply = 5000;
        public const int UnknownPolicyType = 5001;
    }
}
