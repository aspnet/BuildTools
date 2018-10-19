// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit.Abstractions;

namespace NuGetPackageVerifier.Tests.Utilities
{
    public class TestHelper
    {
        public static PackageAnalysisContext CreateAnalysisContext(ITestOutputHelper output, string[] emptyFiles, string version = "1.0.0")
        {
            const string packageId = "TestPackage";
            var basePath = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            var nupkgFileName = $"{packageId}.{version}.nupkg";
            var nupkgPath = Path.Combine(basePath, nupkgFileName);

            Directory.CreateDirectory(basePath);


            var builder = new PackageBuilder();

            builder.Populate(new ManifestMetadata
            {
                Id = packageId,
                Version = new NuGetVersion(version),
                Authors = new[] { "Test" },
                Description = "Test",
            });

            using (var nupkg = File.Create(nupkgPath))
            {

                foreach (var dest in emptyFiles)
                {
                    var fileName = Path.GetFileName(dest);
                    File.WriteAllText(Path.Combine(basePath, fileName), "");
                    builder.AddFiles(basePath, fileName, dest);
                }

                builder.Save(nupkg);
            }

            var context = new TempPackageAnalysisContext(basePath)
            {
                Logger = new TestLogger(output),
                PackageFileInfo = new FileInfo(nupkgPath),
                Metadata = builder,
            };

            return context;
        }

        private class TempPackageAnalysisContext : PackageAnalysisContext
        {
            private string _tempPath;

            public TempPackageAnalysisContext(string tempPath)
            {
                this._tempPath = tempPath;
            }

            public override void Dispose()
            {
                base.Dispose();
                if (Directory.Exists(_tempPath))
                {
                    Directory.Delete(_tempPath, recursive: true);
                }
            }
        }
    }
}
