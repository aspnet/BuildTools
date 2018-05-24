// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging.Core;

namespace NuGetPackageVerifier.Rules
{
    public class DotNetToolPackageRule : IPackageVerifierRule
    {
        private const string ToolManifestFileName = "DotnetToolSettings.xml";

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!context.Metadata.PackageTypes.Any(p => p == Constants.DotNetTool))
            {
                yield break;
            }

            var packageFiles = context.PackageReader.GetFiles();
            var manifests = packageFiles?.Where(f => Path.GetFileName(f).Equals(ToolManifestFileName, StringComparison.Ordinal)) ?? Enumerable.Empty<string>();

            if (packageFiles == null || manifests.Count() == 0)
            {
                yield return PackageIssueFactory.DotNetToolMustHaveManifest(Constants.DotNetTool.Name, ToolManifestFileName);
                yield break;
            }

            foreach (var manifestPath in manifests)
            {
                var manifestDir = Path.GetDirectoryName(manifestPath).Replace('\\', '/');

                var manifestStream = context.PackageReader.GetStream(manifestPath);
                var manifest = XDocument.Load(manifestStream);

                AssemblyAttributesDataHelper.SetAssemblyAttributesData(context);

                var commands = manifest.Descendants("Command");
                foreach (var command in commands)
                {
                    var name = command.Attribute("Name");
                    if (string.IsNullOrEmpty(name?.Value))
                    {
                        yield return PackageIssueFactory.DotNetToolMalformedManifest(manifestPath, "Missing Name");
                        continue;
                    }

                    var entryPoint = command.Attribute("EntryPoint");
                    if (entryPoint?.Value == null)
                    {
                        yield return PackageIssueFactory.DotNetToolMalformedManifest(manifestPath, "Missing EntryPoint");
                        continue;
                    }

                    var entryPointPath = manifestDir + '/' + entryPoint.Value;
                    if (!packageFiles.Any(a => a.Equals(entryPointPath, StringComparison.Ordinal)))
                    {
                        yield return PackageIssueFactory.DotNetToolMissingEntryPoint(manifestPath, entryPoint.Value);
                    }
                }
            }
        }
    }
}
