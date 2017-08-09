// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Tasks.Lineup
{
    internal class PackageVersionSource
    {
        private readonly ILogger _logger;
        private Dictionary<string, string> _packageLineupMap;
        private Dictionary<string, PackageDependency> _packages;
        private List<PackageIdentity> _lineups;

        public PackageVersionSource(ILogger logger)
        {
            _logger = logger;
            _packageLineupMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _packages = new Dictionary<string, PackageDependency>(StringComparer.OrdinalIgnoreCase);
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
                    if (package.VersionRange.Equals(VersionRange.All))
                    {
                        _logger.LogWarning($"Lineup '{lineupId}' has an unbound version for '{package.Id}'. Lineups should include a specific version, so this package will be skipped.");
                        continue;
                    }

                    if (_packageLineupMap.TryGetValue(package.Id, out var existingLineup))
                    {
                        _logger.LogError($"Packages versions for '{package.Id}' found from multiple lineups: {lineupId} and {existingLineup}. Cannot determine which one to use.");
                        continue;
                    }

                    if (!TryAddPackage(package))
                    {
                        _logger.LogError($"A package versions for '{package.Id}' has already been specified from another source.");
                        continue;
                    }

                    _packageLineupMap.Add(package.Id, lineupId);
                }
            }
        }

        public bool TryAddPackage(PackageDependency package)
        {
            if (_packages.TryGetValue(package.Id, out _))
            {
                return false;
            }

            _packages.Add(package.Id, package);
            return true;
        }

        public bool TryGetPackageVersion(string id, out string version)
        {
            version = null;
            if (_packages.TryGetValue(id, out var info))
            {
                version = info.VersionRange.ToShortString();
                return true;
            }

            return false;
        }
    }
}
