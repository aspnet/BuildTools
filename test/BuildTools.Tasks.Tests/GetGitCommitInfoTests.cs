// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using BuildTools.Tasks.Tests;
using Xunit;

namespace Microsoft.AspNetCore.BuildTools.Tests
{
    public class GetGitCommitInfoTests : IDisposable
    {
        private readonly string _tempDir;

        public GetGitCommitInfoTests()
        {
            _tempDir = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        private string CreateTestRepro(string name)
        {
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "Resources", name), _tempDir, overwriteFiles: true);
            return Path.Combine(_tempDir, Path.GetFileNameWithoutExtension(name));
        }

        [Fact]
        public void ItFindsCommitHashFromSparseCheckout()
        {
            var engine = new MockEngine();
            var task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = CreateTestRepro("SparseCheckout.zip"),
            };
            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("7896eb0373dac70940819ef6a6494fdeb4880391", task.CommitHash);
            Assert.Equal("dev", task.Branch);
        }

        [Fact]
        public void ItFindsCommitHash()
        {
            var engine = new MockEngine();
            var task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = CreateTestRepro("SimpleGitRepo.zip"),
            };
            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("9c03629e11679f5f3d9dbbd27286239588e0d296", task.CommitHash);
            Assert.Equal("master", task.Branch);
        }

        [Fact]
        public void ItFindsCommitHashInWorktree()
        {
            var root = CreateTestRepro("WorktreeRepo.zip");

            var engine = new MockEngine();
            var task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = Path.Combine(root, "SourceRoot"),
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("27a9c92f96a117ff926c12beb9d4ea8d0f127e42", task.CommitHash);
            Assert.Equal("master", task.Branch);

            engine = new MockEngine();
            task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = Path.Combine(root, "Worktree1"),
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("51bd19b3825fbc3e96fbdabc9f2cfa9972999bfa", task.CommitHash);
            Assert.Equal("worktree1", task.Branch);
        }

        [Fact]
        public void ItFindsCommitHashInSubmodule()
        {
            var root = CreateTestRepro("SubmoduleRepo.zip");

            var engine = new MockEngine();
            var task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = root,
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("91314ecce9e7d3cbd1d3f4d1f35664f52a301479", task.CommitHash);
            Assert.Equal("master", task.Branch);

            engine = new MockEngine();
            task = new GetGitCommitInfo
            {
                BuildEngine = engine,
                WorkingDirectory = Path.Combine(root, "modules", "submodule1"),
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("599e691c41f502ed9e062b1822ce13b673fc916e", task.CommitHash);
            Assert.Equal("dev", task.Branch);
        }
    }
}
