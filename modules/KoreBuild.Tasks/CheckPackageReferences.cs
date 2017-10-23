// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using KoreBuild.Tasks.ProjectModel;
using KoreBuild.Tasks.Utilities;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using NuGet.Versioning;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Ensures MSBuild files use PackageReference responsibly. 
    /// </summary>
    public class CheckPackageReferences : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The solutions of csproj files to check.
        /// </summary>
        [Required]
        public ITaskItem[] Projects { get; set; }

        /// <summary>
        /// The file that contains the PropertyGroup of versions
        /// </summary>
        [Required]
        public string DependenciesFile { get; set; }

        public string[] Properties { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(DependenciesFile))
            {
                Log.LogError($"Expected the dependencies file to exist at {DependenciesFile}");
                return false;
            }

            if (!DependencyVersionsFile.TryLoad(DependenciesFile, Log, out var depsFile))
            {
                return false;
            }

            if (!depsFile.HasVersionsPropertyGroup())
            {
                Log.LogKoreBuildWarning(KoreBuildErrors.PackageRefPropertyGroupNotFound, $"No PropertyGroup with Label=\"{DependencyVersionsFile.PackageVersionsLabel}\" could be found in {DependenciesFile}");
            }

            foreach (var proj in Projects)
            {
                var ext = Path.GetExtension(proj.ItemSpec);
                if (ext == ".sln")
                {
                    var solutionProps = MSBuildListSplitter.GetNamedProperties(Properties);
                    var projectFiles = Projects.SelectMany(p => SolutionInfoFactory.GetProjects(p, solutionProps)).Distinct();
                    foreach (var project in projectFiles)
                    {
                        VerifyPackageReferences(project, depsFile.VersionVariables);
                    }
                }
                else
                {
                    VerifyPackageReferences(proj.ItemSpec, depsFile.VersionVariables);
                }
            }

            return !Log.HasLoggedErrors;
        }

        private void VerifyPackageReferences(string filePath, IReadOnlyDictionary<string, string> versionVariables)
        {
            ProjectRootElement doc;
            try
            {
                doc = ProjectRootElement.Open(filePath);
            }
            catch (InvalidProjectFileException ex)
            {
                Log.LogError(null, null, null, filePath, 0, 0, 0, 0, message: "Invalid project file: " + ex.Message);
                return;
            }

            var packageReferences = doc.Items.Where(i => i.ItemType == "PackageReference");
            foreach (var pkgRef in packageReferences)
            {
                var id = pkgRef.Include;
                var versionMetadata = pkgRef.Metadata.LastOrDefault(m => m.Name == "Version");
                var versionRaw = versionMetadata?.Value;
                if (versionMetadata == null || string.IsNullOrEmpty(versionRaw))
                {
                    Log.LogKoreBuildError(pkgRef.Location.File, pkgRef.Location.Line, KoreBuildErrors.PackageReferenceDoesNotHaveVersion, $"PackageReference to {id} does not define a Version");
                    continue;
                }

                var versionIsVariable =
                    versionRaw != null
                    && versionRaw.Length > 3
                    && versionRaw[0] == '$'
                    && versionRaw[1] == '('
                    && versionRaw[versionRaw.Length - 1] == ')'
                    && versionRaw.IndexOf(')') == versionRaw.Length - 1;

                if (!versionIsVariable)
                {
                    Log.LogKoreBuildError(pkgRef.Location.File, pkgRef.Location.Line, KoreBuildErrors.PackageRefHasLiteralVersion, "PackageReference must use an MSBuild variable to set the version.");
                    continue;
                }

                var versionVarName = versionRaw.Substring(2, versionRaw.Length - 3);

                if (!versionVariables.TryGetValue(versionVarName, out var versionValue))
                {
                    Log.LogKoreBuildError(pkgRef.Location.File, pkgRef.Location.Line, KoreBuildErrors.VariableNotFoundInDependenciesPropsFile, $"The variable {versionRaw} could not be found in {DependenciesFile}");
                    continue;
                }

                var nugetVersion = VersionRange.Parse(versionValue);
                if (nugetVersion.IsFloating)
                {
                    Log.LogKoreBuildError(pkgRef.Location.File, pkgRef.Location.Line, KoreBuildErrors.PackageRefHasFloatingVersion, $"PackageReference to {id} uses a floating version: '{versionValue}'");
                }
            }
        }
    }
}
