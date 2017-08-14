// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Tasks
{
    public static class KoreBuildErrors
    {
        // Typically used in repos in Directory.Build.targets
        public const int PackagesHaveNotYetBeenPinned = 1001;

        public const int InvalidNuspecFile = 4001;
    }
}
