// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class SignRequestListsAllSignableFiles : IPackageVerifierRule
    {
        private static readonly HashSet<string> SignableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll",
            ".exe",
            ".ps1",
            ".psd1",
            ".psm1",
            ".psc1",
            ".ps1xml",
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (context.SignRequest == null)
            {
                context.Logger.Log(LogLevel.Info, "Skipping signing rule request verification for " + context.PackageFileInfo.FullName);
                yield break;
            }

            foreach (var file in context.PackageReader.GetFiles())
            {
                var ext = Path.GetExtension(file);
                if (!SignableExtensions.Contains(ext))
                {
                    continue;
                }

                if (!context.SignRequest.FilesToSign.Contains(file) && !context.SignRequest.FilesExcludedFromSigning.Contains(file))
                {
                    yield return PackageIssueFactory.SignRequestMissingPackageFile(context.Metadata.Id, file);
                }
            }
        }
    }
}
