﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class DefaultCompositeRule : IPackageVerifierRule
    {
        IPackageVerifierRule[] _rules = new IPackageVerifierRule[]
        {
            new AssemblyHasCompanyAttributeRule(),
            new AssemblyHasCopyrightAttributeRule(),
            new AssemblyHasCorrectJsonNetVersionRule(),
            new AssemblyHasDocumentFileRule(),
            new AssemblyHasDescriptionAttributeRule(),
            new AssemblyHasNeutralResourcesLanguageAttributeRule(),
            new AssemblyHasProductAttributeRule(),
            new AssemblyHasServicingAttributeRule(),
            new AssemblyHasVersionAttributesRule(),
            new FrameworkAssembliesDoesNotContainFacadesRule(),
            new SatellitePackageRule(),
            new StrictSemanticVersionValidationRule(),
        };

        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            foreach (var rule in _rules)
            {
                foreach (var issue in rule.Validate(nupkgFile, package, logger))
                {
                    yield return issue;
                }
            }
        }
    }
}
