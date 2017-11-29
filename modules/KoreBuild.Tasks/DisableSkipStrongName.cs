// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using KoreBuild.Tasks.SkipStrongNames;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Win32;

namespace KoreBuild.Tasks
{
    public class DisableSkipStrongName : Task
    {
        [Required]
        public string XmlFile { get; set; }

        public override bool Execute()
        {
            var configuration = new StrongNameConfiguration(AssembliesFile.Read(XmlFile));

            return SkipDisable(configuration);
        }

        private bool SkipDisable(StrongNameConfiguration configuration)
        {
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
                        RegistryKey subKey = registryKey.OpenSubKey(assembly);

                        if (subKey != null)
                        {
                            if (!printedHeader)
                            {
                                printedHeader = true;
                                Log.LogMessage("Deleting registry entries:");
                            }

                            Log.LogMessage($"  {registryKey}\\{assembly}");
                            subKey.Dispose();
                            registryKey.DeleteSubKeyTree(assembly);
                        }
                    }
                }
            }

            if (!printedHeader)
            {
                Log.LogMessage("Skip Strong Names is already disabled.");
            }
            else
            {
                Log.LogMessage("");
                Log.LogMessage("Skip Strong Names was successfully disabled.");
            }

            return true;
        }
    }
}
