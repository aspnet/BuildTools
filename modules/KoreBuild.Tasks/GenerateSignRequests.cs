// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Generates an XML document that can be passed to a tool for signing.
    /// <para>
    /// The items are expected to be files.
    /// </para>
    /// </summary>
    public class GenerateSignRequest : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Files or containers of files that should be signed.
        /// Required metadata 'Certificate' or 'StrongName'. Both can be specified.
        /// Optional metadata: 'IsContainer'. Set this to true for files that can be extract and have inner parts signed. For example, nupkg and vsix files.
        /// </summary>
        [Required]
        public ITaskItem[] Requests { get; set; }

        /// <summary>
        /// Items that should explicitly be marked as 'excluded' in the sign request.
        /// Only files in listed as a request item will be signed, but excluded files can be
        /// added as well so tests can validate that all files in a container are accounted for.
        /// </summary>
        public ITaskItem[] Exclusions { get; set; }

        /// <summary>
        /// The folder that conatins all items. The sign request file paths will be normalized to this path.
        /// </summary>
        [Required]
        public string BasePath { get; set; }

        /// <summary>
        /// The output path of the sign request file.
        /// </summary>
        [Required]
        [Output]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            OutputPath = OutputPath.Replace('\\', '/');
            BasePath = BasePath.Replace('\\', '/');

            return Execute(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                return File.CreateText(OutputPath);
            });
        }

        internal bool Execute(Func<TextWriter> writerFactory)
        {
            var signRequestCollection = new SignRequestCollection();

            var containers = new Dictionary<string, SignRequestItem.Container>(StringComparer.OrdinalIgnoreCase);
            var isContainer = new bool[Requests.Length];
            for (var i = 0; i < Requests.Length; i++)
            {
                var item = Requests[i];
                if (bool.TryParse(item.GetMetadata("IsContainer"), out var isc) && isc)
                {
                    isContainer[i] = true;
                    var type = item.GetMetadata("Type");
                    if (string.IsNullOrEmpty(type))
                    {
                        type = GetKnownContainerTypes(item);
                    }

                    if (string.IsNullOrEmpty(type))
                    {
                        Log.LogError($"Unknown container type for signed file request:'{item.ItemSpec}'. Signing request container must specify the metadata 'Type'.");
                        continue;
                    }

                    var normalizedPath = NormalizePath(BasePath, item.ItemSpec);
                    var container = new SignRequestItem.Container(
                        normalizedPath,
                        type,
                        item.GetMetadata("Certificate"),
                        item.GetMetadata("StrongName"));

                    containers[item.ItemSpec] = container;
                    signRequestCollection.Add(container);
                }
            }

            for (var i = 0; i < Requests.Length; i++)
            {
                if (isContainer[i])
                {
                    continue;
                }

                var item = Requests[i];
                var normalizedPath = NormalizePath(BasePath, item.ItemSpec);
                var containerPath = item.GetMetadata("Container");
                if (!string.IsNullOrEmpty(containerPath))
                {
                    if (!containers.TryGetValue(containerPath, out var container))
                    {
                        Log.LogError($"Signing request item '{item.ItemSpec}' specifies an unknown container '{containerPath}'.");
                        continue;
                    }
                    var packagePath = item.GetMetadata("PackagePath");
                    normalizedPath = string.IsNullOrEmpty(packagePath) ? normalizedPath : packagePath.Replace('\\', '/');
                    var file = new SignRequestItem.File(normalizedPath,
                        item.GetMetadata("Certificate"),
                        item.GetMetadata("StrongName"));
                    container.AddItem(file);
                }
                else
                {
                    var file = new SignRequestItem.File(normalizedPath,
                      item.GetMetadata("Certificate"),
                      item.GetMetadata("StrongName"));
                    signRequestCollection.Add(file);
                }
            }

            foreach (var item in Exclusions)
            {
                var normalizedPath = NormalizePath(BasePath, item.ItemSpec);

                var containerPath = item.GetMetadata("Container");
                if (!string.IsNullOrEmpty(containerPath))
                {
                    if (!containers.TryGetValue(containerPath, out var container))
                    {
                        Log.LogError($"Exclusion item '{item.ItemSpec}' specifies an unknown container '{containerPath}'.");
                        continue;
                    }

                    var packagePath = item.GetMetadata("PackagePath");
                    normalizedPath = string.IsNullOrEmpty(packagePath) ? normalizedPath : packagePath.Replace('\\', '/');
                    var file = new SignRequestItem.Exclusion(normalizedPath);
                    container.AddItem(file);
                }
                else
                {
                    var file = new SignRequestItem.Exclusion(normalizedPath);
                    signRequestCollection.Add(file);
                }
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            using (var stream = writerFactory())
            using (var writer = new SignRequestCollectionXmlWriter(stream))
            {
                writer.Write(signRequestCollection);
            }

            Log.LogMessage($"Generated bill of materials in {OutputPath}");

            return !Log.HasLoggedErrors;
        }

        private static string GetKnownContainerTypes(ITaskItem item)
        {
            string type = null;

            switch (Path.GetExtension(item.ItemSpec).ToLowerInvariant())
            {
                case ".nupkg":
                    type = "nupkg";
                    break;
                case ".zip":
                    type = "zip";
                    break;
                case ".tar.gz":
                case ".tgz":
                    type = "tar.gz";
                    break;
                case ".vsix":
                    type = "vsix";
                    break;
                case ".msi":
                    type = "msi";
                    break;
            }

            return type;
        }

        private static string NormalizePath(string basePath, string path)
        {
            return Path.GetRelativePath(basePath, path).Replace('\\', '/');
        }
    }
}
