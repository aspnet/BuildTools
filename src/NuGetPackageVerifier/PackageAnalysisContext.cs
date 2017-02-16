// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public class PackageAnalysisContext : IDisposable
    {
        private PackageArchiveReader _reader;

        public FileInfo PackageFileInfo { get; set; }
        public IPackageMetadata Metadata { get; set; }
        public PackageVerifierOptions Options { get; set; }
        public IPackageVerifierLogger Logger { get; set; }
        public PackageArchiveReader PackageReader
        {
            get
            {
                if (_reader == null)
                {
                    _reader = new PackageArchiveReader(PackageFileInfo.FullName);
                }
                return _reader;
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
