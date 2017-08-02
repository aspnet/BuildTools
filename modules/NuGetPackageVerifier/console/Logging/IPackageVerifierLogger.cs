// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetPackageVerifier.Logging
{
    public interface IPackageVerifierLogger
    {
        void Log(LogLevel logLevel, string message);
    }
}
