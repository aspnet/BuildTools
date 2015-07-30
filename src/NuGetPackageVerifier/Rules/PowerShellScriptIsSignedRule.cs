// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGetPackageVerifier.Logging;

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

        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            foreach (IPackageFile current in package.GetFiles())
            {
                var extension = Path.GetExtension(current.Path);
                if (PowerShellExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    if (!VerifySigned(current))
                    {
                        yield return PackageIssueFactory.PowerShellScriptNotSigned(current.Path);
                    }
                }
            }

            yield break;
        }

        private static bool VerifySigned(IPackageFile packageFile)
        {
            bool result;
            using (Stream stream = packageFile.GetStream())
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
