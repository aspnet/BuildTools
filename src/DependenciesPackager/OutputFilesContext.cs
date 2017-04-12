// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    internal class OutputFilesContext : IDisposable
    {
        private const int CrossGenFlag = 4;
        private readonly ILogger _logger;

        public OutputFilesContext(
            string destination,
            string version,
            string runtime,
            ILogger logger)
        {
            var architecture = runtime.Substring(runtime.LastIndexOf('-') + 1);
            OutputPath = Path.GetFullPath(Path.Combine(destination, version, architecture));

            _logger = logger;
        }

        public string OutputPath { get; }

        public void PrintStatistics()
        {
            var notCrossgend = new List<string>();
            var outBuffer = new StringBuilder();

            outBuffer.AppendLine("Cache contents:");

            var packageWithVersionDirectories = new DirectoryInfo(OutputPath).GetDirectories()
                .Select(d => d.GetDirectories().Single());

            foreach (var directory in packageWithVersionDirectories)
            {
                var filesInPackage = directory.GetFiles("*", SearchOption.AllDirectories);
                outBuffer.AppendLine($"Files for package: {directory.Parent.Name}/{directory.Name}");
                foreach (var file in filesInPackage)
                {
                    if (file.Extension == ".dll")
                    {
                        if (!IsCrossGend(file.FullName))
                        {
                            notCrossgend.Add(file.FullName);
                        }
                    }
                    var directoryLength = directory.FullName.Length;
                    var relativePathLength = file.FullName.Length - directoryLength;
                    outBuffer.AppendLine($"    {file.FullName.Substring(directoryLength, relativePathLength)}");
                }
            }

            _logger.LogInformation(outBuffer.ToString());

            if (notCrossgend.Any())
            {
                var buffer = new StringBuilder();
                buffer.Clear();
                buffer.AppendLine("The following files are not crossgened.");

                foreach (var file in notCrossgend)
                {
                    buffer.AppendLine($"    {file}");
                }

                _logger.LogWarning(buffer.ToString());
            }
        }

        private static bool IsCrossGend(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var reader = new PEReader(stream);
                return ((int)reader.PEHeaders.CorHeader.Flags & CrossGenFlag) == CrossGenFlag;
            }
        }

        public static void Compress(
            IEnumerable<OutputFilesContext> outputs,
            string destination,
            string prefix,
            string runtime,
            bool skip,
            ILogger logger)
        {
            if (skip)
            {
                logger.LogInformation("Skip zipping.");
                return;
            }

            if (!outputs.Any())
            {
                logger.LogInformation("No outputs found. Skip zipping.");
                return;
            }

            if (outputs.Select(o => Path.GetDirectoryName(o.OutputPath)).Distinct().Count() > 1)
            {
                logger.LogError("Output folders are not under the same folder. Skip zipping.");
                return;
            }

            var root = Path.GetDirectoryName(outputs.First().OutputPath);
            var version = Path.GetFileName(root);

            // Marker file used by Antares to keep track of the installed caches
            var versionMarkerPath = Path.Combine(root, $"{version}.version");
            File.WriteAllText(versionMarkerPath, string.Empty);

            var zipFileName = Path.Combine(
                destination,
                $"{prefix}-{version}-{runtime}.zip");

            logger.LogInformation($"Creating zip package on {zipFileName}");
            ZipFile.CreateFromDirectory(root, zipFileName, CompressionLevel.Optimal, includeBaseDirectory: false);
        }

        public void Dispose()
        {
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, recursive: true);
            }
        }
    }
}
