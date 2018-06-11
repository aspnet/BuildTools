// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;

namespace KoreBuild.Tasks.Utilities
{
    public class DownloadFileHelper
    {
        public static async Task<bool> DownloadFileAsync(string uri, string destinationPath, bool overwrite, CancellationToken cancellationToken, int timeoutSeconds, TaskLoggingHelper log)
        {
            if (File.Exists(destinationPath) && !overwrite)
            {
                return true;
            }

            const string FileuriProtocol = "file://";

            if (uri.StartsWith(FileuriProtocol, StringComparison.OrdinalIgnoreCase))
            {
                var filePath = uri.Substring(FileuriProtocol.Length);
                log.LogMessage($"Copying '{filePath}' to '{destinationPath}'");
                File.Copy(filePath, destinationPath);
            }
            else
            {
                log.LogMessage($"Downloading '{uri}' to '{destinationPath}'");

                using (var httpClient = new HttpClient
                {
                    // Timeout if no response starts in 2 minutes
                    Timeout = TimeSpan.FromMinutes(2),
                })
                {
                    try
                    {
                        var response = await httpClient.GetAsync(uri, cancellationToken);
                        response.EnsureSuccessStatusCode();
                        cancellationToken.ThrowIfCancellationRequested();

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                        using (var outStream = File.Create(destinationPath))
                        {
                            var responseStream = response.Content.ReadAsStreamAsync();
                            var finished = await Task.WhenAny(responseStream, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));

                            if (!ReferenceEquals(responseStream, finished))
                            {
                                throw new TimeoutException($"Download failed to complete in {timeoutSeconds} seconds.");
                            }

                            responseStream.Result.CopyTo(outStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Downloading '{uri}' failed.");
                        log.LogErrorFromException(ex, showStackTrace: true);

                        File.Delete(destinationPath);
                        return false;
                    }
                }
            }

            return !log.HasLoggedErrors;
        }
    }
}
