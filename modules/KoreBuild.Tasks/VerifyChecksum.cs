// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Verify the checksum for a single file.
    /// </summary>
    public class VerifyChecksum : Microsoft.Build.Utilities.Task
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
        [Required]
        public string Hash { get; set; }

        public override bool Execute()
        {
            var actualHash = HashHelper.GetFileHash(Algorithm, File);

            if (!actualHash.Equals(Hash, StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError($"Checksum mismatch. Expected {File} to have {Algorithm} checksum of {Hash}, but it was {actualHash}");
                return false;
            }

            return true;
        }
    }
}
