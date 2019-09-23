// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BuildTools.Tasks.Tests;
using System;
using System.IO;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public abstract class TaskTestBase : IDisposable
    {
        protected string TempDir { get; }
        protected MockEngine MockEngine { get; }
        protected ITestOutputHelper Output { get; }

        protected TaskTestBase(ITestOutputHelper output, MSBuildTestCollectionFixture fixture)
        {
            TempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(TempDir);
            MockEngine = new MockEngine(output);
            fixture.InitializeEnvironment(output);
            Output = output;
        }

        public void Dispose()
        {
            Directory.Delete(TempDir, recursive: true);
        }
    }
}
