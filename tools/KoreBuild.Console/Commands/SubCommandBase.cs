// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace KoreBuild.Console.Commands
{
    internal class SubCommandBase : CommandBase
    {
        private const string _defaultToolsSource = "https://aspnetcore.blob.core.windows.net/buildtools";
        private const string _dotnetFolderName = ".dotnet";

        private CommandOption _repoPathOption;
        private CommandOption _dotNetHomeOption;
        private CommandOption _toolsSourceOption;
        private CommandOption _verbose;

        private string _koreBuildDir;
        private CommandOption _korebuildOverrideOpt;

        public string KoreBuildDir
        {
            get
            {
                if (_koreBuildDir == null)
                {
                    _koreBuildDir = FindKoreBuildDirectory();
                }
                return _koreBuildDir;
            }
        }

        public string ConfigDirectory => Path.Combine(KoreBuildDir, "config");
        public string RepoPath => _repoPathOption.HasValue() ? _repoPathOption.Value() : Directory.GetCurrentDirectory();
        public string DotNetHome => GetDotNetHome();
        public string ToolsSource => _toolsSourceOption.HasValue() ? _toolsSourceOption.Value() : _defaultToolsSource;
        public string SDKVersion => GetDotnetSDKVersion();

        public IReporter Reporter => new ConsoleReporter(PhysicalConsole.Singleton, _verbose != null, false);

        public override void Configure(CommandLineApplication application)
        {
            base.Configure(application);

            _korebuildOverrideOpt = application.Option("--korebuild-override <PATH>", "Where is KoreBuild?", CommandOptionType.SingleValue);
            // for local development only
            _korebuildOverrideOpt.ShowInHelpText = false;

            _verbose = application.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            _toolsSourceOption = application.Option("--tools-source", "The source to draw tools from.", CommandOptionType.SingleValue);
            _repoPathOption = application.Option("--repo-path", "The path to the repo to work on.", CommandOptionType.SingleValue);
            _dotNetHomeOption = application.Option("--dotnet-home", "The place where dotnet lives", CommandOptionType.SingleValue);
            // TODO: Configure file
        }

        protected override bool IsValid()
        {
            if(!Directory.Exists(RepoPath))
            {
                Reporter.Error($"The RepoPath '{RepoPath}' doesn't exist.");
                return false;
            }

            return base.IsValid();
        }

        protected int RunDotnet(params string[] arguments)
            => RunDotnet(arguments, Directory.GetCurrentDirectory());

        protected int RunDotnet(IEnumerable<string> arguments, string workingDir)
        {
            var args = ArgumentEscaper.EscapeAndConcatenate(arguments);

            // use the dotnet.exe file used to start this process
            var dotnet = DotNetMuxer.MuxerPath;
            // if it could not be found, fallback to detecting DOTNET_HOME or PATH
            dotnet = string.IsNullOrEmpty(dotnet) || !Path.IsPathRooted(dotnet)
                ? GetDotNetExecutable()
                : dotnet;

            var psi = new ProcessStartInfo
            {
                FileName = dotnet,
                Arguments = args,
                WorkingDirectory = workingDir,
            };

            Reporter.Verbose($"Executing '{psi.FileName} {psi.Arguments}'");

            var process = Process.Start(psi);
            process.WaitForExit();

            return process.ExitCode;
        }

        private string GetDotnetSDKVersion()
        {
            var sdkVersionEnv = Environment.GetEnvironmentVariable("KOREBUILD_DOTNET_VERSION");
            if (sdkVersionEnv != null)
            {
                return sdkVersionEnv;
            }
            else
            {
                var sdkVersionPath = Path.Combine(ConfigDirectory, "sdk.version");
                return File.ReadAllText(sdkVersionPath).Trim();
            }
        }

        private string GetDotNetHome()
        {
            var dotnetHome = Environment.GetEnvironmentVariable("DOTNET_HOME");
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            var home = Environment.GetEnvironmentVariable("HOME");

            var result = Path.Combine(Directory.GetCurrentDirectory(), _dotnetFolderName);
            if (_dotNetHomeOption.HasValue())
            {
                result = _dotNetHomeOption.Value();
            }
            else if (!string.IsNullOrEmpty(dotnetHome))
            {
                result = dotnetHome;
            }
            else if (!string.IsNullOrEmpty(userProfile))
            {
                result = Path.Combine(userProfile, _dotnetFolderName);
            }
            else if (!string.IsNullOrEmpty(home))
            {
                result = home;
            }

            return result;
        }

        private string FindKoreBuildDirectory()
        {
            if (_korebuildOverrideOpt.HasValue())
            {
                return Path.GetFullPath(_korebuildOverrideOpt.Value());
            }

            var executingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var root = Directory.GetDirectoryRoot(executingDir);
            while (executingDir != root)
            {
                var files = Directory.EnumerateFiles(executingDir);
                var koreProj = Path.Combine(executingDir, "KoreBuild.proj");
                if (files.Contains(koreProj))
                {
                    return executingDir;
                }

                var directories = Directory.EnumerateDirectories(executingDir);

                var fileDir = Path.Combine(executingDir, "files");
                if (directories.Contains(fileDir))
                {
                    return Path.Combine(fileDir, "KoreBuild");
                }

                executingDir = Directory.GetParent(executingDir).FullName;
            }

            Reporter.Error("Couldn't find the KoreBuild directory.");
            throw new DirectoryNotFoundException();
        }

        protected string GetDotNetInstallDir()
        {
            var dotnetDir = DotNetHome;
            if (IsWindows())
            {
                dotnetDir = Path.Combine(dotnetDir, GetArchitecture());
            }

            return dotnetDir;
        }

        protected string GetDotNetExecutable()
        {
            var dotnetDir = GetDotNetInstallDir();

            var dotnetFile = "dotnet";

            if (IsWindows())
            {
                dotnetFile += ".exe";
            }

            return Path.Combine(dotnetDir, dotnetFile);
        }

        protected static string GetArchitecture()
        {
            return Environment.GetEnvironmentVariable("KOREBUILD_DOTNET_ARCH") ?? "x64";
        }

        protected static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
    }
}
