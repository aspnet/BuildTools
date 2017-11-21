// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Generic;

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

        private IReadOnlyDictionary<string, string> GetLatestDeps()
        {
            return DependencyVersionsFile.Load(UpdateSource).VersionVariables;
        }

        private bool UpdateDependencyFile(IReadOnlyDictionary<string, string> latest)
        {
            if (!DependencyVersionsFile.TryLoad(DependenciesFile, out var localVersionsFile))
            {
                Log.LogError($"Could not load file from {DependenciesFile}");
                return false;
            }

            foreach (var sourceVariable in latest)
            {
                if (!localVersionsFile.VersionVariables.ContainsKey(sourceVariable.Key))
                {
                    Log.LogWarning($"Creating variable {sourceVariable.Key}");
                }
                Log.LogWarning($"Setting '{sourceVariable.Key}' to '{sourceVariable.Value}'",);
                localVersionsFile.Set(sourceVariable.Key, sourceVariable.Value);
            }

            localVersionsFile.Save(DependenciesFile);

            return true;
        }
    }
}
