// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;

namespace NuGetPackageVerifier.Rules
{
    public class PowerShellScriptIsSignedRule : IPackageVerifierRule
    {
        private static readonly string[] PowerShellExtensions = new string[]
        {
            ".ps1",
            ".psm1",
            ".psd1",
            ".ps1xml"
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var current in context.PackageReader.GetFiles())
            {
                var extension = Path.GetExtension(current);
                if (PowerShellExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    if (!VerifySigned(context.PackageReader, current))
                    {
                        yield return PackageIssueFactory.PowerShellScriptNotSigned(current);
                    }
                }
            }

            yield break;
        }

        private static bool VerifySigned(PackageArchiveReader reader, string packageFilePath)
        {
            bool result;
            using (Stream stream = reader.GetStream(packageFilePath))
            {
                var streamReader = new StreamReader(stream);
                var text = streamReader.ReadToEnd();
                result = (text.IndexOf("# SIG # Begin signature block", StringComparison.OrdinalIgnoreCase) > -1 &&
                    text.IndexOf("# SIG # End signature block", StringComparison.OrdinalIgnoreCase) > -1);
            }

            return result;
        }
    }
}
