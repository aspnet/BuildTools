// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace PackagePublisher
{
    public static class Program
    {
        private const string ApiKeyEnvironmentVariableName = "APIKey";
        private static CommandOption _packagesDirectory;
        private static CommandOption _packagePath;
        private static CommandOption _feedToUploadTo;
        private static CommandOption _maxRetryCountOption;
        private static IEnumerable<PackageInfo> _packages;

        // Do NOT log the key
        private static string _apiKey = null;
        private static int _maxRetryCount = 10;

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            _packagesDirectory = app.Option(
                "-d|--directory",
                "The directory containing .nupkg file(s)",
                CommandOptionType.SingleValue);
            _packagePath = app.Option(
                "-p|--package",
                "Full file path to a .nupkg file",
                CommandOptionType.MultipleValue);
            _feedToUploadTo = app.Option(
                "-f|--feed",
                "Feed to upload the .nupkg file(s) to",
                CommandOptionType.SingleValue);
            _maxRetryCountOption = app.Option(
                "-c|--retryCount",
                $"Max retry count to publish a package. Defaults to {_maxRetryCount}",
                CommandOptionType.SingleValue);
            var help = app.Option("-h|--help", "Show help", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                if (help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                if (!AreValidParameters())
                {
                    app.ShowHelp();
                    return 1;
                }

                var packageDir = _packagesDirectory.Value();
                if (!string.IsNullOrEmpty(packageDir))
                {
                    if (!Directory.Exists(packageDir))
                    {
                        Log.WriteError($"The supplied directory '{packageDir}' does not exist.");
                        return 1;
                    }
                    else
                    {
                        _packages = new DirectoryInfo(packageDir)
                        .EnumerateFiles("*.nupkg", SearchOption.AllDirectories)
                        .ProcessPackages();
                    }
                }
                else
                {
                    var files = new List<FileInfo>();
                    foreach (var filePath in _packagePath.Values)
                    {
                        if (!File.Exists(filePath))
                        {
                            Log.WriteError($"The supplied file '{filePath}' does not exist.");
                            return 1;
                        }
                        var fileInfo = new FileInfo(filePath);
                        if (!string.Equals(fileInfo.Extension, ".nupkg", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.WriteError($"The supplied file '{filePath}' is not a 'nupkg' file.");
                            return 1;
                        }
                        files.Add(fileInfo);
                    }

                    _packages = files.ProcessPackages();
                }

                if (_packages.Count() == 0)
                {
                    Log.WriteWarning("No package files were found to be published!!");
                    return 1;
                }

                PublishToFeedAsync().Wait();

                return 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Log.WriteError(ex.ToString());
                return 1;
            }
        }

        private static bool AreValidParameters()
        {
            if (!_feedToUploadTo.HasValue() || string.IsNullOrEmpty(_feedToUploadTo.Value()))
            {
                Log.WriteError("Feed is required.");
                return false;
            }

            _apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
            if (string.IsNullOrEmpty(_apiKey))
            {
                Log.WriteError("Api key is required to publish to the feed. Set the environment " +
                    $"variable {ApiKeyEnvironmentVariableName} before inovking this tool again.");
                return false;
            }

            if (!_packagesDirectory.HasValue() && !_packagePath.HasValue())
            {
                Log.WriteError("Provide a directory path with the packages or a full file path to package(s)");
                return false;
            }
            else if (_packagesDirectory.HasValue() && _packagePath.HasValue())
            {
                Log.WriteError("Both package directory and package path cannot be supplied. Specify either of them only.");
                return false;
            }
            else
            {
                if (_packagesDirectory.HasValue() && string.IsNullOrEmpty(_packagesDirectory.Value()))
                {
                    Log.WriteError("Invalid value for package directory.");
                    return false;
                }

                if (_packagePath.HasValue() && _packagePath.Values.Count == 0)
                {
                    Log.WriteError("Invalid value for package path.");
                    return false;
                }
            }

            if (_maxRetryCountOption.HasValue())
            {
                _maxRetryCount = int.Parse(_maxRetryCountOption.Value());
            }

            return true;
        }

        private static IEnumerable<PackageInfo> ProcessPackages(this IEnumerable<FileInfo> packageFiles)
        {
            return packageFiles
                .Select(fileInfo =>
                {
                    using (var fileStream = File.OpenRead(fileInfo.FullName))
                    using (var reader = new PackageArchiveReader(fileStream))
                    {
                        return new PackageInfo()
                        {
                            Identity = reader.GetIdentity(),
                            PackagePath = fileInfo.FullName
                        };
                    }
                });
        }

        private static async Task PublishToFeedAsync()
        {
            using (var semaphore = new SemaphoreSlim(initialCount: 4, maxCount: 4))
            {
                var sourceRepository = Repository.Factory.GetCoreV3(_feedToUploadTo.Value(), FeedType.HttpV3);
                var metadataResource = await sourceRepository.GetResourceAsync<MetadataResource>();
                var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

                var tasks = _packages.Select(async package =>
                {
                    await semaphore.WaitAsync(TimeSpan.FromMinutes(3));
                    try
                    {
                        await PushPackageAsync(packageUpdateResource, package);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                await Task.WhenAll(tasks);
            }
        }

        private static async Task PushPackageAsync(PackageUpdateResource packageUpdateResource, PackageInfo package)
        {
            var attempt = 0;
            while (true)
            {
                attempt++;
                Log.WriteInformation($"Attempting to publish package {package.Identity} (Attempt: {attempt})");

                try
                {
                    await packageUpdateResource.Push(
                        package.PackagePath,
                        symbolSource: null,
                        timeoutInSecond: 30,
                        disableBuffering: false,
                        getApiKey: _ => _apiKey,
                        getSymbolApiKey: _ => null,
                        log: NullLogger.Instance);

                    Log.WriteInformation($"Done publishing package {package.Identity}");
                    return;
                }
                catch (Exception ex) when (attempt <= _maxRetryCount)
                {
                    Log.WriteInformation($"Attempt {attempt} failed to publish package {package.Identity}.");
                    Log.WriteInformation(ex.ToString());
                    Log.WriteInformation("Retrying...");
                }
            }
        }
    }
}
