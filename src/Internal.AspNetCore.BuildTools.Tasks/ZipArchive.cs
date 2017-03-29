// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace Microsoft.AspNetCore.BuildTools
{
    public class ZipArchive : Task
    {
        /// <summary>
        /// The path where the zip file should be created.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// Files to be added to <see cref="File"/>.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        /// <summary>
        /// The directory to use as the base directory. The entry path
        /// for each item in <see cref="SourceFiles"/> is relative to this.
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Overwrite <see cref="File"/> if it exists. Defaults to false.
        /// </summary>
        public bool Overwrite { get; set; }

        public override bool Execute()
        {
            var workDir = FileHelpers.EnsureTrailingSlash(WorkingDirectory);

            foreach (var file in SourceFiles)
            {
                if (!string.IsNullOrEmpty(file.GetMetadata("Link")))
                {
                    continue;
                }

                if (!file.ItemSpec.StartsWith(workDir))
                {
                    Log.LogError("Item {0} is not inside the working directory {1}. Set the metadata 'Link' to file path that should be used within the zip archive",
                        file.ItemSpec,
                        workDir);
                    return false;
                }

                file.SetMetadata("Link", file.ItemSpec.Substring(workDir.Length));
            }

            var fileMode = Overwrite
                ? FileMode.Create
                : FileMode.OpenOrCreate;

            var archiveMode = Overwrite
                ? ZipArchiveMode.Create
                : ZipArchiveMode.Update;

            Directory.CreateDirectory(Path.GetDirectoryName(File));

            using (var stream = new FileStream(File, fileMode))
            using (var zip = new ZipArchiveStream(stream, archiveMode))
            {
                foreach (var file in SourceFiles)
                {
                    zip.CreateEntryFromFile(file.ItemSpec, file.GetMetadata("Link"));
                    Log.LogMessage("Added '{0}' to archive", file.ItemSpec);
                }
            }

            Log.LogMessage(MessageImportance.High, "Added {0} file(s) to '{1}'", SourceFiles.Length, File);

            return true;
        }
    }
}
