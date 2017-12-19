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
        private const string ToolManifestPath = "tools/DotnetToolSettings.xml";

        private static PackageType DotNetTool = new PackageType("DotnetTool", PackageType.EmptyVersion);

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!context.Metadata.PackageTypes.Any(p => p == DotNetTool))
            {
                yield break;
            }

            var packageFiles = context.PackageReader.GetFiles();

            if (packageFiles == null || packageFiles.SingleOrDefault(f => f.Equals(ToolManifestPath, StringComparison.Ordinal)) == null)
            {
                yield return PackageIssueFactory.DotNetToolMustHaveManifest(DotNetTool.Name, ToolManifestPath);
                yield break;
            }

            var manifestStream = context.PackageReader.GetStream(ToolManifestPath);
            var manifest = XDocument.Load(manifestStream);

            AssemblyAttributesDataHelper.SetAssemblyAttributesData(context);

            var commands = manifest.Descendants("Command");
            foreach (var command in commands)
            {
                var name = command.Attribute("Name");
                if (string.IsNullOrEmpty(name?.Value))
                {
                    yield return PackageIssueFactory.DotNetToolMalformedManifest("Missing Name");
                    continue;
                }

                var entryPoint = command.Attribute("EntryPoint");
                if (entryPoint?.Value == null)
                {
                    yield return PackageIssueFactory.DotNetToolMalformedManifest("Missing EntryPoint");
                    continue;
                }


                if (!packageFiles.Any(a => a.Equals(entryPoint.Value, StringComparison.Ordinal))
                    && !packageFiles.Any(a => a.StartsWith("tools/netcoreapp", StringComparison.Ordinal) && Path.GetFileName(a).Equals(entryPoint.Value, StringComparison.Ordinal)))
                {
                    // In the initial implementation of global tools, EntryPoint is just a filename, not a full path inside the package.
                    yield return PackageIssueFactory.DotNetToolMissingEntryPoint(entryPoint.Value);
                }
            }
        }
    }
}
