// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace KoreBuild.Tasks.Utilities
{
    internal class EnvironmentHelper
    {
        private static string[] _searchPaths;
        private static string[] _executableExtensions;

        static EnvironmentHelper()
        {
            _searchPaths = Environment.GetEnvironmentVariable("PATH")
                        .Split(Path.PathSeparator)
                        .Select(p => p.Trim('"'))
                        .ToArray();

            _executableExtensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Environment.GetEnvironmentVariable("PATHEXT").Split(';').Select(e => e.ToLower().Trim('"')).ToArray()
                : Array.Empty<string>();
        }

        public static string GetCommandOnPath(string exeName)
        {
            return _searchPaths.Join(
                    _executableExtensions,
                    p => true,
                    e => true,
                    (p, e) => Path.Combine(p, exeName + e))
                .FirstOrDefault(File.Exists);
        }
    }
}
