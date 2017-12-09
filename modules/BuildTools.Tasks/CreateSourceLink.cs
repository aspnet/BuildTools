// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var data = new JObject
            {
                ["documents"] = new JObject
                {
                    [SourceLinkRoot] = codeSource
                }
            };
            File.WriteAllText(DestinationFile, data.ToString(Formatting.None));

            SourceLinkFile = DestinationFile;

            return true;
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
    }
}
