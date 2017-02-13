// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NugetReferenceResolver
{
    public class PackageAssembly
    {
        public PackageAssembly(string relativePath, string resolvedPath)
        {
            RelativePath = relativePath;
            ResolvedPath = resolvedPath;
        }

        public string FileName => Path.GetFileName(ResolvedPath);
        public string RelativePath { get; set; }
        public string ResolvedPath { get; set; }
    }
}
