// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.CommandLineUtils;

namespace NuGetPackageVerifier.Rules
{
    public class PackageSigningRule : IPackageVerifierRule
    {
        private readonly string _nuGetExePath;

        public PackageSigningRule()
            : this(GetKorebuildNuGetPath())
        {
        }

        public PackageSigningRule(string exePath)
        {
            if (!File.Exists(exePath))
            {
                throw new ArgumentException($"NuGet.exe could not be located at {exePath}", nameof(exePath));
            }

            _nuGetExePath = exePath;
        }

        private static string GetKorebuildNuGetPath()
        {
            var searchPaths = new[]
            {
                // KoreBuild/nuget.exe
                // KoreBuild/modules/NuGetPackageVerifier/
                Path.Combine(AppContext.BaseDirectory, "..", "..", "nuget.exe"),

                // obj/nuget.exe in local build root
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "obj", "nuget.exe"),
            };

            return searchPaths.First(File.Exists);
        }

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new InvalidOperationException("Package sign verification is only supported on Windows machines");
            }

            var args = new[]
            {
                "verify",
                "-NonInteractive",
                "-All",
                context.PackageFileInfo.FullName,
            };

            var psi = new ProcessStartInfo
            {
                FileName = _nuGetExePath,
                Arguments = ArgumentEscaper.EscapeAndConcatenate(args),
                RedirectStandardOutput = true,
            };

            var process = Process.Start(psi);
            process.WaitForExit(60 * 1000);

            if (process.ExitCode != 0)
            {
                var issueText = process.StandardOutput.ReadToEnd();
                yield return PackageIssueFactory.PackageSignVerificationFailed(context.Metadata.Id, issueText);
            }
        }
    }
}
