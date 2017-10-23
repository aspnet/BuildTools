// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
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

            var projectRoot = ProjectRootElement.Create(NewProjectFileOptions.None);

            projectRoot.AddPropertyGroup().AddProperty("MSBuildAllProjects", "$(MSBuildAllProjects);$(MSBuildThisFileFullPath)");

            var packageVersions = projectRoot.AddPropertyGroup();
            packageVersions.Label = "Package Versions";

            var varNames = new SortedDictionary<string, ProjectPropertyElement>();
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
                    packageVarName = GetVariableName(pkg.ItemSpec);
                }

                if (varNames.ContainsKey(packageVarName))
                {
                    Log.LogError("Multiple packages would produce {0} in the generated dependencies.props file. Set VariableName to differentiate the packages manually", packageVarName);
                    continue;
                }

                var elem = projectRoot.CreatePropertyElement(packageVarName);
                elem.Value = packageVersion;
                if (!SuppressVariableLabels)
                {
                    elem.Label = pkg.ItemSpec;
                }
                varNames.Add(packageVarName, elem);
            }

            foreach (var item in varNames)
            {
                packageVersions.AppendChild(item.Value);
            }

            if (AddOverrideImport)
            {
                var import = projectRoot.AddImport("$(DotNetPackageVersionPropsPath)");
                import.Condition = " '$(DotNetPackageVersionPropsPath)' != '' ";
            }

            projectRoot.Save(OutputPath, Encoding.UTF8);
            Log.LogMessage(MessageImportance.Normal, $"Generated {OutputPath}");
            return !Log.HasLoggedErrors;
        }

        public static string GetVariableName(string packageId)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var ch in packageId)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    first = true;
                    continue;
                }

                if (first)
                {
                    first = false;
                    sb.Append(char.ToUpperInvariant(ch));
                }
                else
                {
                    sb.Append(ch);
                }
            }
            sb.Append("PackageVersion");
            return sb.ToString();
        }
    }
}
