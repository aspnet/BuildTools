// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using KoreBuild.Tasks.Utilities;
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

        public Task<bool> ExecuteAsync()
        {
            return DownloadFileHelper.DownloadFileAsync(Uri, DestinationPath, Overwrite,  _cts, TimeoutSeconds, Log);
        }
    }
}
