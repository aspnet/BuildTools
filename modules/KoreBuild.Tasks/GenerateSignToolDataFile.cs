// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Generates a signdata.json file for the Roslyn SignTool to execute
    /// </summary>
    public class GenerateSignToolDataFile : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] Files { get; set; }
        public ITaskItem[] Exclusions { get; set; }

        /// <summary>
        /// The path to SignData.json file
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            OutputPath = OutputPath.Replace('\\', '/');

            var data = new JObject();
            var signData = new JArray();
            data["sign"] = signData;

            if (Files != null)
            {
                foreach (var certificateGroup in Files.GroupBy(i => (Certificate: i.GetMetadata("Certificate"), StrongName: i.GetMetadata("StrongName"))))
                {
                    var values = new HashSet<string>();
                    foreach (var item in certificateGroup)
                    {
                        values.Add(item.ItemSpec);
                    }

                    var certName = string.IsNullOrEmpty(certificateGroup.Key.Certificate)
                        ? null // needs to be null, not an empty string,
                        : certificateGroup.Key.Certificate;

                    var strongName = string.IsNullOrEmpty(certificateGroup.Key.StrongName)
                        ? null // needs to be null, not an empty string,
                        : certificateGroup.Key.StrongName;

                    signData.Add(new JObject
                    {
                        ["certificate"] = certName,
                        ["strongName"] = strongName,
                        ["values"] = new JArray(values),
                    });
                }
            }

            if (Exclusions != null)
            {
                var exclusions = new HashSet<string>();
                foreach (var item in Exclusions)
                {
                    exclusions.Add(Path.GetFileName(item.ItemSpec));
                }

                data["exclude"] = new JArray(exclusions);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            Log.LogMessage("Generating sign data in {0}", OutputPath);

            using (var file = File.CreateText(OutputPath))
            using (var jsonWriter = new JsonTextWriter(file) { Formatting = Formatting.Indented })
            {
                data.WriteTo(jsonWriter);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
