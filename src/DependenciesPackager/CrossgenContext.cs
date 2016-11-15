using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    internal class CrossgenContext : IDisposable
    {
        private static readonly string TempStagingFolderName = "TempPublish";

        private readonly ProjectContext _context;
        private readonly ILogger _logger;
        private readonly string _stagingFolder;

        private IEnumerable<PackageEntry> _packageEntries = Enumerable.Empty<PackageEntry>();
        private string _crossgenExecutable;

        public CrossgenContext(ProjectContext context, string destinationFolder, ILogger logger)
        {
            _context = context;
            _logger = logger;
            _stagingFolder = GetTempStagingFolder(Path.Combine(destinationFolder, TempStagingFolderName));
        }

        public void PrintCrossgenOutput()
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine("Output of running crossgen of assemblies:");

            foreach (var package in _packageEntries)
            {
                errorMessage.AppendLine($"    Output for assets in {package.Library.Identity}");
                foreach (var entry in package.CrossGenOutput)
                {
                    errorMessage.AppendLine($"    Output for asset {entry.Key.ResolvedPath}");
                    foreach (var line in entry.Value)
                    {
                        errorMessage.AppendLine(line);
                    }
                }
            }

            _logger.LogInformation(errorMessage.ToString());
        }

        public void CollectPackages(string runtime, string restoreFolder)
        {
            _packageEntries = _context.GetPackageEntries(runtime, restoreFolder);
        }

        public void PopulatePublishFolder()
        {
            foreach (var asset in _packageEntries.SelectMany(entry => entry.Assets))
            {
                var path = asset.ResolvedPath;
                var targetPath = Path.Combine(_stagingFolder, asset.FileName);

                _logger.LogFileCopy(path, targetPath);
                File.Copy(path, targetPath, overwrite: true);
            }
        }

        public void FetchCrossgenTools(string runtime)
        {
            var runtimePackageName = $"runtime.{runtime}.Microsoft.NETCore.Runtime.CoreCLR";
            var entry = _packageEntries.SingleOrDefault(
                p => p.Library.Identity.Name.Equals(runtimePackageName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                throw new InvalidOperationException($"Cannot find package {runtimePackageName}. It contains the crossgen tool.");
            }

            var executable = CrossGenTool.GetCrossGenTool(runtime).CrossGen;
            var executablePath = Directory.GetFiles(entry.Library.Path, executable, SearchOption.AllDirectories).SingleOrDefault();

            if (executablePath == null)
            {
                throw new InvalidOperationException($"Cannot find executable {executable}. It is required for crossgen.");
            }

            var crossgenDir = Path.GetDirectoryName(executablePath);
            foreach (var file in Directory.GetFiles(crossgenDir))
            {
                File.Copy(file, Path.Combine(_stagingFolder, Path.GetFileName(file)), overwrite: true);
            }

            _crossgenExecutable = Path.GetFullPath(Path.Combine(_stagingFolder, Path.GetFileName(executablePath)));
        }

        public void FetchJitTools(string runtime, string restoreFolder)
        {
            var jitPackageName = $"runtime.{runtime}.Microsoft.NETCore.Jit";
            var jitPackagePath = Path.Combine(restoreFolder, jitPackageName);
            if (!Directory.Exists(jitPackagePath))
            {
                throw new InvalidOperationException($"Cannot find package {jitPackageName}. It contains the CLR JIT tool.");
            }

            var jitExecutable = CrossGenTool.GetCrossGenTool(runtime).ClrJit;
            var jitExecutablePath = Directory.GetFiles(jitPackagePath, jitExecutable, SearchOption.AllDirectories).SingleOrDefault();

            if (jitExecutablePath == null)
            {
                throw new InvalidOperationException($"Cannot find executable {jitExecutable}.");
            }

            var jitDir = Path.GetDirectoryName(jitExecutablePath);
            foreach (var file in Directory.GetFiles(jitDir))
            {
                File.Copy(file, Path.Combine(_stagingFolder, Path.GetFileName(file)), overwrite: true);
            }
        }

        public void Crossgen(string outputPath)
        {
            foreach (var package in _packageEntries)
            {
                foreach (var asset in package.Assets)
                {
                    var subDir = CreateSubDirectory(outputPath, package, asset);
                    var targetPath = Path.Combine(subDir.FullName, asset.FileName);

                    if (!CrossgenAssembly(_stagingFolder, package, asset, targetPath))
                    {
                        _logger.LogWarning($"Failed to crossgen asset {asset.FileName}. Copy the original asset instead.");
                        File.Copy(asset.ResolvedPath, targetPath, overwrite: true);
                    }
                }
            }
        }

        public void CopyPackageSignatures(string outputPath)
        {
            foreach (var entry in _packageEntries.Where(entry => entry.Library is PackageDescription))
            {
                var desp = (PackageDescription)entry.Library;
                var hash = desp.PackageLibrary.Files.Single(file => file.EndsWith(".sha512"));
                var desination = Path.Combine(outputPath,
                    entry.Library.Identity.Name,
                    entry.Library.Identity.Version.ToNormalizedString(),
                    hash);

                File.Copy(Path.Combine(entry.Library.Path, hash), desination, overwrite: true);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_stagingFolder))
            {
                Directory.Delete(_stagingFolder, recursive: true);
            }
        }

        private bool CrossgenAssembly(string publishFolder, PackageEntry package, LibraryAsset asset, string targetPath)
        {
            var assemblyPath = Path.GetFullPath(asset.ResolvedPath);
            var arguments = $"/Platform_Assemblies_Paths {publishFolder} /in {assemblyPath} /out {targetPath}";

            _logger.LogCrossgenArguments(publishFolder, assemblyPath, targetPath, arguments);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var exitCode = new ProcessRunner(_crossgenExecutable, arguments)
                .WithWorkingDirectory(publishFolder)
                .WriteOutputToStringBuilder(stdout, "    ")
                .WriteErrorsToStringBuilder(stderr, "    ")
                .Run();

            package.CrossGenOutput.Add(asset, new List<string> { stdout.ToString(), stderr.ToString() });

            _logger.LogCrossgenResult(exitCode, targetPath);

            return exitCode == 0;
        }

        private DirectoryInfo CreateSubDirectory(string cacheBasePath, PackageEntry package, LibraryAsset asset)
        {
            // special treatment of the cases in the path to accommodating NuGet package search
            // https://github.com/aspnet/BuildTools/issues/94
            // https://github.com/NuGet/Home/issues/2522
            var subDirectoryPath = Path.Combine(
                cacheBasePath,
                package.Library.Identity.Name,
                package.Library.Identity.Version.ToNormalizedString(),
                Path.GetDirectoryName(asset.RelativePath));

            _logger.LogInformation($"Creating sub directory on {subDirectoryPath}.");

            return Directory.CreateDirectory(subDirectoryPath);
        }

        private string GetTempStagingFolder(string parent)
        {
            var publishFolder = _context.GetStagingFolderPath(parent);

            if (!Directory.Exists(publishFolder))
            {
                _logger.LogInformation($"Creating directory {publishFolder}.");
                Directory.CreateDirectory(publishFolder);
            }

            return publishFolder;
        }
    }
}
