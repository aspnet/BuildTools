// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.AspNetCore.BuildTools.Utilities
{
    internal class FileHelpers
    {
        public static string EnsureTrailingSlash(string path)
            => !HasTrailingSlash(path)
                ? path + Path.DirectorySeparatorChar
                : path;

        public static bool HasTrailingSlash(string path)
            => string.IsNullOrEmpty(path)
                ? false
                : path[path.Length - 1] == Path.DirectorySeparatorChar
                    || path[path.Length - 1] == Path.AltDirectorySeparatorChar;
    }
}
