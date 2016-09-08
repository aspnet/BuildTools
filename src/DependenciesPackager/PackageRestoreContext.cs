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
        private readonly IEnumerable<string> _fallbackFeeds;
        private readonly IEnumerable<string> _sourceFolders;
        private readonly ILogger _logger;
        private readonly string _manifest;
        private readonly string _dotnetSdkPath;

        public PackageRestoreContext(
            string manifest,
            string destination,
            string dotnetSdkPath,
            IEnumerable<string> sourceFolders,
            IEnumerable<string> fallbackFeeds,
            ILogger logger)
        {
            RestoreFolder = Path.GetFullPath(Path.Combine(destination, TempRestoreFolderName));
            _manifest = manifest;
            _sourceFolders = sourceFolders;
            _fallbackFeeds = fallbackFeeds;
            _dotnetSdkPath = dotnetSdkPath ?? "dotnet";

            _logger = logger;
            Quiet = false;
        }

        public string RestoreFolder { get; }

        public bool Quiet { get; set; }

        public bool RestoreAndRemoveUnecessaryFiles()
        {
            if (RunDotnetRestore() != 0)
            {
                return false;
            }

            RemoveUnnecessaryRestoredFiles();
            return true;
        }

        public void Dispose()
        {
            if (Directory.Exists(RestoreFolder))
            {
                Directory.Delete(RestoreFolder, recursive: true);
            }
        }

        private void RemoveUnnecessaryRestoredFiles()
        {
            var files = Directory.GetFiles(RestoreFolder, "*", SearchOption.AllDirectories);
            var dlls = Directory.GetFiles(RestoreFolder, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.GetFiles(RestoreFolder, "*.sha512", SearchOption.AllDirectories);
            var crossgen = CrossGenTool.FindAllCrossGen(RestoreFolder);
            var clrjit = CrossGenTool.FindAllClrJIT(RestoreFolder);

            _logger.LogInformation($@"Dlls in restored packages:
{string.Join($"    {Environment.NewLine}", dlls)}");

            _logger.LogInformation($@"Signature files in restored packages:
{string.Join($"    {Environment.NewLine}", signatures)}");

            var filesToRemove = files.Except(dlls.Concat(signatures).Concat(crossgen).Concat(clrjit));
            foreach (var file in filesToRemove)
            {
                _logger.LogInformation($"Removing file '{file}.");
                File.Delete(file);
            }
        }

        private int RunDotnetRestore()
        {
            var buffer = new StringBuilder();
            buffer.AppendLine("Restore packages");
            buffer.AppendLine($"  restore folder: {RestoreFolder}");
            buffer.AppendLine($"  source folders: {string.Join(",", _sourceFolders)}");
            buffer.AppendLine($"  fallback feeds: {string.Join(",", _fallbackFeeds)}");
            _logger.LogInformation(buffer.ToString());

            var sources = _sourceFolders.Any() ? string.Join(" ", _sourceFolders.Select(v => $"--source {v}")) : string.Empty;
            var fallbackFeeds = _fallbackFeeds.Any() ? string.Join(" ", _fallbackFeeds.Select(v => $"--fallbacksource {v} ")) : string.Empty;
            var packages = $"--packages {RestoreFolder}";

            var arguments = string.Join(
                " ",
                "restore",
                _manifest,
                packages,
                sources,
                fallbackFeeds);

            if (!Quiet)
            {
                arguments += " --verbosity Verbose";
            }

            return new ProcessRunner(_dotnetSdkPath, arguments)
                .AddEnvironmentVariable("NUGET_XMLDOC_MODE", "Skip")
                .WriteOutputToConsole()
                .WriteOutputToConsole()
                .Run();
        }
    }
}
