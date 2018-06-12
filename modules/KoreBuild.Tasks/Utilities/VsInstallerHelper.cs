// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace KoreBuild.Tasks.Utilities
{
    internal class VsInstallerHelper
    { 
        public static async Task<string> DownloadVsExe(TaskLoggingHelper log, string vsProductType)
        {
            // TODO put this in an obj folder instead of temp?
            var tempPath = Path.Combine(Path.GetTempPath(), "vs.exe");

            await DownloadFileHelper.DownloadFileAsync(uri: $"https://aka.ms/vs/15/release/vs_{vsProductType}.exe",
                destinationPath: tempPath,
                overwrite: true,
                cancellationToken: new CancellationToken(),
                timeoutSeconds: 60 * 15,
                log);

            log.LogMessage($"Downloaded visual studio exe to {tempPath}.");

            return tempPath;
        }

        private class VsJsonFile
        {
            [JsonProperty(PropertyName = "channelUri")]
            public string ChannelUri { get; set; }
            [JsonProperty(PropertyName = "channelId")]
            public string ChannelId { get; set; }
            [JsonProperty(PropertyName = "productId")]
            public string ProductId { get; set; }
            [JsonProperty(PropertyName = "includeRecommended")]
            public bool IncludeRecommended { get; set; }
            [JsonProperty(PropertyName = "addProductLang")]
            public List<string> AddProductLang { get; set; }
            [JsonProperty(PropertyName = "add")]
            public List<string> Add { get; set; }
        }

        public static string CreateVsFileFromRequiredToolset(KoreBuildSettings.VisualStudioToolset vsToolset, TaskLoggingHelper log, string vsProductType)
        {
            var vsFile = new VsJsonFile
            {
                ChannelUri = "https://aka.ms/vs/15/release/channel",
                ChannelId = "VisualStudio.15.Release",
                ProductId = $"Microsoft.VisualStudio.Product.{vsProductType}",
                IncludeRecommended = false,
                AddProductLang = new List<string>
                {
                    "en-US"
                },
                Add = new List<string>(vsToolset.RequiredWorkloads)
            };

            var tempFile = Path.Combine(Path.GetTempPath(), "vs.json");

            var json = JsonConvert.SerializeObject(vsFile);
            File.WriteAllText(tempFile, json);

            return tempFile;
        }
    }
}
