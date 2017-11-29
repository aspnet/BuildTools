// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Downloads a file.
    /// </summary>
    public class DownloadFile : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The file to download. Can be prefixed with <c>file://</c> for local file paths.
        /// </summary>
        [Required]
        public string Uri { get; set; }

        /// <summary>
        /// Destination for the downloaded file. If the file already exists, it is not re-downloaded unless <see cref="Overwrite"/> is true.
        /// </summary>
        [Required]
        public string DestinationPath { get; set; }

        /// <summary>
        /// Should <see cref="DestinationPath"/> be overwritten. When <c>true</c>, the file is always re-downloaded.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// The maximum amount of time to allow for downloading the file. Defaults to 15 minutes.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60 * 15;

        public void Cancel() => _cts.Cancel();

        public override bool Execute() => ExecuteAsync().Result;

        public async Task<bool> ExecuteAsync()
        {
            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return true;
            }

            const string FileUriProtocol = "file://";

            if (Uri.StartsWith(FileUriProtocol, StringComparison.OrdinalIgnoreCase))
            {
                var filePath = Uri.Substring(FileUriProtocol.Length);
                Log.LogMessage($"Copying '{filePath}' to '{DestinationPath}'");
                File.Copy(filePath, DestinationPath);
            }
            else
            {
                Log.LogMessage($"Downloading '{Uri}' to '{DestinationPath}'");

                using (var httpClient = new HttpClient
                {
                    // Timeout if no response starts in 2 minutes
                    Timeout = TimeSpan.FromMinutes(2),
                })
                {
                    try
                    {
                        var response = await httpClient.GetAsync(Uri, _cts.Token);
                        response.EnsureSuccessStatusCode();
                        _cts.Token.ThrowIfCancellationRequested();

                        Directory.CreateDirectory(Path.GetDirectoryName(DestinationPath));

                        using (var outStream = File.Create(DestinationPath))
                        {
                            var responseStream = response.Content.ReadAsStreamAsync();
                            var finished = await Task.WhenAny(responseStream, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));

                            if (!ReferenceEquals(responseStream, finished))
                            {
                                throw new TimeoutException($"Download failed to complete in {TimeoutSeconds} seconds.");
                            }

                            responseStream.Result.CopyTo(outStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Downloading '{Uri}' failed.");
                        Log.LogErrorFromException(ex, showStackTrace: true);

                        File.Delete(DestinationPath);
                        return false;
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
