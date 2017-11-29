// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Runtime.InteropServices;
using KoreBuild.Tasks.SkipStrongNames;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Win32;

namespace KoreBuild.Tasks
{
    public class EnableSkipStrongName : Task
    {
        [Required]
        public string XmlFile { get; set; }

        public override bool Execute()
        {
            var configuration = new StrongNameConfiguration(AssembliesFile.Read(XmlFile));

            return SkipEnable(configuration);
        }

        private bool SkipEnable(StrongNameConfiguration configuration)
        {
            if (!File.Exists(XmlFile))
            {
                Log.LogError("The XmlFile given must exist.");
                return false;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.LogError("Strongname tasks should only be run on Windows.");
                return false;
            }

            bool printedHeader = false;

            foreach (RegistrySection section in WindowsRegistry.Sections)
            {
                using (RegistryKey registryKey = WindowsRegistry.CreateWritableVerificationRegistryKey(section))
                {
                    if (registryKey == null)
                    {
                        Log.LogError($"Unable to open writable verification registry key for {section}.");
                        return false;
                    }

                    foreach (var assembly in configuration.FilteredAssemblySpecifications[section])
                    {
                        using (RegistryKey subKey = registryKey.OpenSubKey(assembly))
                        {
                            if (subKey == null)
                            {
                                if (!printedHeader)
                                {
                                    printedHeader = true;
                                    Log.LogMessage("Adding registry entries:");
                                }

                                Log.LogMessage($"  {registryKey}\\{assembly}");
                                registryKey.CreateSubKey(assembly);
                            }
                        }
                    }
                }
            }

            if (!printedHeader)
            {
                Log.LogMessage("Skip Strong Names is already enabled.");
            }
            else
            {
                Log.LogMessage("");
                Log.LogMessage("Skip Strong Names was successfully enabled.");
            }

            return true;
        }
    }
}
