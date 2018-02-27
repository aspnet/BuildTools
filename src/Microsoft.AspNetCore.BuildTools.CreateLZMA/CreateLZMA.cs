// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Archive;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace Microsoft.AspNetCore.BuildTools.LZMA
{
    public class CreateLZMA : MSBuildTask
    {
        [Required]
        public string OutputArchiveFilePath { get; set; }

        [Required]
        public string[] ArchiveSources { get; set; }

        public override bool Execute()
        {
            var progress = new ConsoleProgressReport();
            var archive = new IndexedArchive();

            foreach (var source in ArchiveSources)
            {
                if (Directory.Exists(source))
                {
                    archive.AddDirectory(source, progress);
                }
                else
                {
                    archive.AddFile(source, Path.GetFileName(source));
                }
            }

            archive.Save(OutputArchiveFilePath, progress);


            return true;
        }
    }
}
