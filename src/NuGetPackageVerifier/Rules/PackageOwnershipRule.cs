﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class PackageOwnershipRule : IPackageVerifierRule
    {
        // Per https://github.com/NuGet/Home/issues/2178
        private const string NuGetV3Endpoint = "https://api-v2v3search-0.nuget.org/search/query?q=Id:{0}&prerelease=true&take=1&ignoreFilter=true";
        private const string NuGetOrgPackagePage = "https://www.nuget.org/packages/";
        private static readonly string[] AllowedOwners = {
            "aspnet",
            "microsoft",
        };
        private static readonly IList<string> OwnedPackages = GetOwnedPackages();
        private static readonly HttpClient HttpClient = new HttpClient();

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (OwnedPackages.Contains(context.Metadata.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield break;
            }

            context.Logger.LogWarning($"Looking up ownership for package {context.Metadata.Id}. Add this package to the owned-packages.txt list if it's owned.");
            var url = string.Format(CultureInfo.InvariantCulture, NuGetV3Endpoint, context.Metadata.Id);
            var result = GetPackageSearchResultAsync(context.Logger, url).Result;
            if (result?.Data.Length == 0)
            {
                yield return PackageIssueFactory.IdDoesNotExist(context.Metadata.Id);
            }
            else
            {
                var registration = result.Data[0].PackageRegistration;
                var owners = registration.Owners;

                if (owners.Length == 0)
                {
                    // The API result can sometimes be empty and not contain any owner data.
                    var packagePage = NuGetOrgPackagePage + context.Metadata.Id + "/0.0.1-alpha";
                    using (var httpResponse = HttpClient.GetAsync(packagePage).Result)
                    {
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            context.Logger.LogWarning($"Unable to read content from {packagePage}. Response code {httpResponse.StatusCode}.");
                            yield return PackageIssueFactory.IdDoesNotExist(context.Metadata.Id);
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
                    yield return PackageIssueFactory.IdIsNotOwned(context.Metadata.Id, AllowedOwners);
                }
            }
        }

        private async Task<PackageSearchResult> GetPackageSearchResultAsync(IPackageVerifierLogger logger, string url)
        {
            using (var httpResponse = await HttpClient.GetAsync(url))
            {
                if (!httpResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning($"{NuGetV3Endpoint} request failed. Response code {httpResponse.StatusCode}.");
                    // The service might be unavailable. Return null here to fallback to screen scrapping the gallery.
                    return null;
                }

                var content = await httpResponse.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<PackageSearchResult>(content);
            }
        }

        private string[] ReadOwnersFromGalleryContent(string content)
        {
            var html = new HtmlDocument();
            html.LoadHtml(content);

            return html.DocumentNode.Descendants("span")
                .Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("owner-name"))
                .Select(node => node.InnerText)
                .ToArray();
        }

        private static IList<string> GetOwnedPackages()
        {
            var assembly = typeof(PackageOwnershipRule).GetTypeInfo().Assembly;
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
