// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using KoreBuild.Tasks.Internal;

namespace KoreBuild.Tasks
{
    public class PushNuGetPackages : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private const int _maxRetryCount = 5;
        private const int _maxParallelPackagePushes = 4;

        private ConcurrentBag<PackageInfo> _packages;
        private readonly CancellationTokenSource _packagePushCancellationTokenSource = new CancellationTokenSource();

        [Required]
        public ITaskItem[] Packages { get; set; }

        [Required]
        public string Feed { get; set; }

        // not be required if pushing to the filesystem
        public string ApiKey { get; set; }

        public int TimeoutSeconds { get; set; } = 90;

        public void Cancel()
        {
            _packagePushCancellationTokenSource.Cancel();
        }

        public override bool Execute()
        {
            if (Packages.Length == 0)
            {
                Log.LogWarning("No package files were found to be published.");
                return true;
            }

            if (string.IsNullOrEmpty(Feed))
            {
                Log.LogError("Feed must not be null or empty.");
                return false;
            }

            var packages = Packages
                .Select(fileInfo =>
                {
                    using (var fileStream = File.OpenRead(fileInfo.ItemSpec))
                    using (var reader = new PackageArchiveReader(fileStream))
                    {
                        return new PackageInfo
                        {
                            Identity = reader.GetIdentity(),
                            PackagePath = fileInfo.ItemSpec
                        };
                    }
                });

            _packages = new ConcurrentBag<PackageInfo>(packages);

            Log.LogMessage(MessageImportance.High, "Attempting to push {0} package(s) to {1}", Packages.Length, Feed);
            try
            {
                PublishToFeedAsync().GetAwaiter().GetResult();
                Log.LogMessage(MessageImportance.High, "Successfully pushed {0} package(s) to {1}", Packages.Length, Feed);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private async Task PublishToFeedAsync()
        {
            Log.LogMessage("Publishing packages to feed: {0}", Feed);

            var sourceRepository = Repository.Factory.GetCoreV3(Feed, FeedType.HttpV3);
            var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

            var tasks = new Task[_maxParallelPackagePushes];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = PushPackagesAsync(packageUpdateResource);
            }

            await Task.WhenAll(tasks);
        }

        private async Task PushPackagesAsync(PackageUpdateResource packageUpdateResource)
        {
            while (_packages.TryTake(out var package))
            {
                await PushPackageAsync(packageUpdateResource, package);
            }
        }

        private async Task PushPackageAsync(PackageUpdateResource packageUpdateResource, PackageInfo package)
        {
            for (var attempt = 1; attempt <= _maxRetryCount; attempt++)
            {
                // Fail fast if a parallel push operation has already failed
                _packagePushCancellationTokenSource.Token.ThrowIfCancellationRequested();

                Log.LogMessage($"Attempting to publish package {package.Identity} (Attempt: {attempt})");

                try
                {
                    await packageUpdateResource.Push(
                        package.PackagePath,
                        symbolSource: null,
                        timeoutInSecond: TimeoutSeconds,
                        disableBuffering: false,
                        getApiKey: _ => ApiKey,
                        getSymbolApiKey: _ => null,
                        log: NullLogger.Instance);

                    Log.LogMessage(MessageImportance.High, $"Published package {package.Identity}");

                    return;
                }
                catch (Exception ex) when (attempt < _maxRetryCount) // allow exception to be thrown at the last attempt
                {
                    Log.LogMessage(
                        MessageImportance.High,
                        $"Attempt {attempt} failed to publish package {package.Identity}." +
                        Environment.NewLine +
                        ex +
                        Environment.NewLine +
                        "Retrying...");
                }
                catch
                {
                    _packagePushCancellationTokenSource.Cancel();
                    throw;
                }
            }
        }
    }
}
