// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.BuildTools.CodeSign;
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
        // well-known metadata on items set by the MSBuild engine
        private const string ProjectDirMetadataName = "DefiningProjectDirectory";
        private const string ProjectFileMetadataName = "DefiningProjectFullPath";

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
            OutputPath = NormalizePath(OutputPath);
            BasePath = NormalizePath(BasePath);

            return Execute(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                return File.CreateText(OutputPath);
            });
        }

        internal bool Execute(Func<TextWriter> writerFactory)
        {
            var signRequestCollection = new SignRequestCollection();

            var containers = new Dictionary<string, SignRequestItem>(StringComparer.OrdinalIgnoreCase);
            var isContainer = new bool[Requests.Length];
            for (var i = 0; i < Requests.Length; i++)
            {
                var item = Requests[i];
                if (bool.TryParse(item.GetMetadata("IsContainer"), out var isc) && isc)
                {
                    isContainer[i] = true;
                    var itemType = string.IsNullOrEmpty(item.GetMetadata("Type"))
                        ? Path.GetExtension(item.ItemSpec)
                        : item.GetMetadata("Type");

                    var type = SignRequestItem.GetTypeFromFileExtension(itemType);
                    var normalizedPath = GetRelativePath(BasePath, item.ItemSpec);
                    SignRequestItem container;

                    switch (type)
                    {
                        case SignRequestItemType.Zip:
                            container = SignRequestItem.CreateZip(normalizedPath);
                            break;
                        case SignRequestItemType.Nupkg:
                            container = SignRequestItem.CreateNugetPackage(normalizedPath, item.GetMetadata("Certificate"));
                            break;
                        case SignRequestItemType.Vsix:
                            container = SignRequestItem.CreateVsix(normalizedPath, item.GetMetadata("Certificate"));
                            break;
                        default:
                            Log.LogError(
                                $"Unknown container type for signed file request:'{item.ItemSpec}'. Signing request container must specify the metadata 'Type'.");
                            continue;
                    }

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
                var normalizedPath = GetRelativePath(BasePath, item.ItemSpec);
                var containerPath = item.GetMetadata("Container");
                if (!string.IsNullOrEmpty(containerPath))
                {
                    if (!containers.TryGetValue(containerPath, out var container))
                    {
                        Log.LogError(
                            $"Signing request item '{item.ItemSpec}' specifies an unknown container '{containerPath}'.");
                        continue;
                    }

                    var itemPath = GetPathWithinContainer(item);

                    if (string.IsNullOrEmpty(itemPath))
                    {
                        Log.LogError(null, null, null, item.GetMetadata(ProjectFileMetadataName), 0, 0, 0, 0,
                            message: $"Could not identify the path for the signable file {item.ItemSpec}");
                        continue;
                    }

                    normalizedPath = NormalizePath(itemPath);
                    var file = SignRequestItem.CreateFile(normalizedPath,
                        item.GetMetadata("Certificate"),
                        item.GetMetadata("StrongName"));
                    container.AddChild(file);
                }
                else
                {
                    var file = SignRequestItem.CreateFile(normalizedPath,
                        item.GetMetadata("Certificate"),
                        item.GetMetadata("StrongName"));
                    signRequestCollection.Add(file);
                }
            }

            if (Exclusions != null)
            {
                foreach (var item in Exclusions)
                {
                    var normalizedPath = GetRelativePath(BasePath, item.ItemSpec);

                    var containerPath = item.GetMetadata("Container");
                    if (!string.IsNullOrEmpty(containerPath))
                    {
                        if (!containers.TryGetValue(containerPath, out var container))
                        {
                            Log.LogError(
                                $"Exclusion item '{item.ItemSpec}' specifies an unknown container '{containerPath}'.");
                            continue;
                        }

                        var itemPath = GetPathWithinContainer(item);
                        if (string.IsNullOrEmpty(itemPath))
                        {
                            Log.LogError(null, null, null, item.GetMetadata(ProjectFileMetadataName), 0, 0, 0, 0,
                                message: $"Could not identify the path for the signable file {item.ItemSpec}");
                            continue;
                        }

                        normalizedPath = NormalizePath(itemPath);
                        var file = SignRequestItem.CreateExclusion(normalizedPath);
                        container.AddChild(file);
                    }
                    else
                    {
                        var file = SignRequestItem.CreateExclusion(normalizedPath);
                        signRequestCollection.Add(file);
                    }
                }
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            using (var stream = writerFactory())
            using (var writer = new SignRequestManifestXmlWriter(stream))
            {
                writer.Write(signRequestCollection);
            }

            Log.LogMessage($"Generated bill of materials in {OutputPath}");

            return !Log.HasLoggedErrors;
        }

        private static string GetPathWithinContainer(ITaskItem item)
        {
            // always prefer an explicit package path
            var itemPath = item.GetMetadata("PackagePath");
            if (string.IsNullOrEmpty(itemPath))
            {
                // allow defining SignedPackageFile using just ItemSpec
                var projectDir = item.GetMetadata(ProjectDirMetadataName);
                if (!string.IsNullOrEmpty(projectDir))
                {
                    // users will typically write items with relative itemspecs, but we can't get the original item spec.
                    // Infer the original item spec by getting the path relative to the project directory
                    return Path.GetRelativePath(projectDir, item.ItemSpec);
                }
            }

            if (itemPath.EndsWith('/') || itemPath.EndsWith('\\'))
            {
                return Path.Combine(itemPath, Path.GetFileName(item.ItemSpec));
            }

            return itemPath;
        }

        private static string GetRelativePath(string basePath, string path)
            => NormalizePath(Path.GetRelativePath(basePath, path));

        private static string NormalizePath(string path)
            => string.IsNullOrEmpty(path)
            ? path
            : path.Replace('\\', '/');

    }
}
