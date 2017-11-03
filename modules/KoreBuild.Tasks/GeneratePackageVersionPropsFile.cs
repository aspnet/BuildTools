// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KoreBuild.Tasks.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace KoreBuild.Tasks
{
    public class GeneratePackageVersionPropsFile : Task
    {
        [Required]
        public ITaskItem[] Packages { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public bool AddOverrideImport { get; set; }

        public bool SuppressVariableLabels { get; set; }

        public override bool Execute()
        {
            OutputPath = OutputPath.Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            DependencyVersionsFile depsFile;
            if (File.Exists(OutputPath))
            {
                if (!DependencyVersionsFile.TryLoad(OutputPath, Log, out depsFile))
                {
                    depsFile = DependencyVersionsFile.Create(AddOverrideImport);
                    Log.LogWarning($"Could not load the existing deps file from {OutputPath}. This file will be overwritten.");
                }
            }
            else
            {
                depsFile = DependencyVersionsFile.Create(AddOverrideImport);
            }

            var varNames = new HashSet<string>();
            foreach (var pkg in Packages)
            {
                var packageVersion = pkg.GetMetadata("Version");

                if (string.IsNullOrEmpty(packageVersion))
                {
                    Log.LogError("Package {0} is missing the Version metadata", pkg.ItemSpec);
                    continue;
                }

                string packageVarName;
                if (!string.IsNullOrEmpty(pkg.GetMetadata("VariableName")))
                {
                    packageVarName = pkg.GetMetadata("VariableName");
                    if (!packageVarName.EndsWith("Version", StringComparison.Ordinal))
                    {
                        Log.LogError("VariableName for {0} must end in 'Version'", pkg.ItemSpec);
                        continue;
                    }
                }
                else
                {
                    packageVarName = DependencyVersionsFile.GetVariableName(pkg.ItemSpec);
                }

                if (varNames.Contains(packageVarName))
                {
                    Log.LogError("Multiple packages would produce {0} in the generated dependencies.props file. Set VariableName to differentiate the packages manually", packageVarName);
                    continue;
                }

                var item = depsFile.Set(packageVarName, packageVersion);
                if (!SuppressVariableLabels)
                {
                    item.Label = pkg.ItemSpec;
                }
            }

            depsFile.Save(OutputPath);
            Log.LogMessage(MessageImportance.Normal, $"Generated {OutputPath}");
            return !Log.HasLoggedErrors;
        }
    }
}
