// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Linq;

namespace KoreBuild.Tasks
{
    public class UpgradeExternalDependencies : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string UpdateSource { get; set; }

        /// <summary>
        /// The dependencies.props file to update
        /// </summary>
        [Required]
        public string DependenciesFile { get; set; }

        public override bool Execute()
        {
            var latest = GetLatestDeps();

            return UpdateDependencyFile(latest);
        }

        private IEnumerable<ProjectPropertyElement> GetLatestDeps()
        {
            return ProjectRootElement.Open(UpdateSource).PropertyGroups.SelectMany(p => p.Properties);
        }

        private bool UpdateDependencyFile(IEnumerable<ProjectPropertyElement> latest)
        {
            if (!DependencyVersionsFile.TryLoad(DependenciesFile, out var localVersionsFile))
            {
                Log.LogError($"Could not load file from {DependenciesFile}");
                return false;
            }

            var updateCount = 0;

            foreach (var property in latest)
            {
                if (!localVersionsFile.VersionVariables.ContainsKey(property.Name))
                {
                    Log.LogWarning($"Creating variable {property.Name}");
                }

                if(localVersionsFile.VersionVariables[property.Name] != property.Value)
                {
                    Log.LogWarning($"Setting '{property.Name}' to '{property.Value}'");
                    localVersionsFile.Set(property.Name, property.Value);
                    updateCount++;
                }
            }

            if (updateCount > 0)
            {
                localVersionsFile.Save(DependenciesFile);
            }
            else
            {
                Log.LogMessage($"Versions in {DependenciesFile} are already up to date");
            }

            return true;
        }
    }
}
