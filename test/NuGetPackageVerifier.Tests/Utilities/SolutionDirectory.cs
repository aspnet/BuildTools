// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGetPackageVerifier.Utilities
{
    internal static class SolutionDirectory
    {
        public static string GetSolutionRootDirectory()
        {
            const string SolutionFileName = "BuildTools.sln";
            var applicationBasePath = AppContext.BaseDirectory;
            var directoryInfo = new DirectoryInfo(applicationBasePath);

            do
            {
                var projectFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, SolutionFileName));
                if (projectFileInfo.Exists)
                {
                    return projectFileInfo.DirectoryName;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            throw new FileNotFoundException(
                $"Solution file {SolutionFileName} could not be found in {applicationBasePath} or its parent directories.",
                SolutionFileName);
        }
    }
}
