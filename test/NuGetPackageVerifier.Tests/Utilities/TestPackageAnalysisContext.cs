// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.AspNetCore.BuildTools.CodeSign;
using NuGet.Packaging;
using NuGet.Versioning;
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
            return Create(output,
                new ManifestMetadata
                {
                    Id = PackageId,
                    Version = new NuGetVersion(version),
                },
                emptyFiles,
                signRequest);
        }

        public static PackageAnalysisContext Create(
            ITestOutputHelper output,
            ManifestMetadata metadata,
            string[] emptyFiles = null,
            string signRequest = null)
        {
            var disposableDirectory = new DisposableDirectory();
            var basePath = disposableDirectory.Path;
            var nupkgFileName = $"{PackageId}.{metadata.Version}.nupkg";
            var nupkgPath = Path.Combine(basePath, nupkgFileName);

            // set required metadata
            metadata.Id = metadata.Id ?? "Test";
            metadata.Version = metadata.Version ?? new NuGetVersion("1.0.0");
            metadata.Authors = metadata.Authors.Any() ? metadata.Authors : new[] { "test" };
            metadata.Description = metadata.Description ?? "Description";

            // prevent PackageException for packages with no dependencies or content
            emptyFiles = emptyFiles ?? new[] { "_._" };

            var builder = new PackageBuilder();

            builder.Populate(metadata);

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

            SignRequestItem packageSignRequest = null;

            if (signRequest != null)
            {
                var reader = new StringReader(signRequest);
                var signManifest = SignRequestManifestXmlReader.Load(reader, basePath);
                packageSignRequest = signManifest.First(f => f.Path == nupkgFileName);
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
