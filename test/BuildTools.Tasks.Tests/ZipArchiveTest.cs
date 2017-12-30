// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace BuildTools.Tasks.Tests
{
    public class ZipArchiveTest : IDisposable
    {
        private readonly string _tempDir;

        public ZipArchiveTest()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void ZipsLinkItems()
        {
            var inputFile = Path.Combine(_tempDir, "..", Guid.NewGuid().ToString());
            var dest = Path.Combine(_tempDir, "test.zip");
            var linkItem = new TaskItem(inputFile);
            linkItem.SetMetadata("Link", "temp/temp/temp/file.txt");
            try
            {
                File.WriteAllText(inputFile, "");
                var task = new ZipArchive
                {
                    SourceFiles = new[] { linkItem },
                    WorkingDirectory = Path.Combine(_tempDir, "temp"),
                    File = dest,
                    BuildEngine = new MockEngine(),
                };
                Assert.True(task.Execute());

                using (var fileStream = new FileStream(dest, FileMode.Open))
                using (var zipStream = new ZipArchiveStream(fileStream))
                {
                    var entry = Assert.Single(zipStream.Entries);
                    Assert.Equal("temp/temp/temp/file.txt", entry.FullName);
                }
            }
            finally
            {
                File.Delete(inputFile);
            }
        }

        [Fact]
        public void CreatesZip()
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt",
                @"dir\c.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            Assert.False(File.Exists(dest));

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files).ToArray(),
                WorkingDirectory = _tempDir,
                File = dest,
                Overwrite = true,
                BuildEngine = new MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            using (var fileStream = new FileStream(dest, FileMode.Open))
            using (var zipStream = new ZipArchiveStream(fileStream))
            {
                Assert.Equal(files.Length, zipStream.Entries.Count);
                Assert.Collection(zipStream.Entries,
                    a => Assert.Equal("a.txt", a.FullName),
                    b => Assert.Equal("dir/b.txt", b.FullName),
                    c => Assert.Equal("dir/c.txt", c.FullName));
            }
        }

        [Fact]
        public void FailsIfFileExists()
        {
             var files = new[]
            {
                "test.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            File.WriteAllText(dest, "Original");

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files).ToArray(),
                WorkingDirectory = _tempDir,
                File = dest,
                Overwrite = false,
                BuildEngine = new MockEngine { ContinueOnError = true },
            };

            Assert.False(task.Execute(), "Task should fail");
            Assert.Equal("Original", File.ReadAllText(dest));
        }

        [Fact]
        public void OverwriteReplacesEntireZip()
        {
            var files1 = new[]
            {
                "a.txt",
                "dir/b.txt",
                @"dir\c.txt",
            };

            var files2 = new[]
            {
                "test.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            Assert.False(File.Exists(dest));

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files1).ToArray(),
                WorkingDirectory = _tempDir,
                File = dest,
                BuildEngine = new MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            task = new ZipArchive
            {
                SourceFiles = CreateItems(files2).ToArray(),
                WorkingDirectory = _tempDir,
                File = dest,
                Overwrite = true,
                BuildEngine = new MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            using (var fileStream = File.OpenRead(dest))
            using (var zipStream = new ZipArchiveStream(fileStream))
            {
                var entry = Assert.Single(zipStream.Entries);
                Assert.Equal("test.txt", entry.FullName);
            }
        }

        [Fact]
        public void FailsForEmptyFileName()
        {
            var inputFile = Path.Combine(_tempDir, "..", Guid.NewGuid().ToString());
            var dest = Path.Combine(_tempDir, "test.zip");
            var linkItem = new TaskItem(inputFile);
            linkItem.SetMetadata("Link", "temp/");
            try
            {
                File.WriteAllText(inputFile, "");
                var mock = new MockEngine { ContinueOnError = true };
                var task = new ZipArchive
                {
                    SourceFiles = new[] { linkItem },
                    WorkingDirectory = Path.Combine(_tempDir, "temp"),
                    File = dest,
                    BuildEngine = mock,
                };

                Assert.False(task.Execute(), "Task should fail");
                Assert.NotEmpty(mock.Errors);

                using (var fileStream = new FileStream(dest, FileMode.Open))
                using (var zipStream = new ZipArchiveStream(fileStream))
                {
                    Assert.Empty(zipStream.Entries);
                }
            }
            finally
            {
                File.Delete(inputFile);
            }
        }

        private IEnumerable<ITaskItem> CreateItems(string[] files)
        {
            foreach (var file in files)
            {
                var path = Path.Combine(_tempDir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path.Replace('\\', '/'), "");
                // intentionally allow item spec to contain \ and /
                // this tests that MSBuild normalizes before we create zip entries
                yield return new TaskItem(path);
            }
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
