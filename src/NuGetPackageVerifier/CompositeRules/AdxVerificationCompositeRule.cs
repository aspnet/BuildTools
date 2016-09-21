// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class AdxVerificationCompositeRule : IPackageVerifierRule
    {
        IPackageVerifierRule[] _rules = new IPackageVerifierRule[]
        {
            new AssemblyHasCommitHashAttributeRule(),
            new AssemblyHasCompanyAttributeRule(),
            new AssemblyHasCopyrightAttributeRule(),
            new AssemblyHasCorrectBuildConfigurationRule(),
            new AssemblyHasCorrectJsonNetVersionRule(),
            new AssemblyHasDocumentFileRule(),
            new AssemblyHasNeutralResourcesLanguageAttributeRule(),
            new AssemblyHasProductAttributeRule(),
            new AssemblyHasServicingAttributeRule(),
            new AssemblyHasVersionAttributesRule(),
            new AssemblyStrongNameRule(),
            new PackageAuthorRule(),
            new PackageTypesRule(),
            new SatellitePackageRule(),
            new StrictSemanticVersionValidationRule(),
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var rule in _rules)
            {
                foreach (var issue in rule.Validate(context))
                {
                    yield return issue;
                }
            }
        }
    }
}
