// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetPackageVerifier.Logging;
using Xunit.Abstractions;

namespace NuGetPackageVerifier.Tests.Utilities
{
    internal class TestLogger : IPackageVerifierLogger
    {
        private ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Log(LogLevel logLevel, string message)
        {
            _output.WriteLine($"{logLevel}: {message}");
        }
    }
}
