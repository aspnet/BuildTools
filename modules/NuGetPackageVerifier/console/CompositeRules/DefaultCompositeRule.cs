// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class DefaultCompositeRule : CompositeRule
    {
        protected override IPackageVerifierRule[] Rules => new IPackageVerifierRule[]
        {
            new AssemblyHasCompanyAttributeRule(),
            new AssemblyHasCopyrightAttributeRule(),
            new AssemblyHasDocumentFileRule(),
            new AssemblyHasDescriptionAttributeRule(),
            new AssemblyHasNeutralResourcesLanguageAttributeRule(),
            new AssemblyHasProductAttributeRule(),
            new AssemblyHasServicingAttributeRule(),
            new AssemblyHasVersionAttributesRule(),
            new AssemblyStrongNameRule(),
            new RequiredPackageMetadataRule(),
            new RequiredNuSpecInfoRule(),
            new SatellitePackageRule(),
            new StrictSemanticVersionValidationRule(),
            new DependenciesVersionRangeBoundsRule(),
            new DotNetCliToolPackageRule(),
            new DotNetToolPackageRule(),
            new PackageTypesRule(),
            new PackageVersionMatchesAssemblyVersionRule(),
            new BuildItemsRule(),
            new SignRequestListsAllSignableFiles(),
        };
    }
}
