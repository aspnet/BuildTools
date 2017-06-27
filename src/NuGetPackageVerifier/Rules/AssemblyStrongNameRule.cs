// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyStrongNameRule : IPackageVerifierRule
    {
        private static readonly string _publicKeyToken = "ADB9793829DDAE60";

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
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

                    var isManagedCode = false;
                    var isStrongNameSigned = false;
                    var hasCorrectPublicKeyToken = false;
                    var hresult = 0;

                    try
                    {
                        using (var packageFileStream = context.PackageReader.GetStream(currentFile))
                        using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
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
    }
}
