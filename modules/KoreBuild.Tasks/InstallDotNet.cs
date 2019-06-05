// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Shim for executing dotnet-install.{sh,ps1}, allowing repos to specify the required .NET Core runtimes or to programmatically add new ones.
    /// </summary>
    public class InstallDotNet : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private const string DefaultArch = "x64";
        public readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The .NET Core runtimes or SDKs to be installed.
        ///
        /// Supported metadata:
        ///   Channel: default is ''
        ///   Arch: default is 'x64'
        ///   Runtime: default is ''
        ///   InstallDir: default is ''. When not specified <see cref="DotNetHome"/> and the location of the currently executing dotnet.exe process will be used to determine the install path.
        /// </summary>
        [Required]
        public ITaskItem[] Assets { get; set; }

        /// <summary>
        /// Location of the dotnet-install.{sh,ps1} script
        /// </summary>
        [Required]
        public string InstallScript { get; set; }

        /// <summary>
        /// Optionally overwrite the default installation directory. This is ignored if <see cref="Assets"/> specify the 'InstallDir' metadata value.
        /// </summary>
        public string DotNetHome { get; set; }

        /// <summary>
        /// Timeout per install request.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 600;

        public void Cancel()
        {
            _cts.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            if (Assets.Length == 0)
            {
                return true;
            }

            if (!File.Exists(InstallScript))
            {
                Log.LogError($"Could not install .NET Core. Expected the install script to be in '{InstallScript}'");
                return false;
            }

            var requests = CreateAssetRequests();
            var (exe, defaultArgs) = GetDefaultArgs();

            foreach (var request in requests)
            {
                if (_cts.IsCancellationRequested)
                {
                    return false;
                }

                var arguments = new List<string>();
                arguments.AddRange(defaultArgs);

                var arch = string.IsNullOrEmpty(request.Arch)
                    ? DefaultArch
                    : request.Arch;
                arguments.Add("-Architecture");
                arguments.Add(arch);

                var installDir = string.IsNullOrEmpty(request.InstallDir)
                    ? GetInstallDir(arch)
                    : request.InstallDir;

                arguments.Add("-InstallDir");
                arguments.Add(installDir);

                if (!string.IsNullOrEmpty(request.RuntimeName))
                {
                    arguments.Add("-Runtime");
                    arguments.Add(request.RuntimeName);
                }

                arguments.Add("-Version");
                arguments.Add(request.Version);
                var isFloatingVersion = request.Version.Equals("latest", StringComparison.OrdinalIgnoreCase)
                    || request.Version.Equals("coherent", StringComparison.OrdinalIgnoreCase);

                var assetName = $"{request.DisplayName} ({arch}) {request.Version}";

                if (!string.IsNullOrEmpty(request.Channel))
                {
                    arguments.Add("-Channel");
                    arguments.Add(request.Channel);
                    assetName += $"/{request.Channel}";
                }

                if (!string.IsNullOrEmpty(request.Feed))
                {
                    arguments.Add("-AzureFeed");
                    arguments.Add(request.Feed);
                    arguments.Add("-UncachedFeed");
                    arguments.Add(request.Feed);
                }

                if (!string.IsNullOrEmpty(request.FeedCredential))
                {
                    arguments.Add("-FeedCredential");
                    arguments.Add(request.FeedCredential);
                }

                if (!isFloatingVersion && request.IsInstalled(installDir))
                {
                    Log.LogMessage(MessageImportance.Normal, $"{assetName} is already installed. Skipping installation.");
                    continue;
                }

                Log.LogMessage(MessageImportance.High, $"Installing {assetName}");

                if (isFloatingVersion)
                {
                    Log.LogKoreBuildWarning(KoreBuildErrors.DotNetAssetVersionIsFloating, $"The version of {assetName} being installed is a floating version. This may result in irreproducible builds. Consider specifying an exact version number instead.");
                }

                using (var process = new Process
                {
                    StartInfo =
                    {
                        FileName = exe,
                        Arguments = ArgumentEscaper.EscapeAndConcatenate(arguments),
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    },
                    EnableRaisingEvents = true,
                })
                {
                    Log.LogCommandLine(MessageImportance.Normal, $"Executing {process.StartInfo.FileName} {process.StartInfo.Arguments}");

                    var collectedOutput = new List<string>();
                    process.OutputDataReceived += (o, e) => collectedOutput.Add(e.Data ?? string.Empty);
                    process.ErrorDataReceived += (o, e) => collectedOutput.Add(e.Data ?? string.Empty);

                    var processFinished = new TaskCompletionSource<object>();
                    process.Exited += (o, e) => processFinished.TrySetResult(null);
                    process.Disposed += (o, e) => processFinished.TrySetResult(null);

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    _cts.Token.Register(() => process.Kill());

                    var timeout = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds));

                    var finished = await Task.WhenAny(timeout, processFinished.Task);

                    process.CancelErrorRead();
                    process.CancelOutputRead();

                    if (ReferenceEquals(finished, timeout))
                    {
                        Log.LogMessage(MessageImportance.High, $"dotnet-install output:\n{string.Join("\n", collectedOutput)}");
                        Log.LogError($"dotnet-install of {assetName} timed out after {TimeoutSeconds} seconds.");
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        Log.LogMessage(MessageImportance.High, $"dotnet-install output:\n{string.Join("\n", collectedOutput)}");
                        Log.LogError($"dotnet-install failed on {assetName}.");
                        return false;
                    }
                }
            }

            return true;
        }

        private (string exe, IReadOnlyList<string> args) GetDefaultArgs()
        {
            var defaultArgs = new List<string>();
            var ext = Path.GetExtension(InstallScript).ToLowerInvariant();
            string exe;
            if (ext == ".sh")
            {
                exe = "bash";
                defaultArgs.Add(InstallScript);
            }
            else if (ext == ".cmd")
            {
                exe = InstallScript;
            }
            else
            {
                throw new InvalidOperationException("Unexpected dotnet-install script type");
            }

            // required, otherwise it may attempt to overwrite the dotnet.exe file that is executing this MSBuild process
            defaultArgs.Add("-SkipNonVersionedFiles");

            // don't modify PATH
            defaultArgs.Add("-NoPath");

            // we capture stdout/stderr, so verbose output will only appear in the verbose MSBuild log
            defaultArgs.Add("-Verbose");

            return (exe, defaultArgs);
        }

        private string GetInstallDir(string arch)
        {
            var dotnetPath = string.IsNullOrEmpty(DotNetHome)
                ? Path.GetDirectoryName(DotNetMuxer.MuxerPath)
                : DotNetHome;

            return ShouldInstallToSubfolderOfDotNetHome(arch)
                ? Path.Combine(Path.GetDirectoryName(dotnetPath), arch)
                : dotnetPath;
        }

        private bool ShouldInstallToSubfolderOfDotNetHome(string arch)
        {
            // Only install to $DOTNET_HOME/x64/ on Windows
            // if KOREBUILD_DISABLE_DOTNET_ARCH is undefined or arch != x64
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && (Environment.GetEnvironmentVariable("KOREBUILD_DISABLE_DOTNET_ARCH") == null || !string.Equals(DefaultArch, arch, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<DotNetAssetRequest> CreateAssetRequests()
        {
            var requests = new List<DotNetAssetRequest>();
            var dotnetRuntimes = new List<DotNetAssetRequest>();
            var aspnetcoreRuntimes = new List<DotNetAssetRequest>();

            foreach (var item in Assets)
            {
                DotNetAssetRequest request;

                switch (item.GetMetadata("Runtime").ToLowerInvariant())
                {
                    case "aspnetcore":
                        request = new DotNetAssetRequest.AspNetCoreRuntime(item.ItemSpec);
                        aspnetcoreRuntimes.Add(request);
                        break;
                    case "dotnet":
                        request = new DotNetAssetRequest.DotNetRuntime(item.ItemSpec);
                        dotnetRuntimes.Add(request);
                        break;
                    default:
                        request = new DotNetAssetRequest.DotNetSdk(item.ItemSpec);
                        requests.Add(request);
                        break;
                }

                request.Channel = item.GetMetadata("Channel");
                request.InstallDir = item.GetMetadata("InstallDir");
                request.Arch = item.GetMetadata("Arch");
                // trimmed because dotnet-install.ps1/sh expect the feed not to have this
                request.Feed = item.GetMetadata("Feed")?.TrimEnd(new[] { '/', '\\' });
                request.FeedCredential = item.GetMetadata("FeedCredential");
            }

            // A cheap attempt to reduce the number of things we download.
            // dotnet-sdk.zip often bundles shared runtimes, making susquent downloads unnecessary.
            // likewise, aspnetcore-runtime.zip bundles Microsoft.NETCore.App, so we can often skip the dotnet-runtime download

            return requests
                .Concat(aspnetcoreRuntimes)
                .Concat(dotnetRuntimes);
        }
    }
}
