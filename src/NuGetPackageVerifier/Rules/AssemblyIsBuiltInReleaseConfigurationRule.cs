// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyIsBuiltInReleaseConfigurationRule : IPackageVerifierRule
    {
        // TODO: Revert to using Mono.Cecil when https://github.com/jbevain/cecil/issues/306 is fixed.
        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
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
                            using (var stream = File.OpenRead(assemblyPath))
                            using (var peReader = new PEReader(stream))
                            {
                                var reader = peReader.GetMetadataReader();
                                foreach (var handle in reader.CustomAttributes)
                                {
                                    var customAttribute = reader.GetCustomAttribute(handle);
                                    var ctor = reader.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor);
                                    var type = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                                    var typeName = reader.GetString(type.Name);
                                    if (string.Equals(typeof(DebuggableAttribute).Name, typeName, StringComparison.Ordinal))
                                    {
                                        var attribute = customAttribute.DecodeValue(NullProvider.Instance);

                                        var debuggingMode = (DebuggableAttribute.DebuggingModes)attribute.FixedArguments.Single().Value;
                                        if (debuggingMode.HasFlag(DebuggableAttribute.DebuggingModes.Default) ||
                                            debuggingMode.HasFlag(DebuggableAttribute.DebuggingModes.DisableOptimizations))
                                        {
                                            yield return PackageIssueFactory.AssemblyHasIncorrectBuildConfiguration(currentFile);
                                        };

                                        break;
                                    }
                                }
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

        private class NullProvider : ICustomAttributeTypeProvider<object>
        {
            public static NullProvider Instance => new NullProvider();

            public object GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
            public object GetSystemType() => null;
            public object GetSZArrayType(object elementType) => null;
            public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromSerializedName(string name) => null;
            public PrimitiveTypeCode GetUnderlyingEnumType(object type) => PrimitiveTypeCode.Int32;
            public bool IsSystemType(object type) => false;
        }
    }
}
