// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier.Rules
{
    public class SigningVerificationCompositeRule : CompositeRule
    {
        protected override IPackageVerifierRule[] Rules => new IPackageVerifierRule[]
        {
            new AssemblyHasCommitHashAttributeRule(),
            new AssemblyIsBuiltInReleaseConfigurationRule(),
            new AuthenticodeSigningRule(),
            new PowerShellScriptIsSignedRule(),
            new PackageOwnershipRule(),
        };
    }
}
