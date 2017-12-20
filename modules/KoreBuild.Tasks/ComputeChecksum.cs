// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Computes the checksum for a single file.
    /// </summary>
    public class ComputeChecksum : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The file path.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The algorithm. Allowed values: SHA256, SHA384, SHA512.
        /// </summary>
        public string Algorithm { get; set; } = "SHA256";

        /// <summary>
        /// The hash.
        /// </summary>
        [Output]
        public string Hash { get; set; }

        public override bool Execute()
        {
            Hash = HashHelper.GetFileHash(Algorithm, File);

            return true;
        }
    }
}
