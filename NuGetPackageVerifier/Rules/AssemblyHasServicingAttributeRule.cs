using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasServicingAttributeRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(IPackageRepository packageRepo, IPackage package, IPackageVerifierLogger logger)
        {
            foreach (IPackageFile currentFile in package.GetFiles())
            {
                string extension = Path.GetExtension(currentFile.Path);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string assemblyPath = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);
                    try
                    {
                        using (Stream packageFileStream = currentFile.GetStream())
                        {
                            var _assemblyBytes = new byte[packageFileStream.Length];
                            packageFileStream.Read(_assemblyBytes, 0, _assemblyBytes.Length);

                            using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                            {
                                packageFileStream.Seek(0, SeekOrigin.Begin);
                                packageFileStream.CopyTo(fileStream);
                                fileStream.Flush(true);
                            }

                            if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                            {
                                var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);

                                var asmAttrs = assemblyDefinition.CustomAttributes;

                                if (!HasServicingAttribute(asmAttrs))
                                {
                                    yield return PackageIssueFactory.AssemblyMissingServicingAttribute(currentFile.Path);
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
            yield break;
        }

        private static bool HasServicingAttribute(Mono.Collections.Generic.Collection<CustomAttribute> asmAttrs)
        {
            var servicingAttributes = asmAttrs.Where(asmAttr => IsValidServicingAttribute(asmAttr));
            return servicingAttributes.Count() == 1;
        }

        private static bool IsValidServicingAttribute(CustomAttribute asmAttr)
        {
            if (asmAttr.AttributeType.FullName != typeof(AssemblyMetadataAttribute).FullName)
            {
                return false;
            }
            if (asmAttr.ConstructorArguments.Count != 2)
            {
                return false;
            }
            var keyValue = asmAttr.ConstructorArguments[0].Value as string;
            var valueValue = asmAttr.ConstructorArguments[1].Value as string;
            return (keyValue == "Serviceable") && (valueValue == "True");
        }
    }
}
