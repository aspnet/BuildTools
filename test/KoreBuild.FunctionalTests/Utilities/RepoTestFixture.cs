// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Xunit;

namespace KoreBuild.FunctionalTests
{
    public class RepoTestFixture : IDisposable
    {
        private static readonly string _solutionDir;
        private readonly ConcurrentQueue<IDisposable> _disposables = new ConcurrentQueue<IDisposable>();

        static RepoTestFixture()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.GetFiles("BuildTools.sln")?.Length > 0)
                {
                    _solutionDir = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }
        }

        public string ScriptsDir { get; } = Path.Combine(_solutionDir, "scripts", "bootstrapper");
        public string ToolsSource { get; } = Path.Combine(_solutionDir, "artifacts");
        public string LogDir { get; } = Path.Combine(_solutionDir, "artifacts", "tests");
        public string TestAssets { get; } = Path.Combine(_solutionDir, "testassets");

        public TestApp CreateTestApp(string name)
        {
            var srcDir = Path.Combine(TestAssets, name);
            var instanceName = Path.GetRandomFileName();
            var tempDir = Path.Combine(Path.GetTempPath(), "korebuild", instanceName);
            var app = new TestApp(ScriptsDir, ToolsSource, srcDir, tempDir, Path.Combine(LogDir, "test-" + instanceName + ".binlog"));
            _disposables.Enqueue(app);
            return app;
        }

        public void Dispose()
        {
            while (_disposables.Count > 0)
            {
                if (_disposables.TryDequeue(out var disposable))
                {
                    disposable.Dispose();
                }
            }
        }
    }

    [CollectionDefinition(nameof(RepoTestCollection))]
    public class RepoTestCollection : ICollectionFixture<RepoTestFixture>
    {
    }
}
