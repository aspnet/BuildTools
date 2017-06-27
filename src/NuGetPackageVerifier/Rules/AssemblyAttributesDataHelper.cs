// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyAttributesDataHelper
    {
        public static void SetAssemblyAttributesData(PackageAnalysisContext context)
        {
            if (context.AssemblyData.Any())
            {
                return;
            }

            foreach (var currentFile in context.PackageReader.GetFiles())
            {
                var extension = Path.GetExtension(currentFile);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string assemblyPath;
                    do
                    {
                        assemblyPath = Path.ChangeExtension(
                        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), extension);
                    } while (File.Exists(assemblyPath));

                    try
                    {
                        using (var packageFileStream = context.PackageReader.GetStream(currentFile))
                        using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                        {
                            packageFileStream.CopyTo(fileStream);
                        }

                        if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                        {
                            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
                            {
                                var asmAttrs = assembly.CustomAttributes;
                                context.AssemblyData.Add(currentFile, new AssemblyAttributesData
                                {
                                    AssemblyName = assembly.Name,
                                    AssemblyAttributes = asmAttrs,
                                });
                            }
                        }
                    }
                    finally
                    {
                        if (File.Exists(assemblyPath))
                        {
                            File.Delete(assemblyPath);
                        }
                    }
                }
            }
        }
    }
}
