// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetPackageVerifier
{
    public enum IgnoreAssistanceMode
    {
        /// <summary>
        /// Don't assist with creating verification ignores.
        /// </summary>
        None,

        /// <summary>
        /// Show ignore data for new (non-ignored) verification issues.
        /// </summary>
        ShowNew,

        /// <summary>
        /// Show ignore data for all verification issues (include those already ignored).
        /// </summary>
        ShowAll,
    }
}
