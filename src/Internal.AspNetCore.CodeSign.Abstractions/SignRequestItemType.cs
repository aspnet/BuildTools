// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    /// <summary>
    /// The type of the sign request item.
    /// </summary>
    public enum SignRequestItemType
    {
        /// <summary>
        /// An unknown sign request item type.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// A file that should be excluded from signing.
        /// <para>
        /// In some cases, it can be useful to explicitly define files to be ignored by code-signing.
        /// </para>
        /// </summary>
        Exclusion,

        /// <summary>
        /// A .zip archive
        /// </summary>
        Zip,

        /// <summary>
        /// A NuGet package
        /// </summary>
        Nupkg,

        /// <summary>
        /// A Visual Studio extension installer file
        /// </summary>
        Vsix,

        /// <summary>
        /// A signable binary or powershell script.
        /// </summary>
        File,
    }
}
