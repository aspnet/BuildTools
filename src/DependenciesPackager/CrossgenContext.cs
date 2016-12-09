using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    internal class CrossgenContext
    {
        private readonly ProjectContext _context;
        private readonly ILogger _logger;
        private bool _preservePackageCasing;

        private IEnumerable<LibraryExport> _exports = Enumerable.Empty<LibraryExport>();
        private IEnumerable<PackageEntry> _packagesToCrossGen = Enumerable.Empty<PackageEntry>();
        private IEnumerable<string> _referenceAssemblyPaths = Enumerable.Empty<string>();

        private IEnumerable<LibraryAsset> _crossGenAssets = Enumerable.Empty<LibraryAsset>();
        private string _clrJitPath;
        private string _crossgenExecutable;

        private readonly string _destinationFolder;
        private string _responseFilePath;
        private readonly string _exclusionsFile;

        public CrossgenContext(ProjectContext context, string destinationFolder, ILogger logger, string exclusionsFile)
        {
            _context = context;
            _logger = logger;
            _destinationFolder = destinationFolder;
            _exclusionsFile = exclusionsFile;
        }

        /// <summary>
        /// Creates the package cache with the original casing of the package names.
        /// This is required for projects created using project.json tooling and NuGet 3.4.
        /// MSBuild and NuGet 4 .ToLower() the package ID.
        /// This only affects package caches on case-sensitive file systems.
        /// </summary>
        public void PreserveOriginalPackageCasing()
        {
            _preservePackageCasing = true;
        }

        public void CollectAssets(string runtime, string restoreFolder)
        {
            var exporter = _context.CreateExporter("CACHE");
            _exports = exporter.GetAllExports().ToArray();

            var netCoreApp = _exports
                .Single(le => le.Library.Identity.Name.Equals("Microsoft.NetCore.App", StringComparison.OrdinalIgnoreCase));
            var netCoreAppDependencies = new HashSet<LibraryExport>(
                GetDependencies(netCoreApp, _exports));
            var packagesToCrossGen = _exports.Except(netCoreAppDependencies);

            var crossGenPackage = _exports
                .Single(le => le.Library.Identity.Name.Equals(
                    GetCrossGenPackageName(runtime),
                    StringComparison.OrdinalIgnoreCase));

            _crossGenAssets = GetAssets(crossGenPackage, runtime);

            var referenceAssembliesPath = new HashSet<string>();
            foreach (var dependency in netCoreAppDependencies)
            {
                var assets = GetAssets(dependency, runtime);
                if (assets == null)
                {
                    continue;
                }

                foreach (var asset in assets)
                {
                    referenceAssembliesPath.Add(GetCleanedUpDirectoryPath(asset));
                }
            }
            _referenceAssemblyPaths = referenceAssembliesPath;

            var result = new List<PackageEntry>();
            foreach (var dependency in packagesToCrossGen)
            {
                var assets = GetAssets(dependency, runtime);
                if (assets == null)
                {
                    continue;
                }

                result.Add(new PackageEntry
                {
                    Library = (PackageDescription)dependency.Library,
                    Assets = assets
                });
            }
            _packagesToCrossGen = result;
        }

        private static IEnumerable<LibraryExport> GetDependencies(LibraryExport dependency, IEnumerable<LibraryExport> allExports)
        {
            if (!dependency.Library.Dependencies.Any())
            {
                yield break;
            }

            foreach (var export in allExports)
            {
                if (dependency.Library.Dependencies
                    .Any(d => d.Name.Equals(export.Library.Identity.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return export;
                    foreach (var transitiveDependency in GetDependencies(export, allExports))
                    {
                        yield return transitiveDependency;
                    }
                }
            }
        }

        private IReadOnlyList<LibraryAsset> GetAssets(LibraryExport dependency, string runtime)
        {
            if (dependency.Library?.Path == null)
            {
                return null;
            }

            var assemblyGroup =
                dependency.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == runtime) ??
                dependency.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == string.Empty);

            var assets = assemblyGroup?.Assets;

            if (assets?.Any() != true)
            {
                return null;
            }

            return assets;
        }

        public void FetchCrossgenTools(string runtime)
        {
            var runtimePackageName = GetCrossGenPackageName(runtime);
            var entry = _exports.SingleOrDefault(
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

            var crossGenDirectory = Path.Combine(_destinationFolder, GetCrossGenFolderName(runtime));
            Directory.CreateDirectory(crossGenDirectory);

            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(executablePath)))
            {
                File.Copy(file, Path.Combine(crossGenDirectory, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var crossGenAsset in _crossGenAssets)
            {
                File.Copy(
                    crossGenAsset.ResolvedPath,
                    Path.Combine(crossGenDirectory, Path.GetFileName(crossGenAsset.ResolvedPath)),
                    overwrite: true);
            }

            _crossgenExecutable = Path.GetFullPath(Path.Combine(crossGenDirectory, Path.GetFileName(executablePath)));
        }

        private static string GetCrossGenPackageName(string runtime) => $"runtime.{runtime}.Microsoft.NETCore.Runtime.CoreCLR";

        private static string GetCrossGenFolderName(string runtime) => $"crossgen-{runtime}";

        public void FetchJitTools(string runtime, string restoreFolder)
        {
            var jitPackageName = $"runtime.{runtime}.Microsoft.NETCore.Jit";
            var jitPackagePath = Path.Combine(restoreFolder, jitPackageName);
            if (!Directory.Exists(jitPackagePath))
            {
                throw new InvalidOperationException($"Cannot find package {jitPackageName}. It contains the CLR JIT tool.");
            }

            var jitExecutable = CrossGenTool.GetCrossGenTool(runtime).ClrJit;
            string jitExecutablePath;
            try
            {
                jitExecutablePath = Directory
                    .GetFiles(jitPackagePath, jitExecutable, SearchOption.AllDirectories)
                    .SingleOrDefault();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(0, ex, "Found multiple versions of the cross gen tool in {0}", jitPackagePath);
                throw;
            }

            if (jitExecutablePath == null)
            {
                throw new InvalidOperationException($"Cannot find executable {jitExecutable}.");
            }

            _clrJitPath = jitExecutablePath;
        }

        public void Crossgen(string outputPath)
        {
            var lck = new object();
            Parallel.ForEach(_packagesToCrossGen, package =>
            {
                var packageDirectory = CreatePackageSubDirectory(outputPath, package);
                CopyPackageSignature(package, outputPath);

                foreach (var asset in package.Assets)
                {
                    var subDir = CreateAssetSubDirectory(outputPath, package, asset);
                    var targetPath = Path.Combine(subDir.FullName, asset.FileName);

                    var crossGen = CrossgenAssembly(package, asset, targetPath);
                    lock (lck)
                    {
                        _logger.LogInformation(crossGen.CrossGenArgumentsMessage);
                        if (!crossGen.Result)
                        {
                            _logger.LogWarning(crossGen.CrossGenResultMessage);
                        }
                        else
                        {
                            _logger.LogInformation(crossGen.CrossGenResultMessage);
                        }
                    }
                }

                if (package.CrossGenExitCode.Values.All(c => c != 0))
                {
                    packageDirectory.Parent.Delete(recursive: true);
                }
            });
        }

        private CrossGenResult CrossgenAssembly(PackageEntry package, LibraryAsset asset, string targetPath)
        {
            var assemblyPath = Path.GetFullPath(asset.ResolvedPath);
            var arguments = $"@\"{_responseFilePath}\" /JITPath \"{_clrJitPath}\" /in \"{assemblyPath}\" /out \"{targetPath}\"";

            var crossGen = new CrossGenResult();
            crossGen.CrossGenArgumentsMessage = $@"Crossgen assembly
    assembly: {assemblyPath}
      output: {targetPath}
   arguments: {arguments}";

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var exitCode = new ProcessRunner(_crossgenExecutable, arguments)
                .WriteOutputToStringBuilder(stdout, "    ")
                .WriteErrorsToStringBuilder(stderr, "    ")
                .Run();

            package.CrossGenExitCode.Add(asset, exitCode);
            package.CrossGenOutput.Add(asset, new List<string> { stdout.ToString(), stderr.ToString() });

            crossGen.CrossGenResultMessage = exitCode == 0 ?
                $"Native image {targetPath} generated successfully." :
                $"Crossgen failed for {targetPath}. Exit code {exitCode}.";

            crossGen.Result = exitCode == 0;

            return crossGen;
        }

        public void WriteCrossgenOutput()
        {
            var message = new StringBuilder();
            message.AppendLine("Crossgen output:");

            foreach (var package in _packagesToCrossGen)
            {
                message.AppendLine($"    Output for assets in {package.Library.Identity}");
                foreach (var entry in package.CrossGenOutput)
                {
                    message.AppendLine($"    Output for asset {entry.Key.ResolvedPath}");
                    foreach (var line in entry.Value)
                    {
                        message.AppendLine(line);
                    }
                }
            }

            File.WriteAllText(Path.Combine(_destinationFolder, "crossgen-output.txt"), message.ToString());
        }

        public bool AllPackagesSuccessfullyCrossgened()
        {
            var exclusions = new string[] { };

            var exclusionFilePath = _exclusionsFile ?? Path.Combine(Directory.GetCurrentDirectory(), "exclusions.txt");
            if (File.Exists(exclusionFilePath))
            {
                exclusions = File.ReadAllLines(exclusionFilePath);
            }

            foreach (var package in _packagesToCrossGen)
            {
                foreach (var assetResult in package.CrossGenExitCode)
                {
                    if (assetResult.Value != 0 &&
                        !exclusions.Contains(assetResult.Key.FileName, StringComparer.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void PrintFailedCrossgenedPackages()
        {
            _logger.LogWarning("Crossgen failed for the following packages:");

            var exclusions = new string[] { };
            var exclusionFilePath = _exclusionsFile ?? Path.Combine(Directory.GetCurrentDirectory(), "exclusions.txt");
            if (File.Exists(exclusionFilePath))
            {
                exclusions = File.ReadAllLines(exclusionFilePath);
            }

            foreach (var package in _packagesToCrossGen)
            {
                var firstFalingAsset = true;
                foreach (var assetResult in package.CrossGenExitCode)
                {
                    if (assetResult.Value != 0)
                    {
                        if (firstFalingAsset)
                        {
                            firstFalingAsset = false;
                            _logger.LogInformation(package.Library.Identity.ToString() + ":");
                        }

                        if (exclusions.Contains(assetResult.Key.FileName, StringComparer.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation($"    {assetResult.Key.ResolvedPath}");
                        }
                        else
                        {
                            _logger.LogWarning($"    {assetResult.Key.ResolvedPath}");
                        }
                    }
                }
            }
        }

        private void CopyPackageSignature(PackageEntry entry, string outputPath)
        {
            var hash = entry.Library.PackageLibrary.Files.Single(file => file.EndsWith(".sha512"));
            var desination = Path.Combine(outputPath,
                GetPackageId(entry.Library),
                GetPackageVersion(entry.Library),
                _preservePackageCasing
                    ? hash
                    : hash.ToLowerInvariant());

            File.Copy(Path.Combine(entry.Library.Path, hash), desination, overwrite: true);
        }

        private DirectoryInfo CreatePackageSubDirectory(string outputPath, PackageEntry package)
        {
            var subDirectoryPath = Path.Combine(
                outputPath,
                GetPackageId(package.Library),
                GetPackageVersion(package.Library));
            return Directory.CreateDirectory(subDirectoryPath);
        }

        private string GetPackageId(PackageDescription library)
            => _preservePackageCasing
                ? library.Identity.Name
                : library.Identity.Name.ToLowerInvariant();

        private string GetPackageVersion(PackageDescription library)
            => _preservePackageCasing
                ? library.Identity.Version.ToNormalizedString()
                : library.Identity.Version.ToNormalizedString().ToLowerInvariant();

        private DirectoryInfo CreateAssetSubDirectory(string cacheBasePath, PackageEntry package, LibraryAsset asset)
        {
            // special treatment of the cases in the path to accommodating NuGet package search
            // https://github.com/aspnet/BuildTools/issues/94
            // https://github.com/NuGet/Home/issues/2522
            var subDirectoryPath = Path.Combine(
                cacheBasePath,
                GetPackageId(package.Library),
                GetPackageVersion(package.Library),
                Path.GetDirectoryName(asset.RelativePath));

            return Directory.CreateDirectory(subDirectoryPath);
        }

        public void CreateResponseFile()
        {
            _responseFilePath = Path.Combine(_destinationFolder, "aspnet-crossgen.rsp");
            var lines = new List<string>();
            lines.Add("/Platform_Assemblies_Paths");
            lines.Add("\"" + string.Join(Path.PathSeparator.ToString(), _referenceAssemblyPaths) + "\"");
            lines.Add("/App_paths");
            lines.Add(
                "\"" +
                string.Join(Path.PathSeparator.ToString(), _packagesToCrossGen.SelectMany(e => e.Assets.Select(a => GetCleanedUpDirectoryPath(a)))) +
                "\"");
            lines.Add("/ReadyToRun");
            File.WriteAllLines(_responseFilePath, lines);
        }

        private static string GetCleanedUpDirectoryPath(LibraryAsset asset) =>
            Path.GetDirectoryName(asset.ResolvedPath)
                .TrimEnd(Path.DirectorySeparatorChar)
                .TrimEnd(Path.AltDirectorySeparatorChar);

        private struct CrossGenResult
        {
            public string CrossGenArgumentsMessage { get; set; }
            public string CrossGenResultMessage { get; set; }
            public bool Result { get; set; }
        }
    }
}
