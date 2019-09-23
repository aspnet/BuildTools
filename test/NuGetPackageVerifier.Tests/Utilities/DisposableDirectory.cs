// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using IOPath = System.IO.Path;

namespace NuGetPackageVerifier.Utilities
{
    internal class DisposableDirectory : IDisposable
    {
        public DisposableDirectory()
        {
            Path = IOPath.Combine(AppContext.BaseDirectory, IOPath.GetRandomFileName());
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Don't throw if we fail to delete the test directory.
            }
        }
    }
}
