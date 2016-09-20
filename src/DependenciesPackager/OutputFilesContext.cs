using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    internal class OutputFilesContext : IDisposable
    {
        private const int CrossGenFlag = 4;
        private readonly string _restoreFolder;
        private readonly Project _project;
        private ILogger _logger;

        private readonly HashSet<string> _dependencies;

        public OutputFilesContext(
            string desitnation,
            string version,
            string runtime,
            string restoreFolder,
            Project project,
            ILogger logger)
        {
            var architecture = runtime.Substring(runtime.LastIndexOf('-') + 1);
            OutputPath = Path.GetFullPath(Path.Combine(desitnation, version, architecture));

            _restoreFolder = restoreFolder;
            _project = project;
            _logger = logger;

            _dependencies = new HashSet<string>(_project.Dependencies.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        }

        public string OutputPath { get; }

        public void CompareOutputToRestore()
        {
            var assemblies = Directory.EnumerateFiles(_restoreFolder, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.EnumerateFiles(_restoreFolder, "*.sha512", SearchOption.AllDirectories);
            var dependencies = _project.Dependencies;

            var restoreFolderPathLen = _restoreFolder.Length + 1;
            var files = assemblies.Concat(signatures)
                .Where(file => !file.Contains("net451") && !file.Contains("Microsoft.NETCore.App"))
                .Where(file => dependencies.Any(dep => file.Contains(dep.Name)))
                .Select(file => file.Substring(restoreFolderPathLen));

            var qualifiedFilesInRestoreFolder = new HashSet<string>(files);

            var outputPathLen = OutputPath.Length + 1;
            var outputFilePaths = Directory.EnumerateFiles(OutputPath, "*", SearchOption.AllDirectories);
            var outputFileRelativePaths = new HashSet<string>(outputFilePaths.Select(path => path.Remove(0, outputPathLen)));

            var missingFiles = qualifiedFilesInRestoreFolder.Except(outputFileRelativePaths, StringComparer.OrdinalIgnoreCase);
            foreach (var file in missingFiles)
            {
                _logger.LogMissingFile(file, Path.Combine(OutputPath, file));
            }
        }

        public void RemoveExtraOutputFiles()
        {
            foreach (var dir in Directory.GetDirectories(OutputPath))
            {
                if (!_dependencies.Contains(Path.GetFileName(dir)))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }

        public void PrintDiagnostics()
        {
            var files = Directory.GetFiles(OutputPath, "*", SearchOption.AllDirectories);
            var notCrossgend = new List<string>();
            var buffer = new StringBuilder();
            buffer.AppendLine($"There are {files.Count()} files in output:");

            foreach (var file in files)
            {
                buffer.AppendLine($"    {file}");

                if (Path.GetExtension(file) == ".dll")
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var reader = new PEReader(stream);
                        var crossgened = ((int)reader.PEHeaders.CorHeader.Flags & CrossGenFlag) == CrossGenFlag;
                        if (!crossgened)
                        {
                            notCrossgend.Add(file);
                        }
                    }
                }
            }

            _logger.LogInformation(buffer.ToString());

            if (notCrossgend.Any())
            {
                buffer.Clear();
                buffer.AppendLine($"Following files are not crossgened.");
                foreach (var file in notCrossgend)
                {
                    buffer.AppendLine($"    {file}");
                }
            }

            _logger.LogWarning(buffer.ToString());
        }

        public static void Compress(
            IEnumerable<OutputFilesContext> outputs,
            string destination,
            string prefix,
            string runtime,
            bool skip,
            ILogger logger)
        {
            if (skip || !outputs.Any())
            {
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
