// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;

namespace PackageClassifier
{
    public class PackageSourcesCache
    {
        public PackageSourcesCache(string[] sourceFolders)
        {
            SourceFolders = sourceFolders;
            Packages = ReadPackages();
            if (Packages.Length == 0)
            {
                var foldersList = string.Concat(sourceFolders.Select(sf => $"{Environment.NewLine}    {sf}"));
                throw new InvalidOperationException($"No packages found in:{foldersList}");
            }
        }

        public string[] SourceFolders { get; }

        public PackageInformation[] Packages { get; }

        private PackageInformation[] ReadPackages()
        {
            return SourceFolders
                .Select(sf => new DirectoryInfo(sf))
                .SelectMany(di => di.EnumerateFileSystemInfos("*.nupkg"))
                .Select(ReadPackageInformation)
                .ToArray();
        }

        private PackageInformation ReadPackageInformation(FileSystemInfo file)
        {
            using (var reader = new PackageArchiveReader(file.FullName))
            {
                var identity = reader.GetIdentity();

                return new PackageInformation(
                    file.FullName,
                    identity.Id,
                    identity.Version.ToString(),
                    GetSupportedFrameworks(reader));
            }
        }

        private IEnumerable<string> GetSupportedFrameworks(PackageArchiveReader reader)
        {
            // To array is required here to force the enumeration.
            return reader.GetLibItems()
                .Select(frameworkSpecificGroup => frameworkSpecificGroup.TargetFramework.DotNetFrameworkName)
                .ToArray();
        }

        public PackageInformation[] GetById(string id)
        {
            return Packages.Where(p => p.HasId(id)).ToArray();
        }

        public PackageInformation[] GetByPattern(string pattern)
        {
            return SourceFolders.Select(s => new DirectoryInfo(s))
                .SelectMany(di => di.EnumerateFileSystemInfos(pattern))
                .Where(fi => !fi.Attributes.HasFlag(FileAttributes.Directory))
                .Select(fi => Packages.First(p => string.Equals(p.FullPath, fi.FullName)))
                .ToArray();
        }
    }
}
