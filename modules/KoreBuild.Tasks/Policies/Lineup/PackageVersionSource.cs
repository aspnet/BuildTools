// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace KoreBuild.Tasks.Lineup
{
    internal class PackageVersionSource
    {
        private readonly ILogger _logger;
        private IDictionary<string, IDictionary<NuGetFramework, LineupVersion>> _packageLineupMap;
        private List<PackageIdentity> _lineups;

        public PackageVersionSource(ILogger logger)
        {
            _logger = logger;
            _packageLineupMap = new Dictionary<string, IDictionary<NuGetFramework, LineupVersion>>(StringComparer.OrdinalIgnoreCase);
            _lineups = new List<PackageIdentity>();
        }

        public IEnumerable<PackageIdentity> Lineups => _lineups;

        public void AddPackagesFromLineup(string lineupId, NuGetVersion lineupVersion, IReadOnlyCollection<PackageDependencyGroup> dependencyGroups)
        {
            _lineups.Add(new PackageIdentity(lineupId, lineupVersion));

            foreach (var depGroup in dependencyGroups)
            {
                foreach (var package in depGroup.Packages)
                {
                    if (!package.VersionRange.HasLowerBound)
                    {
                        _logger.LogWarning($"Lineup '{lineupId}' is missing a lower version bound for '{package.Id}'. Lineups should include a specific minimum version, so this package will be skipped.");
                        continue;
                    }

                    var packageIdentity = new PackageIdentity(package.Id, package.VersionRange.MinVersion);
                    AddPackage(packageIdentity, depGroup.TargetFramework, lineupId);
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

                AddPackage(package, NuGetFramework.AnyFramework, folder);
                _logger.LogVerbose($"Using package {package.Id} {package.Version} from {packageFile}");
            }
        }

        public bool TryGetPackageVersion(string id, NuGetFramework targetFramework, out string version)
        {
            if (!_packageLineupMap.TryGetValue(id, out var versions))
            {
                version = null;
                return false;
            }

            var lineupFramework = NuGetFrameworkUtility.GetNearest(versions.Keys, targetFramework, f => f);
            if (lineupFramework == null)
            {
                version = null;
                return false;
            }

            version = versions[lineupFramework].Package.Version.ToNormalizedString();
            return true;
        }

        private void AddPackage(PackageIdentity package, NuGetFramework framework, string lineupName)
        {
            if (!_packageLineupMap.TryGetValue(package.Id, out var lineups))
            {
                lineups = _packageLineupMap[package.Id] = new Dictionary<NuGetFramework, LineupVersion>(new NuGetFrameworkFullComparer());
            }

            if (lineups.TryGetValue(framework, out var existingLineup))
            {
                _logger.LogError($"Packages version for '{package.Id}'/{framework} found from multiple lineups: {lineupName} and {existingLineup.LineupName}. Cannot determine which one to use.");
            }
            else
            {
                lineups.Add(framework, new LineupVersion { LineupName = lineupName, Package = package });
            }
        }

        private class LineupVersion
        {
            public string LineupName { get; set; }
            public PackageIdentity Package { get; set; }
        }
    }
}
