// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace DependenciesPackager
{
    class Program
    {
        private static readonly string TempRestoreFolderName = "TempRestorePackages";
        private static readonly string TempDiagnosticsRestoreFolderName = "TempDiagnosticsRestorePackages";
        private static readonly string TempPublishFolderName = "TempPublish";

        private readonly IEnumerable<string> Runtimes = new[] { "x86", "x64" };
        private readonly IEnumerable<NuGetFramework> FrameworkConfigurations = new[] { FrameworkConstants.CommonFrameworks.NetCoreApp10 };

        private const int Ok = 0;
        private const int Error = 1;

        private ILogger _logger;

        private CommandLineApplication _app;
        private CommandOption _projectJson;
        private CommandOption _diagnosticsProjectJson;
        private CommandOption _sourceFolders;
        private CommandOption _fallbackFeeds;
        private CommandOption _destination;
        private CommandOption _version;
        private CommandOption _cliPath;
        private CommandOption _quiet;
        private CommandOption _keepTemporaryFiles;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = CreateLogger();
                }

                return _logger;
            }
        }

        private ILogger CreateLogger()
        {
            var loggerFactory = new LoggerFactory();
            var logLevel = _quiet.HasValue() ? LogLevel.Warning : LogLevel.Information;

            loggerFactory.AddConsole(logLevel, includeScopes: false);

            return loggerFactory.CreateLogger<Program>();
        }

        public Program(
            CommandLineApplication app,
            CommandOption projectJson,
            CommandOption diagnosticsProjectJson,
            CommandOption sourceFolders,
            CommandOption fallbackFeeds,
            CommandOption destination,
            CommandOption packagesVersion,
            CommandOption cliPath,
            CommandOption quiet,
            CommandOption keepTemporaryFiles)
        {
            _app = app;
            _projectJson = projectJson;
            _diagnosticsProjectJson = diagnosticsProjectJson;
            _sourceFolders = sourceFolders;
            _fallbackFeeds = fallbackFeeds;
            _destination = destination;
            _version = packagesVersion;
            _cliPath = cliPath;
            _quiet = quiet;
            _keepTemporaryFiles = keepTemporaryFiles;
        }

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "DependenciesPackager";

            app.HelpOption("-?|-h|--help");

            var projectJson = app.Option(
                "--project <PATH>",
                "The path to the project.json file from which to perform dotnet restore",
                CommandOptionType.SingleValue);

            var diagnosticsProjectJson = app.Option(
                "--diagnostics-project <PATH>",
                "The path to the project.json file from which to perform dotnet restore for diagnostic purposes",
                CommandOptionType.SingleValue);

            var sourceFolders = app.Option(
                "--sources <DIRS>",
                "Path to the directories containing the nuget packages",
                CommandOptionType.MultipleValue);

            var fallbackFeeds = app.Option(
                "--fallback <URLS>",
                "The list of URLs to use as fallback feeds in case the package can't be found on the source folders",
                CommandOptionType.MultipleValue);

            var destination = app.Option(
                "--destination <DIR>",
                "The folder on which the artifacts will be produced",
                CommandOptionType.SingleValue);

            var packagesVersion = app.Option(
                "--version <VERSION>",
                "The version of the artifacts produced",
                CommandOptionType.SingleValue);

            var useCli = app.Option(
                "--use-cli <PATH>",
                "The path to the dotnet CLI tools to use",
                CommandOptionType.SingleValue);

            var quiet = app.Option(
                "--quiet",
                "Avoids printing to the output anything other than warnings or errors",
                CommandOptionType.NoValue);

            var keepTemporaryFiles = app.Option(
                "--keep-temporary-files",
                "Avoids deleting the package folders and the publish folder used for crossgen",
                CommandOptionType.NoValue);

            var program = new Program(
                app,
                projectJson,
                diagnosticsProjectJson,
                sourceFolders,
                fallbackFeeds,
                destination,
                packagesVersion,
                useCli,
                quiet,
                keepTemporaryFiles);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_projectJson.HasValue() ||
                    !_sourceFolders.HasValue() ||
                    !_fallbackFeeds.HasValue() ||
                    !_destination.HasValue() ||
                    !_version.HasValue())
                {
                    _app.ShowHelp();
                    return Error;
                }

                if (_diagnosticsProjectJson.HasValue())
                {
                    RunDotnetRestore(_diagnosticsProjectJson.Value(), TempDiagnosticsRestoreFolderName);
                }

                if (RunDotnetRestore(_projectJson.Value(), TempRestoreFolderName) != 0)
                {
                    Logger.LogWarning("Error restoring nuget packages into destination folder");
                }

                RemoveUnnecessaryRestoredFiles();

                var project = ProjectReader.GetProject(_projectJson.Value());

                foreach (var framework in FrameworkConfigurations)
                {
                    foreach (var runtime in Runtimes)
                    {
                        var cacheBasePath = Path.Combine(_destination.Value(), _version.Value(), runtime);

                        var projectContext = CreateProjectContext(project, framework, runtime);
                        var entries = GetEntries(projectContext, runtime);

                        Logger.LogInformation($"Performing crossgen on {framework.GetShortFolderName()} for runtime {runtime}.");

                        var publishFolderPath = GetPublishFolderPath(projectContext);
                        CreatePublishFolder(entries, publishFolderPath);
                        var crossGenPath = CopyCrossgenToPublishFolder(entries, $"win7-{runtime}", publishFolderPath);
                        RunCrossGenOnEntries(entries, publishFolderPath, crossGenPath, cacheBasePath);
                        DisplayCrossGenOutput(entries);

                        CopyPackageSignatures(entries, cacheBasePath);

                        CompareWithRestoreHive(Path.Combine(_destination.Value(), TempRestoreFolderName), cacheBasePath);
                        PrintHiveFilesForDiagnostics(cacheBasePath);
                    }
                }

                CreateZipPackage();
                if (!_keepTemporaryFiles.HasValue())
                {
                    CleanupIntermediateFiles();
                }

                return Ok;
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                _app.ShowHelp();
                return Error;
            }
        }

        private void RemoveUnnecessaryRestoredFiles()
        {
            var restorePath = Path.Combine(_destination.Value(), TempRestoreFolderName);

            var files = Directory.GetFiles(restorePath, "*", SearchOption.AllDirectories);
            var dlls = Directory.GetFiles(restorePath, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.GetFiles(restorePath, "*.sha512", SearchOption.AllDirectories);
            var crossgen = Directory.GetFiles(restorePath, "crossgen.exe", SearchOption.AllDirectories);

            Logger.LogInformation($@"Dlls in restored packages:
{string.Join($"    {Environment.NewLine}", dlls)}");

            Logger.LogInformation($@"Signature files in restored packages:
{string.Join($"    {Environment.NewLine}", signatures)}");

            var filesToRemove = files.Except(dlls.Concat(signatures).Concat(crossgen));
            foreach (var file in filesToRemove)
            {
                Logger.LogInformation($"Removing file '{file}.");
                File.Delete(file);
            }
        }

        private void CleanupIntermediateFiles()
        {
            Retry(() =>
            {
                var restorePath = Path.Combine(_destination.Value(), TempRestoreFolderName);
                if (Directory.Exists(restorePath))
                {
                    Directory.Delete(restorePath, recursive: true);
                }
            }, 3);

            Retry(() =>
            {
                var diagnosticsRestorePath = Path.Combine(_destination.Value(), TempDiagnosticsRestoreFolderName);
                if (Directory.Exists(diagnosticsRestorePath))
                {
                    Directory.Delete(diagnosticsRestorePath, recursive: true);
                }
            }, 3);

            Retry(() =>
            {
                var publishPath = Path.Combine(_destination.Value(), TempPublishFolderName);
                if (Directory.Exists(publishPath))
                {
                    Directory.Delete(publishPath, recursive: true);
                }
            }, 3);

            Retry(() =>
            {
                var expandedCachePath = Path.Combine(_destination.Value(), _version.Value());
                if (Directory.Exists(expandedCachePath))
                {
                    Directory.Delete(expandedCachePath, recursive: true);
                }
            }, 3);
        }

        private void Retry(Action action, int times)
        {
            for (var i = 0; i < times; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void PrintHiveFilesForDiagnostics(string cacheBasePath)
        {
            Logger.LogInformation($@"Files in hive:
{string.Join($"{Environment.NewLine}    ", Directory.EnumerateFiles(cacheBasePath, "*", SearchOption.AllDirectories))}");
        }

        private void CompareWithRestoreHive(string restoredCache, string hivePath)
        {
            var dlls = Directory.EnumerateFiles(restoredCache, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.EnumerateFiles(restoredCache, "*.sha512", SearchOption.AllDirectories);
            var filesInRestoredCache = new HashSet<string>(
                dlls.Concat(signatures).Select(p => p.Remove(0, restoredCache.Length + 1)));

            var hiveFilePaths = Directory.EnumerateFiles(hivePath, "*", SearchOption.AllDirectories);
            var filesInHive = new HashSet<string>(hiveFilePaths.Select(p => p.Remove(0, hivePath.Length + 1)));

            var filesNotInHive = filesInRestoredCache.Except(filesInHive, StringComparer.OrdinalIgnoreCase);
            foreach (var file in filesNotInHive)
            {
                Logger.LogWarning($@"The file
    {Path.Combine(restoredCache, file)} can not be found in
    {Path.Combine(hivePath, file)}");
            }
        }

        private static void CopyPackageSignatures(PackageEntry[] entries, string cacheBasePath)
        {
            foreach (var entry in entries.Where(e => e.Library is PackageDescription))
            {
                var packageDescription = (PackageDescription)entry.Library;
                var hash = packageDescription.PackageLibrary.Files.Single(f => f.EndsWith(".sha512"));
                File.Copy(
                    Path.Combine(entry.Library.Path, hash),
                    Path.Combine(
                        cacheBasePath,
                        entry.Library.Identity.Name,
                        entry.Library.Identity.Version.ToNormalizedString(),
                        hash),
                    overwrite: true);
            }
        }

        private void DisplayCrossGenOutput(PackageEntry[] entries)
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine("Output of running crossgen of assemblies:");
            foreach (var package in entries)
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

            Logger.LogInformation(errorMessage.ToString());
        }

        private void RunCrossGenOnEntries(PackageEntry[] entries, string publishFolderPath, string crossGenPath, string cacheBasePath)
        {
            foreach (var package in entries)
            {
                foreach (var asset in package.Assets)
                {
                    var subDirectory = CreateSubDirectory(cacheBasePath, package, asset);
                    var targetPath = Path.Combine(subDirectory, asset.FileName);
                    var succeeded = RunCrossGenOnAssembly(crossGenPath, publishFolderPath, package, asset, targetPath);

                    if (!succeeded)
                    {
                        Logger.LogWarning("Copying non crossgen asset instead.");
                        File.Copy(asset.ResolvedPath, targetPath, overwrite: true);
                    }
                }
            }
        }

        private string CreateSubDirectory(string cacheBasePath, PackageEntry package, LibraryAsset asset)
        {
            var subDirectoryPath = Path.Combine(
                cacheBasePath,
                package.Library.Identity.Name,
                package.Library.Identity.Version.ToNormalizedString(),
                Path.GetDirectoryName(asset.RelativePath));

            Logger.LogInformation($"Creating sub directory on {subDirectoryPath}.");

            var subDirectory = Directory.CreateDirectory(subDirectoryPath);
            return subDirectory.FullName;
        }

        private string GetPublishFolderPath(ProjectContext projectContext)
        {
            return Path.Combine(
                _destination.Value(),
                TempPublishFolderName,
                projectContext.TargetFramework.GetShortFolderName(),
                projectContext.RuntimeIdentifier);
        }

        private void CreatePublishFolder(PackageEntry[] entries, string publishFolder)
        {
            Logger.LogInformation($"Creating directory {publishFolder}.");
            Directory.CreateDirectory(publishFolder);

            foreach (var package in entries)
            {
                foreach (var asset in package.Assets)
                {
                    var path = asset.ResolvedPath;
                    var targetPath = Path.Combine(publishFolder, asset.FileName);

                    Logger.LogInformation($@"Copying file
    {path} into
    {targetPath}");
                    File.Copy(path, targetPath, overwrite: true);
                }
            }
        }

        private PackageEntry[] GetEntries(ProjectContext context, string runtime)
        {
            var restoreCache = Path.Combine(_destination.Value(), TempRestoreFolderName);
            return context
                .CreateExporter("CACHE")
                .GetDependencies()
                .Select(d => new PackageEntry
                {
                    Library = d.Library,
                    Assets = (d.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == $"win7-{runtime}") ??
                              d.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == ""))?.Assets
                })
                .Where(e =>
                    e.Library?.Path?.StartsWith(restoreCache, StringComparison.OrdinalIgnoreCase) != null &&
                    e.Assets?.Any() == true)
                .ToArray();
        }

        private ProjectContext CreateProjectContext(Project project, NuGetFramework framework, string runtime)
        {
            var builder = new ProjectContextBuilder()
                .WithPackagesDirectory(Path.Combine(_destination.Value(), TempRestoreFolderName))
                .WithProject(project)
                .WithTargetFramework(framework)
                .WithRuntimeIdentifiers(new[] { $"win7-{runtime}" });

            return builder.Build();
        }

        private bool RunCrossGenOnAssembly(
            string crossGenPath,
            string publishedAssemblies,
            PackageEntry package,
            LibraryAsset asset,
            string targetPath)
        {
            var assemblyPath = asset.ResolvedPath;

            var arguments = new[]
            {
                $"/Platform_Assemblies_Paths {publishedAssemblies}",
                $"/in {assemblyPath}",
                $"/out {targetPath}"
            };

            Logger.LogInformation($@"Running crossgen on
    {assemblyPath} and putting results on
    {targetPath} with arguments
        {string.Join($"{Environment.NewLine}        ", arguments)}.");

            Environment.CurrentDirectory = publishedAssemblies;
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            var exitCode = new ProcessRunner(crossGenPath, string.Join(" ", arguments))
                .WriteOutputToStringBuilder(output, "    ")
                .WriteErrorsToStringBuilder(error, "    ")
                .Run();

            package.CrossGenOutput.Add(asset, new List<string> { output.ToString(), error.ToString() });

            if (exitCode == 0)
            {
                Logger.LogInformation($"Native image {targetPath} generated successfully.");
                return true;
            }
            else
            {
                Logger.LogWarning($"Crossgen failed for {targetPath}.");
                return false;
            }
        }

        private string CopyCrossgenToPublishFolder(
            IEnumerable<PackageEntry> packages,
            string moniker,
            string publishDirectory)
        {
            var entry = packages.SingleOrDefault(p =>
                p.Library.Identity.Name.Equals(
                    $"runtime.{moniker}.Microsoft.NETCore.Runtime.CoreCLR",
                    StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                throw new InvalidOperationException("Couldn't find the dependency 'Microsoft.NETCore.Runtime.CoreCLR'");
            }

            var path = Directory
                .GetFiles(entry.Library.Path, "crossgen.exe", SearchOption.AllDirectories)
                .SingleOrDefault();

            if (path == null)
            {
                throw new InvalidOperationException("Couldn't find path to crossgen.exe");
            }

            var crossGenDirectory = Path.GetDirectoryName(path);
            foreach (var file in Directory.GetFiles(crossGenDirectory))
            {
                File.Copy(file, Path.Combine(publishDirectory, Path.GetFileName(file)), overwrite: true);
            }

            return Path.Combine(publishDirectory, Path.GetFileName(path));
        }

        private void CreateZipPackage()
        {
            var version = _version.Value();
            var destinationFolderPath = _destination.Value();
            var packagesFolder = Path.Combine(destinationFolderPath, version);

            // Marker file used by Antares to keep track of the installed caches
            var versionMarkerPath = Path.Combine(packagesFolder, $"{version}.version");
            File.WriteAllText(versionMarkerPath, string.Empty);

            var zipFileName = Path.Combine(destinationFolderPath, $"AspNetCore.{version}.packagecache.zip");
            Logger.LogInformation($"Creating zip package on {zipFileName}");
            ZipFile.CreateFromDirectory(packagesFolder, zipFileName, CompressionLevel.Optimal, includeBaseDirectory: false);
        }

        private int RunDotnetRestore(string projectJson, string packagesFolderName)
        {
            Logger.LogInformation($"Running dotnet restore on {Path.Combine(_destination.Value(), packagesFolderName)}");
            var sources = string.Join(" ", _sourceFolders.Values.Select(v => $"--source {v}"));
            var fallbackFeeds = string.Join(" ", _fallbackFeeds.Values.Select(v => $"--fallbacksource {v} "));
            var packages = $"--packages {Path.Combine(_destination.Value(), packagesFolderName)}";

            var dotnet = _cliPath.HasValue() ? _cliPath.Value() : "dotnet";

            var arguments = string.Join(
                " ",
                "restore",
                packages,
                projectJson,
                sources,
                fallbackFeeds,
                "--verbosity Verbose");

            return new ProcessRunner(dotnet, arguments)
                .WriteOutputToConsole()
                .WriteOutputToConsole()
                .Run();
        }

        private class ProcessRunner
        {
            private const int DefaultTimeOutMinutes = 20;

            private readonly string _arguments;
            private readonly string _exePath;
            private readonly IDictionary<string, string> _environment = new Dictionary<string, string>();
            private Process _process = null;

            public ProcessRunner(
                string exePath,
                string arguments)
            {
                _exePath = exePath;
                _arguments = arguments;
            }

            public Action<string> OnError { get; set; } = s => { };

            public Action<string> OnOutput { get; set; } = s => { };

            public int ExitCode => _process.ExitCode;

            public int TimeOut { get; set; } = DefaultTimeOutMinutes * 60 * 1000;

            public ProcessRunner WriteErrorsToConsole()
            {
                OnError = s => Console.WriteLine(s);
                return this;
            }

            public ProcessRunner WriteOutputToConsole()
            {
                OnOutput = s => Console.WriteLine(s);
                return this;
            }

            public ProcessRunner WriteErrorsToStringBuilder(StringBuilder builder, string indentation)
            {
                OnError = s => builder.AppendLine(indentation + s);
                return this;
            }

            public ProcessRunner WriteOutputToStringBuilder(StringBuilder builder, string indentation)
            {
                OnOutput = s => builder.AppendLine(indentation + s);
                return this;
            }

            public ProcessRunner AddEnvironmentVariable(string name, string value)
            {
                _environment.Add(name, value);
                return this;
            }

            public ProcessRunner WithTimeOut(int minutes)
            {
                TimeOut = minutes * 60 * 1000;
                return this;
            }

            public int Run()
            {
                if (_process != null)
                {
                    throw new InvalidOperationException("The process has already been started.");
                }

                ProcessStartInfo processInfo = CreateProcessInfo();
                _process = new Process();
                _process.StartInfo = processInfo;
                _process.EnableRaisingEvents = true;
                _process.ErrorDataReceived += (s, e) => OnError(e.Data);
                _process.OutputDataReceived += (s, e) => OnOutput(e.Data);
                _process.Start();

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _process.WaitForExit(TimeOut);
                if (!_process.HasExited)
                {
                    _process.Close();
                    throw new InvalidOperationException($"Process {_process.ProcessName} timed out");
                }

                return _process.ExitCode;
            }

            private ProcessStartInfo CreateProcessInfo()
            {
                var processInfo = new ProcessStartInfo(_exePath, _arguments);
                foreach (var variable in _environment)
                {
                    processInfo.EnvironmentVariables.Add(variable.Key, variable.Value);
                }

                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardError = true;
                processInfo.RedirectStandardOutput = true;

                return processInfo;
            }
        }

        private class PackageEntry
        {
            public IReadOnlyList<LibraryAsset> Assets { get; set; }
            public LibraryDescription Library { get; set; }

            public IDictionary<LibraryAsset, IList<string>> CrossGenOutput { get; } =
                new Dictionary<LibraryAsset, IList<string>>();
        }
    }
}