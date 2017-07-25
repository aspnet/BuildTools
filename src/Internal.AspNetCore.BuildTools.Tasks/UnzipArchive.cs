﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ZipArchiveStream = System.IO.Compression.ZipArchive;
using IOFile = System.IO.File;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Unzips an archive file.
    /// </summary>
#if SDK
    public class Sdk_UnzipArchive : Task
#elif BuildTools
    public class UnzipArchive : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        /// <summary>
        /// The file to unzip.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The directory where files will be unzipped.
        /// </summary>
        /// <returns></returns>
        [Required]
        public string Destination { get; set; }

        /// <summary>
        /// Overwrite <see cref="File"/> if it exists. Defaults to false.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// The files that were unzipped.
        /// </summary>
        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        public override bool Execute()
        {
            if (!IOFile.Exists(File))
            {
                Log.LogError("'{0}' does not exist", File);
                return false;
            }

            Directory.CreateDirectory(Destination);

            var output = new List<ITaskItem>();
            using (var stream = new FileStream(File, FileMode.Open))
            using (var zip = new ZipArchiveStream(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    var fileDest = Path.Combine(Destination, entry.FullName);
                    var dirName = Path.GetDirectoryName(fileDest);
                    Directory.CreateDirectory(dirName);
                    entry.ExtractToFile(fileDest, Overwrite);
                    Log.LogMessage(MessageImportance.Low, "Extracted '{0}'", fileDest);
                    output.Add(new TaskItem(fileDest));
                }
            }

            Log.LogMessage(MessageImportance.High, "Extracted {0} file(s) to '{1}'", output.Count, Destination);
            OutputFiles = output.ToArray();

            return true;
        }
    }
}
