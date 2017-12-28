// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGetPackageVerifier.Manifests
{
    public class SignRequestManifest
    {
        /// <summary>
        /// Represents all signing requests in the sign request manifest that are for nupkg files.
        /// </summary>
        public IReadOnlyDictionary<string, PackageSignRequest> PackageSignRequests { get; private set; }

        public static SignRequestManifest Parse(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                return Parse(reader, Path.GetDirectoryName(filePath));
            }
        }

        public static SignRequestManifest Parse(TextReader reader, string manifestBasePath)
        {
            var doc = XDocument.Load(reader);
            var requests = new Dictionary<string, PackageSignRequest>(StringComparer.OrdinalIgnoreCase);
            var manifest = new SignRequestManifest { PackageSignRequests = requests };

            var nupkgContainers = doc.Root
                .Elements("Container")
                .Where(c => "nupkg".Equals(c.Attribute("Type")?.Value, StringComparison.Ordinal));

            foreach (var container in nupkgContainers)
            {
                var request = new PackageSignRequest
                {
                    FilesToSign = container.Elements("File").Select(GetPath).ToHashSet(StringComparer.Ordinal),
                    FilesExcludedFromSigning = container.Elements("ExcludedFile").Select(GetPath).ToHashSet(StringComparer.Ordinal),
                };

                var path = new FileInfo(Path.Combine(manifestBasePath, GetPath(container))).FullName;

                requests.Add(path, request);
            }

            return manifest;
        }

        private static string GetPath(XElement element) => element.Attribute("Path")?.Value;
    }
}
