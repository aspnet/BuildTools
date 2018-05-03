// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetPackageVerifier.Manifests;
using Xunit.Abstractions;

namespace NuGetPackageVerifier.Utilities
{
    internal class TestPackageAnalysisContext : PackageAnalysisContext
    {
        public const string PackageId = "TestPackage";
        private readonly DisposableDirectory _disposableDirectory;

        private TestPackageAnalysisContext(DisposableDirectory disposableDirectory)
        {
            _disposableDirectory = disposableDirectory;
        }

        public static PackageAnalysisContext CreateContext(
            ITestOutputHelper output,
            string[] emptyFiles,
            string version = "1.0.0",
            string signRequest = null)
        {
            var disposableDirectory = new DisposableDirectory();
            var basePath = disposableDirectory.Path;
            var nupkgFileName = $"{PackageId}.{version}.nupkg";
            var nupkgPath = Path.Combine(basePath, nupkgFileName);

            var builder = new PackageBuilder();

            builder.Populate(new ManifestMetadata
            {
                Id = PackageId,
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

            PackageSignRequest packageSignRequest = null;

            if (signRequest != null)
            {
                var reader = new StringReader(signRequest);
                var signManifest = SignRequestManifest.Parse(reader, basePath);
                packageSignRequest = signManifest.PackageSignRequests[nupkgPath];
            }

            var context = new TestPackageAnalysisContext(disposableDirectory)
            {
                Logger = new TestLogger(output),
                PackageFileInfo = new FileInfo(nupkgPath),
                SignRequest = packageSignRequest,
                Metadata = builder,
            };

            return context;
        }

        public override void Dispose()
        {
            base.Dispose();
            _disposableDirectory.Dispose();
        }
    }
}
