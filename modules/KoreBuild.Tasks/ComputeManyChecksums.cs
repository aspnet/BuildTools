// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Computes the hash value for many files and updates the metadata of an item with its value.
    /// </summary>
    public class ComputeManyChecksum : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// A group of files.
        /// </summary>
        [Required]
        [Output]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// The algorithm. Allowed values: SHA256, SHA384, SHA512.
        /// </summary>
        public string Algorithm { get; set; } = "SHA256";

        /// <summary>
        /// The metadata name to set.
        /// </summary>
        public string MetadataName { get; set; } = "FileHash";

        public override bool Execute()
        {
            foreach (var file in Items)
            {
                file.SetMetadata(MetadataName, HashHelper.GetFileHash(Algorithm, file.ItemSpec));
            }

            return true;
        }
    }
}
