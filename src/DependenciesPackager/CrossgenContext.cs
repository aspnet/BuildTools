// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NugetReferenceResolver;

namespace DependenciesPackager
{
    internal class CrossgenContext
    {
        private readonly PackageGraph _graph;
        private readonly ILogger _logger;
        private bool _preservePackageCasing;

        private IEnumerable<PackageEntry> _packagesToCrossGen = Enumerable.Empty<PackageEntry>();
        private IEnumerable<PackageEntry> _referenceAssemblyPaths = Enumerable.Empty<PackageEntry>();

        private IEnumerable<PackageAssembly> _crossGenAssets;
        private string _clrJitPath;
        private string _crossgenExecutable;

        private readonly string _destinationFolder;
        private string _responseFilePath;
        private readonly string _exclusionsFile;

        public CrossgenContext(PackageGraph graph, string destinationFolder, ILogger logger, string exclusionsFile)
        {
            _graph = graph;
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
            var packagesToCrossgenGraph = _graph.WithoutPackage("Microsoft.NETCore.App");
            _packagesToCrossGen =
                packagesToCrossgenGraph.AllPackages.Values.Select(p => new PackageEntry
                {
                    Assets = p.Assemblies.ToArray(),
                    Library = p
                });

            var netCoreAppGraph = _graph.GetClosure("Microsoft.NETCore.App");
            _referenceAssemblyPaths =
                netCoreAppGraph.AllPackages.Values.Select(p => new PackageEntry
                {
                    Assets = p.Assemblies.ToArray(),
                    Library = p
                });

            var crossGenPackage = _graph.AllPackages
                .Single(le => le.Key.Equals(
                    GetCrossGenPackageName(runtime),
                    StringComparison.OrdinalIgnoreCase));

            _crossGenAssets = crossGenPackage.Value.Assemblies;
        }

        public void FetchCrossgenTools(string runtime)
        {
            var runtimePackageName = GetCrossGenPackageName(runtime);

            if (!_graph.AllPackages.TryGetValue(runtimePackageName, out var entry))
            {
                throw new InvalidOperationException($"Cannot find package {runtimePackageName}. It contains the crossgen tool.");
            }

            var executable = CrossGenTool.GetCrossGenTool(runtime).CrossGen;
            var executablePath = Directory.GetFiles(entry.Path, executable, SearchOption.AllDirectories).SingleOrDefault();

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

        private CrossGenResult CrossgenAssembly(PackageEntry package, PackageAssembly asset, string targetPath)
        {
            var assemblyPath = Path.GetFullPath(asset.ResolvedPath);
            var arguments = $"@\"{_responseFilePath}\" /JITPath \"{_clrJitPath}\" /in \"{assemblyPath}\" /out \"{targetPath}\"";

            var crossGen = new CrossGenResult
            {
                CrossGenArgumentsMessage = $@"Crossgen assembly
    assembly: {assemblyPath}
      output: {targetPath}
   arguments: {arguments}"
            };
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
                            _logger.LogInformation(package.Library.Identity + ":");
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
            var hash = entry.Library.PackageHash;
            var destination = Path.Combine(outputPath,
                GetPackageId(entry.Library),
                GetPackageVersion(entry.Library),
                _preservePackageCasing
                    ? hash
                    : hash.ToLowerInvariant());

            File.Copy(Path.Combine(entry.Library.Path, hash), destination, overwrite: true);
        }

        private DirectoryInfo CreatePackageSubDirectory(string outputPath, PackageEntry package)
        {
            var subDirectoryPath = Path.Combine(
                outputPath,
                GetPackageId(package.Library),
                GetPackageVersion(package.Library));
            return Directory.CreateDirectory(subDirectoryPath);
        }

        private string GetPackageId(Package library)
            => _preservePackageCasing
                ? library.Name
                : library.Name.ToLowerInvariant();

        private string GetPackageVersion(Package library)
            => _preservePackageCasing
                ? library.Version
                : library.Version.ToLowerInvariant();

        private DirectoryInfo CreateAssetSubDirectory(string cacheBasePath, PackageEntry package, PackageAssembly asset)
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
            var lines = new List<string>
            {
                "/Platform_Assemblies_Paths",
                "\"" + string.Join(
                Path.PathSeparator.ToString(),
                _referenceAssemblyPaths.SelectMany(e => e.Assets.Select(GetCleanedUpDirectoryPath))) + "\"",
                "/App_paths",
                "\"" +
                string.Join(Path.PathSeparator.ToString(), _packagesToCrossGen.SelectMany(e => e.Assets.Select(GetCleanedUpDirectoryPath))) +
                "\"",
                "/ReadyToRun"
            };
            File.WriteAllLines(_responseFilePath, lines);
        }

        private static string GetCleanedUpDirectoryPath(PackageAssembly asset) =>
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
