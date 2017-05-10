// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Mono.Cecil;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasAttributeHelper
    {
        public static void GetAssemblyAttributesData(PackageAnalysisContext context)
        {
            if (context.AssemblyData.Count == 0)
            {
                foreach (var currentFile in context.PackageReader.GetFiles())
                {
                    var extension = Path.GetExtension(currentFile);
                    if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var assemblyPath = Path.ChangeExtension(
                            Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

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
                                        Assembly = assembly,
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
}
