// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace KoreBuild.Tasks.SkipStrongNames
{
    internal class StrongNameConfiguration
    {
        private readonly Dictionary<RegistrySection, HashSet<string>> _strongNameExclusions;
        private readonly AssemblySpecification[] _assemblies;

        public StrongNameConfiguration(AssemblySpecification[] specifications)
        {
            if (specifications != null)
            {
                _assemblies = specifications.OrderBy(s => s.Name).ToArray();
            }
            else
            {
                _assemblies = null;
            }

            FilteredAssemblySpecifications = new Dictionary<RegistrySection, string[]>();
            _strongNameExclusions = new Dictionary<RegistrySection, HashSet<string>>();

            foreach (RegistrySection section in WindowsRegistry.Sections)
            {
                HashSet<string> sectionExclusions = WindowsRegistry.LoadStrongNameExclusions(section);
                string[] sectionFilteredAssemblies = FilterAssemblySpecifications(_assemblies, sectionExclusions);

                FilteredAssemblySpecifications.Add(section, sectionFilteredAssemblies);
                _strongNameExclusions.Add(section, sectionExclusions);
            }
        }

        public Dictionary<RegistrySection, string[]> FilteredAssemblySpecifications { get; private set; }

        public Status Status
        {
            get
            {
                bool allFound = true;
                bool anyFound = false;

                foreach (RegistrySection section in WindowsRegistry.Sections)
                {
                    string[] sectionFilteredAssemblies = FilteredAssemblySpecifications[section];
                    HashSet<string> sectionExclusions = _strongNameExclusions[section];

                    foreach (string assemblySpecification in sectionFilteredAssemblies)
                    {
                        bool found = sectionExclusions.Contains(assemblySpecification);
                        allFound = allFound && found;
                        anyFound = anyFound || found;

                        if (!allFound && anyFound)
                        {
                            break;
                        }
                    }
                }

                if (allFound)
                {
                    return Status.Enabled;
                }
                else if (anyFound)
                {
                    return Status.PartiallyEnabled;
                }
                else
                {
                    return Status.Disabled;
                }
            }
        }

        private string[] FilterAssemblySpecifications(AssemblySpecification[] assemblies, HashSet<string> exclusions)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                return new string[0];
            }

            // Determine whether strong name verification is disabled globally (with a "*,*" entry).
            if (exclusions.Contains("*,*"))
            {
                return new string[0];
            }

            // Get a list of the public key tokens that are globally excluded (with a "*,PublicKeyToken" entry)
            HashSet<string> globallyExcludedPublicKeyTokens = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var publicKeyToken in assemblies.Select(a => a.PublicKeyToken.ToUpperInvariant()).Distinct())
            {
                if (exclusions.Contains("*," + publicKeyToken))
                {
                    globallyExcludedPublicKeyTokens.Add(publicKeyToken);
                }
            }

            // Create specifications for the list of the assemblies not covered by a global exclusion
            return (from element in assemblies
                    where !globallyExcludedPublicKeyTokens.Contains(element.PublicKeyToken)
                    select element.ToString()).ToArray();
        }
    }
}
