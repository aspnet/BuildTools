// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace DependenciesPackager
{
    class Program
    {
        private const int CrossGenFlag = 4;
        private static readonly string TempRestoreFolderName = "TempRestorePackages";
        private static readonly string TempDiagnosticsRestoreFolderName = "TempDiagnosticsRestorePackages";
        private static readonly string TempPublishFolderName = "TempPublish";

        private readonly IEnumerable<NuGetFramework> FrameworkConfigurations = new[] { FrameworkConstants.CommonFrameworks.NetCoreApp10 };

        private readonly IDictionary<string, CrossGenToolFileNames> CrossGenFiles = new Dictionary<string, CrossGenToolFileNames>
        {
            ["win7"] = new CrossGenToolFileNames("crossgen.exe","clrjit.dll"),
            ["ubuntu.16.04"] = new CrossGenToolFileNames("crossgen", "libclrjit.so"),
            ["ubuntu.14.04"] = new CrossGenToolFileNames("crossgen", "libclrjit.so"),
            ["debian.8"] = new CrossGenToolFileNames("crossgen", "libclrjit.so"),
        };

        private readonly ISet<string> ValidRuntimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "win7-x86",
            "win7-x64",
            "ubuntu.14.04-x64",
            "ubuntu.16.04-x64",
            "debian.8-x64"
        };

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
        private CommandOption _runtimes;
        private CommandOption _prefix;
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
            CommandOption runtimes,
            CommandOption prefix,
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
            _runtimes = runtimes;
            _prefix = prefix;
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

            var runtimes = app.Option(
                "--runtime",
                "The runtimes for which to generate the cache",
                CommandOptionType.MultipleValue);

            var prefix = app.Option(
                "--prefix",
                "The prefix to use for the zip file name",
                CommandOptionType.SingleValue);

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
                runtimes,
                prefix,
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
                    !_runtimes.HasValue() ||
                    !_prefix.HasValue() ||
                    !_version.HasValue())
                {
                    _app.ShowHelp();
                    return Error;
                }

                var invalidRuntimes = false;
                foreach (var runtime in _runtimes.Values)
                {
                    if (!ValidRuntimes.Contains(runtime))
                    {
                        invalidRuntimes = true;
                        Logger.LogError($"Invalid runtime {runtime}");
                    }
                }
                if (invalidRuntimes)
                {
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
                    foreach (var runtime in _runtimes.Values)
                    {
                        var cacheBasePath = Path.Combine(_destination.Value(), _version.Value(), GetArchitecture(runtime));

                        var projectContext = CreateProjectContext(project, framework, runtime);
                        var entries = GetEntries(projectContext, runtime);

                        Logger.LogInformation($"Performing crossgen on {framework.GetShortFolderName()} for runtime {runtime}.");

                        var publishFolderPath = GetPublishFolderPath(projectContext);
                        CreatePublishFolder(entries, publishFolderPath);
                        var crossGenPath = CopyCrossgenToPublishFolder(entries, runtime, publishFolderPath);
                        CopyClrJitToPublishFolder(Path.Combine(_destination.Value(), TempRestoreFolderName), runtime, publishFolderPath);
                        RunCrossGenOnEntries(entries, publishFolderPath, crossGenPath, cacheBasePath);
                        DisplayCrossGenOutput(entries);

                        CopyPackageSignatures(entries, cacheBasePath);

                        CompareWithRestoreHive(Path.Combine(_destination.Value(), TempRestoreFolderName), cacheBasePath, project);
                        RemoveUnnecesaryHiveFiles(cacheBasePath, project);
                        PrintHiveFilesForDiagnostics(cacheBasePath);
                        ValidateHiveFiles(cacheBasePath);
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
                Logger.LogError(e.ToString());
                _app.ShowHelp();
                return Error;
            }
        }

        private void ValidateHiveFiles(string cacheBasePath)
        {
            var dlls = Directory.EnumerateFiles(cacheBasePath, "*.dll", SearchOption.AllDirectories);
            foreach (var dll in dlls)
            {
                using (var stream = File.OpenRead(dll))
                {
                    var reader = new PEReader(stream);
                    var isCrossGen = ((int)reader.PEHeaders.CorHeader.Flags & CrossGenFlag) == CrossGenFlag;
                    if (!isCrossGen)
                    {
                        Logger.LogWarning($"{dll} is not crossgen");
                    }
                }
            }
        }

        private string GetArchitecture(string runtime)
        {
            return runtime.Substring(runtime.LastIndexOf('-') + 1);
        }

        private void RemoveUnnecesaryHiveFiles(string cacheBasePath, Project project)
        {
            var directories = new DirectoryInfo(cacheBasePath);
            foreach (var directory in directories.EnumerateDirectories())
            {
                if (!project.Dependencies.Any(d => d.Name.Equals(directory.Name)))
                {
                    directory.Delete(recursive: true);
                }
            }
        }

        private void RemoveUnnecessaryRestoredFiles()
        {
            var restorePath = Path.Combine(_destination.Value(), TempRestoreFolderName);

            var files = Directory.GetFiles(restorePath, "*", SearchOption.AllDirectories);
            var dlls = Directory.GetFiles(restorePath, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.GetFiles(restorePath, "*.sha512", SearchOption.AllDirectories);
            var crossgen = CrossGenFiles.SelectMany(kvp => Directory.GetFiles(restorePath, kvp.Value.CrossGen, SearchOption.AllDirectories));
            var clrjit = CrossGenFiles.SelectMany(kvp => Directory.GetFiles(restorePath, kvp.Value.ClrJit, SearchOption.AllDirectories));

            Logger.LogInformation($@"Dlls in restored packages:
{string.Join($"    {Environment.NewLine}", dlls)}");

            Logger.LogInformation($@"Signature files in restored packages:
{string.Join($"    {Environment.NewLine}", signatures)}");

            var filesToRemove = files.Except(dlls.Concat(signatures).Concat(crossgen).Concat(clrjit));
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

        private void CompareWithRestoreHive(string restoredCache, string hivePath, Project project)
        {
            var dlls = Directory.EnumerateFiles(restoredCache, "*.dll", SearchOption.AllDirectories);
            var signatures = Directory.EnumerateFiles(restoredCache, "*.sha512", SearchOption.AllDirectories);
            var allFiles = dlls
                .Concat(signatures)
                .Where(p => project.Dependencies.Any(d => p.Contains(d.Name) && !p.Contains("net451") && !p.Contains("Microsoft.NETCore.App")))
                .Select(p => p.Remove(0, restoredCache.Length + 1));

            var filesInRestoredCache = new HashSet<string>(allFiles);

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
                    Assets = (d.RuntimeAssemblyGroups.SingleOrDefault(rg => rg.Runtime == runtime) ??
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
                .WithLockFile(LockFileReader.Read(project.ProjectFilePath.Replace("project.json", "project.lock.json"), designTime: false))
                .WithTargetFramework(framework)
                .WithRuntimeIdentifiers(new[] { runtime });

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

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            var exitCode = new ProcessRunner(crossGenPath, string.Join(" ", arguments))
                .WithWorkingDirectory(publishedAssemblies)
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

            var crossGenExecutable = CrossGenFiles[GetPlatform(moniker)].CrossGen;
            var path = Directory
                .GetFiles(entry.Library.Path, crossGenExecutable, SearchOption.AllDirectories)
                .SingleOrDefault();

            if (path == null)
            {
                throw new InvalidOperationException($"Couldn't find path to {crossGenExecutable}");
            }

            var crossGenDirectory = Path.GetDirectoryName(path);
            foreach (var file in Directory.GetFiles(crossGenDirectory))
            {
                File.Copy(file, Path.Combine(publishDirectory, Path.GetFileName(file)), overwrite: true);
            }

            return Path.Combine(publishDirectory, Path.GetFileName(path));
        }

        private void CopyClrJitToPublishFolder(
            string tempRestorePath,
            string moniker,
            string publishDirectory)
        {
            var clrJitPath = Path.Combine(tempRestorePath, $"runtime.{moniker}.Microsoft.NETCore.Jit");
            if (!Directory.Exists(clrJitPath))
            {
                throw new InvalidOperationException("Couldn't find the dependency 'Microsoft.NETCore.Jit'");
            }

            var clrJit = CrossGenFiles[GetPlatform(moniker)].ClrJit;
            var path = Directory
                .GetFiles(clrJitPath, clrJit, SearchOption.AllDirectories)
                .SingleOrDefault();

            if (path == null)
            {
                throw new InvalidOperationException($"Couldn't find path to {clrJit}");
            }

            var crossGenDirectory = Path.GetDirectoryName(path);
            foreach (var file in Directory.GetFiles(crossGenDirectory))
            {
                File.Copy(file, Path.Combine(publishDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }

        //Remove the architecture part (-x86/-x64) from the runtime moniker (return win7, ubuntu.16.04, etc).
        private string GetPlatform(string moniker)
        {
            return moniker.Substring(0, moniker.Length - 4);
        }

        private void CreateZipPackage()
        {
            var version = _version.Value();
            var destinationFolderPath = _destination.Value();
            var packagesFolder = Path.Combine(destinationFolderPath, version);

            // Marker file used by Antares to keep track of the installed caches
            var versionMarkerPath = Path.Combine(packagesFolder, $"{version}.version");
            File.WriteAllText(versionMarkerPath, string.Empty);

            var zipFileName = Path.Combine(destinationFolderPath, $"{_prefix.Value()}-{version}-{_runtimes.Value()}.zip");
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
            private object _writeLock = new object();
            private string _workingDirectory;

            public ProcessRunner(
                string exePath,
                string arguments)
            {
                _exePath = exePath;
                _arguments = arguments;
                _workingDirectory = Path.GetDirectoryName(_exePath);
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
                OnError = s =>
                {
                    lock (_writeLock)
                    {
                        builder.AppendLine(indentation + s);
                    }
                };
                return this;
            }

            public ProcessRunner WriteOutputToStringBuilder(StringBuilder builder, string indentation)
            {
                OnOutput = s =>
                {
                    lock (_writeLock)
                    {
                        builder.AppendLine(indentation + s);
                    }
                };
                return this;
            }

            public ProcessRunner WithWorkingDirectory(string workingDirectory)
            {
                _workingDirectory = workingDirectory;
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
                    _process.Dispose();
                    throw new InvalidOperationException($"Process {_process.ProcessName} timed out");
                }

                return _process.ExitCode;
            }

            private ProcessStartInfo CreateProcessInfo()
            {
                var processInfo = new ProcessStartInfo(_exePath, _arguments);
                foreach (var variable in _environment)
                {
                    processInfo.Environment.Add(variable.Key, variable.Value);
                }

                processInfo.WorkingDirectory = _workingDirectory;
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

        private class CrossGenToolFileNames
        {
            public CrossGenToolFileNames(string crossgen, string clrjit)
            {
                CrossGen = crossgen;
                ClrJit = clrjit;
            }

            public string ClrJit { get; }
            public string CrossGen { get; }
        }
    }
}
