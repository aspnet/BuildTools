// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace KoreBuild.Tasks.SkipStrongNames
{
    internal static class WindowsRegistry
    {
        private const string SoftwareNativeKeyName = "SOFTWARE";
        private const string SoftwareWowKeyName = @"SOFTWARE\Wow6432Node";
        private const string VerificationSubKeyName = @"Microsoft\StrongName\Verification";

        private static readonly Lazy<bool> softwareWowExists = new Lazy<bool>(() => CheckRegistryKeyExists(SoftwareWowKeyName));

        public static IEnumerable<RegistrySection> Sections
        {
            get
            {
                yield return RegistrySection.Native;

                if (softwareWowExists.Value)
                {
                    yield return RegistrySection.Windows32OnWindows64;
                }
            }
        }

        public static RegistryKey CreateWritableVerificationRegistryKey(RegistrySection section)
        {
            return CreateWriteableRegistryKey(GetBaseKeyName(section), VerificationSubKeyName);
        }

        public static HashSet<string> LoadStrongNameExclusions(RegistrySection section)
        {
            HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (RegistryKey key = OpenReadOnlyVerificationRegistryKey(section))
            {
                if (key != null)
                {
                    foreach (var subKey in key.GetSubKeyNames())
                    {
                        results.Add(subKey);
                    }
                }
            }

            return results;
        }

        private static bool CheckRegistryKeyExists(string name)
        {
            using (RegistryKey key = OpenReadOnlyRegistryKey(name))
            {
                return key != null;
            }
        }

        private static RegistryKey CreateWriteableRegistryKey(string baseKeyName, string subKeyName)
        {
            List<RegistryKey> keysToDispose = new List<RegistryKey>();

            try
            {
                RegistryKey currentKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKeyName, writable: true);

                if (currentKey == null)
                {
                    return null;
                }

                string[] subKeyParts = subKeyName.Split('\\');

                foreach (string subKeyPart in subKeyParts)
                {
                    keysToDispose.Add(currentKey);
                    currentKey = currentKey.CreateSubKey(subKeyPart);
                }

                return currentKey;
            }
            finally
            {
                foreach (RegistryKey key in keysToDispose)
                {
                    key.Dispose();
                }
            }
        }

        private static string GetBaseKeyName(RegistrySection section)
        {
            switch (section)
            {
                case RegistrySection.Windows32OnWindows64:
                    return SoftwareWowKeyName;
                case RegistrySection.Native:
                default:
                    return SoftwareNativeKeyName;
            }
        }

        private static RegistryKey OpenReadOnlyRegistryKey(string name)
        {
            return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(name);
        }

        private static RegistryKey OpenReadOnlyVerificationRegistryKey(RegistrySection section)
        {
            string keyName = GetBaseKeyName(section) + '\\' + VerificationSubKeyName;
            return OpenReadOnlyRegistryKey(keyName);
        }
    }
}
