// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class PackageOwnershipRule : IPackageVerifierRule
    {
        // Per https://github.com/NuGet/Home/issues/2178
        private const string NuGetV3Endpoint = "https://api-v2v3search-0.nuget.org/search/query?q=Id:{0}&prerelease=true&take=1&ignoreFilter=true";
        private const string NuGetOrgPackagePage = "https://www.nuget.org/packages/";
        private static readonly string[] AllowedOwners = new[]
        {
            "aspnet",
            "microsoft",
        };
        private static readonly IList<string> OwnedPackages = GetOwnedPackages();
        private readonly HttpClient _httpClient = new HttpClient();

        public IEnumerable<PackageVerifierIssue> Validate(FileInfo nupkgFile, IPackageMetadata package, IPackageVerifierLogger logger)
        {
            if (OwnedPackages.Contains(package.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield break;
            }

            logger.LogInfo($"Looking up ownership for package {package.Id}. Add this package to the owned-packages.txt list if it's owned.");

            var url = string.Format(CultureInfo.InvariantCulture, NuGetV3Endpoint, package.Id);
            var jsonResult = _httpClient.GetStringAsync(url).Result;
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageSearchResult>(jsonResult);
            if (result.Data.Length == 0)
            {
                yield return PackageIssueFactory.IdDoesNotExist(package.Id);
            }
            else
            {
                var registration = result.Data[0].PackageRegistration;
                var owners = registration.Owners;

                if (owners.Length == 0)
                {
                    // The API result can sometimes be empty and not contain any owner data.
                    var packagePage = NuGetOrgPackagePage + package.Id;
                    using (var httpResponse = _httpClient.GetAsync(packagePage).Result)
                    {
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            logger.LogWarning($"Unable to read content from {packagePage}. Response code {httpResponse.StatusCode}.");
                            yield return PackageIssueFactory.IdDoesNotExist(package.Id);
                        }
                        else
                        {
                            var content = httpResponse.Content.ReadAsStringAsync().Result;
                            owners = ReadOwnersFromGalleryContent(content);
                        }
                    }
                }

                if (!AllowedOwners.Intersect(owners, StringComparer.OrdinalIgnoreCase).Any())
                {
                    yield return PackageIssueFactory.IdIsNotOwned(package.Id, AllowedOwners);
                }
            }
        }

        private string[] ReadOwnersFromGalleryContent(string content)
        {
            var html = new HtmlDocument();
            html.LoadHtml(content);

            return html.DocumentNode.SelectNodes("//span[contains(@class,'owner-name')]")
                    .Select(node => node.InnerText)
                    .ToArray();
        }

        private static IList<string> GetOwnedPackages()
        {
            var assembly = typeof(PackageOwnershipRule).Assembly;
            using (var stream = assembly.GetManifestResourceStream("NuGetPackageVerifier.already-owned-packages.txt"))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            }
        }

        private class PackageSearchResult
        {
            public PackageSearchResultData[] Data { get; set; }
        }

        private class PackageSearchResultData
        {
            public PackageRegistration PackageRegistration { get; set; }
        }

        private class PackageRegistration
        {
            public string Id { get; set; }

            public string[] Owners { get; set; }
        }
    }
}
