// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Generates a source link JSON file.
    /// <para>
    /// <seealso href="https://github.com/dotnet/core/blob/aa68ce453ad63152bf321c86eb83f9420114d087/Documentation/diagnostics/source_link.md" />
    /// </para>
    /// </summary>
#if SDK
    public class Sdk_CreateSourceLink : Task
#elif BuildTools
    public class CreateSourceLink : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        [Required]
        public string SourceLinkRoot { get; set; }

        [Required]
        public string OriginUrl { get; set; }

        [Required]
        public string DestinationFile { get; set; }

        [Required]
        public string Commit { get; set; }

        [Output]
        public string SourceLinkFile { get; set; }

        public override bool Execute()
        {
            var lastCh = SourceLinkRoot[SourceLinkRoot.Length - 1];
            if (lastCh != '/' && lastCh != '\\')
            {
                Log.LogError("SourceLinkRoot must end with a slash.");
                return false;
            }

            SourceLinkRoot += '*';

            var codeSource = ConvertUrl();

            var mappings = new List<Mapping>
            {
                new Mapping { LocalPath = SourceLinkRoot, Url = codeSource },
            };

            GenerateSourceLink(DestinationFile, mappings);
            SourceLinkFile = DestinationFile;
            return true;
        }

        private static void GenerateSourceLink(string filePath, List<Mapping> mappings)
        {
            var data = new StringBuilder();
            data.Append("{\"documents\":{");

            // not bullet-proof, but should be good enough for escaping paths
            string JsonEscape(string str)
                => str.Replace(@"\", @"\\").Replace("\"", "\\\"");

            var first = true;
            foreach (var mapping in mappings)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    data.Append(",");
                }

                data
                    .Append('"')
                    .Append(JsonEscape(mapping.LocalPath))
                    .Append("\":\"")
                    .Append(JsonEscape(mapping.Url))
                    .Append('"');
            }

            data.Append("}}");

            File.WriteAllText(filePath, data.ToString());
        }

        private string ConvertUrl()
        {
            if (!OriginUrl.Contains("github.com"))
            {
                throw new ArgumentException("OriginUrl must be for github.com", "OriginUrl");
            }

            var prefix = OriginUrl.StartsWith("git@github.com:")
                ? "git@github.com:"
                : "https://github.com/";

            var repoName = OriginUrl.Replace(prefix, "");

            repoName = repoName.Replace(".git", "");

            return $"https://raw.githubusercontent.com/{repoName}/{Commit}/*";
        }

        private struct Mapping
        {
            public string LocalPath;
            public string Url;
        }
    }
}
