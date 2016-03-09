// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NuGetPackageVerifier.Logging;
using NuGet.Packaging;

namespace NuGetPackageVerifier.Rules
{
    public class AuthenticodeSigningRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            var packagePath = nupkgFile.FullName;
            var nupkgWithoutExt = Path.Combine(
                Path.GetDirectoryName(packagePath),
                Path.GetFileNameWithoutExtension(packagePath));

            try
            {
                UnzipPackage(nupkgWithoutExt);
                using (var reader = new PackageArchiveReader(nupkgFile.FullName))
                {
                    foreach (var current in reader.GetFiles())
                    {
                        //string packagePath = package.FileSystem.Root + "\\" + Id + "." + Version + ".nupkg"
                        var extension = Path.GetExtension(current);

                        // TODO: Need to add more extensions?
                        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            var pathOfFileToScan = Path.Combine(nupkgWithoutExt, current);
                            var realAssemblyPath = pathOfFileToScan;
                            if (!File.Exists(realAssemblyPath))
                            {
                                realAssemblyPath = pathOfFileToScan.Replace("+", "%2B").Replace("#", "%23");
                                if (!File.Exists(realAssemblyPath))
                                {
                                    logger.LogError(
                                        "The assembly '{0}' in this package can't be found (a bug in this tool, most likely).",
                                        current);

                                    continue;
                                }
                            }

                            var isAuthenticodeSigned = WinTrust.IsAuthenticodeSigned(realAssemblyPath);
                            if (!isAuthenticodeSigned)
                            {
                                yield return PackageIssueFactory.PEFileNotAuthenticodeSigned(current);
                            }
                        }
                    }
                }
            }
            finally
            {
                CleanUpFolder(nupkgWithoutExt, logger);
            }

            yield break;
        }

        private void UnzipPackage(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            Directory.CreateDirectory(path);

            ZipFile.ExtractToDirectory(path + ".nupkg", path);
        }

        private void CleanUpFolder(string path, IPackageVerifierLogger logger)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Couldn't clean temp unzip folder: " + ex.Message);
            }
        }
    }
}
