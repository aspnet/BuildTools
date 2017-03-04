// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    public class PackageRestoreContext : IDisposable
    {
        private static readonly string TempRestoreFolderName = "TempRestorePackages";
        private readonly IEnumerable<string> _sourceFolders;
        private readonly ILogger _logger;
        private readonly string _manifest;
        private readonly string _dotnetSdkPath;

        public PackageRestoreContext(
            string manifest,
            string destination,
            string dotnetSdkPath,
            IEnumerable<string> sourceFolders,
            ILogger logger)
        {
            RestoreFolder = Path.GetFullPath(Path.Combine(destination, TempRestoreFolderName));
            _manifest = manifest;
            _sourceFolders = sourceFolders;
            _dotnetSdkPath = dotnetSdkPath ?? "dotnet";

            _logger = logger;
            Quiet = false;
        }

        public string RestoreFolder { get; }

        public bool Quiet { get; set; }

        public bool Restore()
        {
            var buffer = new StringBuilder();
            buffer.AppendLine("Restore packages");
            buffer.AppendLine($"  restore folder: {RestoreFolder}");
            buffer.AppendLine($"  source folders: {string.Join(",", _sourceFolders)}");
            _logger.LogInformation(buffer.ToString());

            var sources = _sourceFolders.Any() ? string.Join(" ", _sourceFolders.Select(v => $"--source {v}")) : string.Empty;
            var packages = $"--packages {RestoreFolder}";

            var arguments = string.Join(
                " ",
                "restore",
                _manifest,
                packages,
                sources);

            if (!Quiet)
            {
                arguments += " --verbosity Normal";
            }

            var exitCode = new ProcessRunner(_dotnetSdkPath, arguments)
                .AddEnvironmentVariable("NUGET_XMLDOC_MODE", "Skip")
                .WriteOutputToConsole()
                .WriteOutputToConsole()
                .Run();

            return exitCode == 0;
        }

        public void Dispose()
        {
            if (Directory.Exists(RestoreFolder))
            {
                Directory.Delete(RestoreFolder, recursive: true);
            }
        }
    }
}
