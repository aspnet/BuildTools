// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetPackageVerifier
{
    public class IssueIgnore
    {
        public string PackageId { get; set; }
        public string IssueId { get; set; }
        public string Instance { get; set; }
        public string Justification { get; set; }
    }
}
