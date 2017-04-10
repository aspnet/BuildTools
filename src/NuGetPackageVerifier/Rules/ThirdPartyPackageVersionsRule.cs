// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier.Rules
{
    public class ThirdPartyPackageVersionsRule : IPackageVerifierRule
    {
        private static readonly ThirdPartyPackageVersionsConfig Config = GetConfig();

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var dependencySet in context.Metadata.DependencyGroups)
            {
                foreach (var package in dependencySet.Packages)
                {
                    var issue = VerifyPackageReference(context.Metadata, dependencySet, package);
                    if (issue != null)
                    {
                        yield return issue;
                    }
                }
            }
        }

        private PackageVerifierIssue VerifyPackageReference(IPackageMetadata contextMetadata, PackageDependencyGroup dependencySet, PackageDependency package)
        {
            if (Config.PrefixWhitelist.Any(prefix => package.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (!Config.PrefixBlacklist.Any(prefix => package.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    // Package is in whitelist and not in the blacklist
                    return null;
                }
            }

            var packageVersion = package.VersionRange.MinVersion.ToString();
            if (!Config.Packages.TryGetValue(package.Id, out var expectedVersions))
            {
                return PackageIssueFactory.PackageHasUnregisteredThirdPartyDependency(
                    contextMetadata.Id,
                    dependencySet.TargetFramework.DotNetFrameworkName,
                    package.Id,
                    packageVersion);
            }

            if (!expectedVersions.Contains(packageVersion))
            {
                return PackageIssueFactory.PackageHasWrongThirdPartyDependencyVersion(
                    contextMetadata.Id,
                    dependencySet.TargetFramework.DotNetFrameworkName,
                    package.Id,
                    packageVersion,
                    string.Join(", ", expectedVersions));
            }

            return null;
        }

        private static ThirdPartyPackageVersionsConfig GetConfig()
        {
            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            var assembly = typeof(ThirdPartyPackageVersionsRule).GetTypeInfo().Assembly;
            using (var stream = assembly.GetManifestResourceStream("NuGetPackageVerifier.third-party.json"))
            using (var reader = new StreamReader(stream))
            {
                return JsonConvert.DeserializeObject<ThirdPartyPackageVersionsConfig>(reader.ReadToEnd(), serializerSettings);
            }
        }
    }

    internal class ThirdPartyPackageVersionsConfig
    {
        public string[] PrefixWhitelist { get; set; }
        public string[] PrefixBlacklist { get; set; }
        public IDictionary<string, string[]> Packages { get; set; }
    }
}
