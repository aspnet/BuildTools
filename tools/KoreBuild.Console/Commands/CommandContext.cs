// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;
using Newtonsoft.Json;

namespace KoreBuild.Console.Commands
{
    internal class CommandContext
    {
        private const string _defaultToolsSource = "https://aspnetcore.blob.core.windows.net/buildtools";
        private const string _dotnetFolderName = ".dotnet";

        private CommandOption _repoPathOption;
        private CommandOption _dotNetHomeOption;
        private CommandOption _toolsSourceOption;
        private CommandOption _verbose;

        private string _koreBuildDir;
        private CommandOption _korebuildOverrideOpt;

        public CommandContext(CommandLineApplication application)
        {
            _korebuildOverrideOpt = application.Option("--korebuild-override <PATH>", "Where is KoreBuild?", CommandOptionType.SingleValue, inherited: true);
            // for local development only
            _korebuildOverrideOpt.ShowInHelpText = false;

            _verbose = application.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, inherited: true);
            _toolsSourceOption = application.Option("--tools-source", "The source to draw tools from.", CommandOptionType.SingleValue, inherited: true);
            _repoPathOption = application.Option("--repo-path", "The path to the repo to work on.", CommandOptionType.SingleValue, inherited: true);
            _dotNetHomeOption = application.Option("--dotnet-home", "The place where dotnet lives", CommandOptionType.SingleValue, inherited: true);
            // TODO: Configure file
        }

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

        public string KoreBuildVersion => GetKoreBuildVersion();

        public IReporter Reporter => new ConsoleReporter(PhysicalConsole.Singleton, _verbose != null, false);

        private string GetDotnetSDKVersion()
        {
            var globalJsonPath = Path.Combine(RepoPath, "global.json");

            if (!File.Exists(globalJsonPath))
            {
                throw new FileNotFoundException($"{globalJsonPath} doesn't exist. Your repo root must have a valid global.json.");
            }

            var globalJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(globalJsonPath));

            return ((Dictionary<string, string>)globalJson["sdk"])["version"];
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

        private string GetKoreBuildVersion()
        {
            var dir = new DirectoryInfo(FindKoreBuildDirectory());
            return dir.Parent.Name;
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

        public string GetDotNetInstallDir()
        {
            var dotnetDir = DotNetHome;
            if (IsWindows())
            {
                dotnetDir = Path.Combine(dotnetDir, GetArchitecture());
            }

            return dotnetDir;
        }

        public string GetDotNetExecutable()
        {
            var dotnetDir = GetDotNetInstallDir();

            var dotnetFile = "dotnet";

            if (IsWindows())
            {
                dotnetFile += ".exe";
            }

            return Path.Combine(dotnetDir, dotnetFile);
        }

        public string GetArchitecture()
        {
            return Environment.GetEnvironmentVariable("KOREBUILD_DOTNET_ARCH") ?? "x64";
        }

        public bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
    }
}
