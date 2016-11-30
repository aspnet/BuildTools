// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.Loader;

namespace NuGetPackageVerifier
{
    public class AssemblyHelpers
    {
        public static bool IsAssemblyManaged(string assemblyPath)
        {
            // From http://msdn.microsoft.com/en-us/library/ms173100.aspx
            try
            {
                var testAssembly = AssemblyLoadContext.GetAssemblyName(assemblyPath);
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