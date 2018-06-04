// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.BuildTools.CodeSign;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class SignRequestListsAllSignableFiles : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (context.SignRequest == null)
            {
                context.Logger.Log(LogLevel.Info, "Skipping signing rule request verification for " + context.PackageFileInfo.FullName);
                yield break;
            }

            foreach (var file in context.PackageReader.GetFiles())
            {
                if (!SignRequestItem.IsFileTypeSignable(file))
                {
                    continue;
                }

                if (!context.SignRequest.Children.Any(f => string.Equals(f.Path,file)))
                {
                    yield return PackageIssueFactory.SignRequestMissingPackageFile(context.Metadata.Id, file);
                }
            }
        }
    }
}
