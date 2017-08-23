// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace KoreBuild.Tasks.Lineup
{
    internal class PackageVersionSource
    {
        private readonly ILogger _logger;
        private Dictionary<string, (string lineup, PackageIdentity identity)> _packageLineupMap;
        private List<PackageIdentity> _lineups;

        public PackageVersionSource(ILogger logger)
        {
            _logger = logger;
            _packageLineupMap = new Dictionary<string, (string, PackageIdentity)>(StringComparer.OrdinalIgnoreCase);
            _lineups = new List<PackageIdentity>();
        }

        public IEnumerable<PackageIdentity> Lineups => _lineups;

        public void AddPackagesFromLineup(string lineupId, NuGetVersion lineupVersion, List<PackageDependencyGroup> dependencies)
        {
            _lineups.Add(new PackageIdentity(lineupId, lineupVersion));

            foreach (var dep in dependencies)
            {
                foreach (var package in dep.Packages)
                {
                    if (!package.VersionRange.HasLowerBound)
                    {
                        _logger.LogWarning($"Lineup '{lineupId}' is missing a lower version bound for '{package.Id}'. Lineups should include a specific minimum version, so this package will be skipped.");
                        continue;
                    }

                    var packageIdentity = new PackageIdentity(package.Id, package.VersionRange.MinVersion);
                    AddPackage(packageIdentity, lineupId);
                }
            }
        }

        public void AddPackagesFromFolder(string folder)
        {
            foreach (var packageFile in Directory.GetFiles(folder, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(packageFile).EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogVerbose($"Skipping symbols package {packageFile} from folder lineup");
                    continue;
                }

                PackageIdentity package;
                try
                {
                    using (var reader = new PackageArchiveReader(packageFile))
                    {
                        package = reader.GetIdentity();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to read package information from {packageFile}. Error: {ex.Message}");
                    continue;
                }

                AddPackage(package, folder);
                _logger.LogVerbose($"Using package {package.Id} {package.Version} from {packageFile}");
            }
        }

        public bool TryGetPackageVersion(string id, out string version)
        {
            version = null;
            if (_packageLineupMap.TryGetValue(id, out var info))
            {
                version = info.identity.Version.ToNormalizedString();
                return true;
            }

            return false;
        }

        private void AddPackage(PackageIdentity package, string lineupName)
        {
            if (_packageLineupMap.TryGetValue(package.Id, out var existingLineup))
            {
                _logger.LogError($"Packages versions for '{package.Id}' found from multiple lineups: {lineupName} and {existingLineup.lineup}. Cannot determine which one to use.");
            }
            else
            {
                _packageLineupMap.Add(package.Id, (lineupName, package));
            }
        }
    }
}
