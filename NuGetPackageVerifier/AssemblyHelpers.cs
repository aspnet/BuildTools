using System;
using System.IO;
using System.Reflection;

namespace NuGetPackageVerifier
{
    public class AssemblyHelpers
    {
        public static bool IsAssemblyManaged(string assemblyPath)
        {
            // From http://msdn.microsoft.com/en-us/library/ms173100.aspx
            try
            {
                var testAssembly = AssemblyName.GetAssemblyName(assemblyPath);
                return true;
            }
            catch (FileNotFoundException)
            {
                // The file cannot be found
            }
            catch (BadImageFormatException)
            {
                // The file is not an assembly
            }
            catch (FileLoadException)
            {
                // The assembly has already been loaded
            }
            return false;
        }
    }
}
