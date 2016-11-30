// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Mono.Cecil;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyStrongNameRule : IPackageVerifierRule
    {
        private static string _publicKeyToken = "ADB9793829DDAE60";

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            using (var reader = new PackageArchiveReader(context.PackageFileInfo.FullName))
            {
                foreach (var currentFile in reader.GetFiles())
                {
                    var extension = Path.GetExtension(currentFile);
                    if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var assemblyPath = Path.ChangeExtension(
                            Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

                        var isManagedCode = false;
                        var isStrongNameSigned = false;
                        var hasCorrectPublicKeyToken = false;
                        var hresult = 0;

                        try
                        {
                            using (var packageFileStream = reader.GetStream(currentFile))
                            using (var fileStream = File.OpenWrite(assemblyPath))
                            {
                                packageFileStream.CopyTo(fileStream);
                            }

                            if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                            {
                                isManagedCode = true;
                                using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
                                {
                                    if (assembly.Modules.Any())
                                    {
                                        isStrongNameSigned = assembly.Modules.All(
                                            module => module.Attributes.HasFlag(ModuleAttributes.StrongNameSigned));
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("The managed assembly does not contain any modules.");
                                    }

                                    var tokenHexString = BitConverter.ToString(assembly.Name.PublicKeyToken).Replace("-", "");
                                    if (_publicKeyToken.Equals(tokenHexString, StringComparison.OrdinalIgnoreCase))
                                    {
                                        hasCorrectPublicKeyToken = true;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Logger.LogError(
                                "Error while verifying strong name signature for {0}: {1}", currentFile, ex.Message);
                        }
                        finally
                        {
                            if (File.Exists(assemblyPath))
                            {
                                File.Delete(assemblyPath);
                            }
                        }

                        if (isManagedCode && !isStrongNameSigned)
                        {
                            yield return PackageIssueFactory.AssemblyNotStrongNameSigned(currentFile, hresult);
                        }

                        if (isManagedCode && !hasCorrectPublicKeyToken)
                        {
                            yield return PackageIssueFactory.AssemblyHasWrongPublicKeyToken(currentFile, _publicKeyToken);
                        }
                    }
                }
            }
            yield break;
        }
    }
}