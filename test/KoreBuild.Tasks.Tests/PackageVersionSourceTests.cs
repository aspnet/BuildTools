// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using KoreBuild.Tasks.Lineup;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class PackageVersionSourceTests
    {
        [Fact]
        public void PicksTheRightNetCoreAppVersion()
        {
            const string pkgId = "Microsoft.NETCore.App";
            var netcoreappPackages = new(NuGetFramework tfm, string version)[]
            {
                (FrameworkConstants.CommonFrameworks.NetCoreApp10, "1.0.5"),
                (FrameworkConstants.CommonFrameworks.NetCoreApp11, "1.1.2"),
                (FrameworkConstants.CommonFrameworks.NetCoreApp20, "2.0.0"),
                (MyFrameworks.NetCoreApp21, "2.1.0-preview1"),
            };

            var source = new PackageVersionSource(NullLogger.Instance);
            source.AddPackagesFromLineup("TestLineup", NuGetVersion.Parse("1.0.0"), netcoreappPackages.Select(pair =>
                    new PackageDependencyGroup(pair.tfm, new[] { new PackageDependency(pkgId, VersionRange.Parse(pair.version)) })).ToArray());

            foreach (var pair in netcoreappPackages)
            {
                Assert.True(source.TryGetPackageVersion(pkgId, pair.tfm, out var selectedVersion));
                Assert.Equal(pair.version, selectedVersion);
            }
        }

        [Theory]
        [MemberData(nameof(FrameworkLineupData))]
        public void PicksTheMostCompatibleFramework(NuGetFramework targetFramework, string expectedVersion)
        {
            const string pkgId = "Test";
            var source = new PackageVersionSource(NullLogger.Instance);
            source.AddPackagesFromLineup("TestLineup", NuGetVersion.Parse("1.0.0"), new[]
            {
                new PackageDependencyGroup(FrameworkConstants.CommonFrameworks.NetStandard16, new[] { new PackageDependency(pkgId, VersionRange.Parse("1.6.0")) }),
                new PackageDependencyGroup(FrameworkConstants.CommonFrameworks.NetStandard20, new[] { new PackageDependency(pkgId, VersionRange.Parse("2.0.0")) }),
                new PackageDependencyGroup(FrameworkConstants.CommonFrameworks.Net461, new[] { new PackageDependency(pkgId, VersionRange.Parse("3.0.0")) }),
                new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] { new PackageDependency(pkgId, VersionRange.Parse("1.0.0")) }),
            });

            source.TryGetPackageVersion(pkgId, targetFramework, out var selectedVersion);
            Assert.Equal(expectedVersion, selectedVersion);
        }

        public static TheoryData<NuGetFramework, string> FrameworkLineupData
            => new TheoryData<NuGetFramework, string>
            {
                { FrameworkConstants.CommonFrameworks.NetStandard13, "1.0.0" },
                { FrameworkConstants.CommonFrameworks.NetStandard16, "1.6.0" },
                { FrameworkConstants.CommonFrameworks.NetCoreApp11, "1.6.0" },
                { FrameworkConstants.CommonFrameworks.NetCoreApp20, "2.0.0" },
                { FrameworkConstants.CommonFrameworks.Net46, "1.0.0" },
                { FrameworkConstants.CommonFrameworks.Net461, "3.0.0" },
            };

        [Fact]
        public void DoesNotPickAVersionForIncompatibleFramework()
        {
            const string pkgId = "Test";
            var source = new PackageVersionSource(NullLogger.Instance);
            source.AddPackagesFromLineup("TestLineup", NuGetVersion.Parse("1.0.0"), new[]
            {
                new PackageDependencyGroup(FrameworkConstants.CommonFrameworks.NetStandard16, new[] { new PackageDependency(pkgId, VersionRange.Parse("1.0.0")) })
            });

            Assert.False(source.TryGetPackageVersion(pkgId, NuGetFramework.AgnosticFramework, out var _));
            Assert.False(source.TryGetPackageVersion(pkgId, FrameworkConstants.CommonFrameworks.NetStandard15, out var _));
            Assert.False(source.TryGetPackageVersion(pkgId, FrameworkConstants.CommonFrameworks.Net45, out var _));
        }
    }
}
