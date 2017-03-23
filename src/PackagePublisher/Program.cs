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
        private static CommandOption _feedToUploadTo;
        private static CommandOption _maxRetryCountOption;

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

                var packages = new DirectoryInfo(_packagesDirectory.Value())
                    .EnumerateFiles("*.nupkg", SearchOption.AllDirectories)
                    .Where(fileInfo => !fileInfo.Name.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
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

                if (packages.Count() == 0)
                {
                    Console.WriteLine("No package files were found to be published!!");
                    return 1;
                }

                PublishToFeedAsync(packages).Wait();

                return 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static bool AreValidParameters()
        {
            if (!_feedToUploadTo.HasValue())
            {
                Console.WriteLine("Feed is required.");
                return false;
            }

            if (!_packagesDirectory.HasValue())
            {
                Console.WriteLine("Packages directory is required");
                return false;
            }
            else
            {
                var packageDir = _packagesDirectory.Value();
                if (!Directory.Exists(packageDir))
                {
                    Console.WriteLine($"The supplied directory '{packageDir}' does not exist.");
                    return false;
                }
            }

            _apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine("Api key is required to publish to the feed. Set the environment " +
                    $"variable {ApiKeyEnvironmentVariableName} before invoking this tool again.");
                return false;
            }

            if (_maxRetryCountOption.HasValue())
            {
                _maxRetryCount = int.Parse(_maxRetryCountOption.Value());
            }

            return true;
        }

        private static async Task PublishToFeedAsync(IEnumerable<PackageInfo> packages)
        {
            using (var semaphore = new SemaphoreSlim(initialCount: 4, maxCount: 4))
            {
                var sourceRepository = Repository.Factory.GetCoreV3(_feedToUploadTo.Value(), FeedType.HttpV3);
                var metadataResource = await sourceRepository.GetResourceAsync<MetadataResource>();
                var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

                var tasks = packages.Select(async package =>
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
                Console.WriteLine($"Attempting to publish package {package.Identity} (Attempt: {attempt})");

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

                    Console.WriteLine($"Done publishing package {package.Identity}");
                    return;
                }
                catch (Exception ex) when (attempt <= _maxRetryCount)
                {
                    Console.WriteLine($"Attempt {attempt} failed to publish package {package.Identity}.");
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Retrying...");
                }
            }
        }
    }
}
