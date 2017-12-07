// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_CreateSourceLink : Task
#elif BuildTools
    public class CreateSourceLink : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        [Required]
        public string RootDirectory { get; set; }

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
            var rootDirectory = Path.Combine(Path.GetFullPath(RootDirectory), "*");

            rootDirectory = rootDirectory.Replace("/", "\\");
            rootDirectory = rootDirectory.Replace("\\", "\\\\");

            var codeSource = ConvertUrl();

            File.WriteAllText(DestinationFile, $"{{\"documents\":{{\"{rootDirectory}\":\"{codeSource}\"}}}}");

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
