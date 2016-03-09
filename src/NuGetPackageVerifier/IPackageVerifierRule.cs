// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier
{
    public interface IPackageVerifierRule
    {
        IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger);
    }
}

