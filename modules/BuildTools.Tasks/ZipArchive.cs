// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ZipArchiveStream = System.IO.Compression.ZipArchive;
using IOFile = System.IO.File;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_ZipArchive : Task
#elif BuildTools
    public class ZipArchive : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
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
            if (!Overwrite && IOFile.Exists(File))
            {
                Log.LogError($"Zip file {File} already exists. Set Overwrite=true to replace it.");
                return false;
            }

            var workDir = FileHelpers.EnsureTrailingSlash(WorkingDirectory).Replace('\\', '/');

            foreach (var file in SourceFiles)
            {
                if (!string.IsNullOrEmpty(file.GetMetadata("Link")))
                {
                    continue;
                }

                var filePath = file.ItemSpec.Replace('\\', '/');
                if (!filePath.StartsWith(workDir))
                {
                    Log.LogError("Item {0} is not inside the working directory {1}. Set the metadata 'Link' to file path that should be used within the zip archive",
                        filePath,
                        workDir);
                    return false;
                }

                file.SetMetadata("Link", filePath.Substring(workDir.Length));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(File));

            using (var stream = IOFile.Create(File))
            using (var zip = new ZipArchiveStream(stream, ZipArchiveMode.Create))
            {
                foreach (var file in SourceFiles)
                {
                    var entryName = file.GetMetadata("Link").Replace('\\', '/');
                    if (string.IsNullOrEmpty(Path.GetFileName(entryName)))
                    {
                        Log.LogError("Empty file names not allowed. The effective entry path for item '{0}' is '{1}'", file.ItemSpec, entryName);
                        return false;
                    }

                    var entry = zip.CreateEntryFromFile(file.ItemSpec, entryName);
#if NET472
#elif NETCOREAPP3_0 || NETSTANDARD2_0
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // This isn't required when creating a zip on Windows. unzip will check which
                        // platform was used to create the zip file. If the zip was created on Windows,
                        // unzip will use a default set of permissions. However, if the zip was created
                        // on a Unix-y system, it will set the permissions as defined in the external_attr
                        // field.

                        // Set the file permissions on each entry so they are extracted correctly on Unix.
                        // Picking -rw-rw-r-- by default because we don't yet have a good way to access existing
                        // Unix permissions. If we don't set this, files may be extracted as ---------- (0000),
                        // which means the files are completely unusable.

                        // FYI - this may not be necessary in future versions of .NET Core. See https://github.com/dotnet/corefx/issues/17342.
                        const int rw_rw_r = (0x8000 + 0x0100 + 0x0080 + 0x0020 + 0x0010 + 0x0004) << 16;
                        entry.ExternalAttributes = rw_rw_r;
                    }
#else
#error Update target frameworks
#endif
                    Log.LogMessage("Added '{0}' to archive", entry.FullName);
                }
            }

            var fileInfo = new FileInfo(File);
            Log.LogMessage(MessageImportance.High,
                $"Added {SourceFiles.Length} file(s) to '{File}' ({fileInfo.Length / 1024:n0} KB)");

            return true;
        }
    }
}
