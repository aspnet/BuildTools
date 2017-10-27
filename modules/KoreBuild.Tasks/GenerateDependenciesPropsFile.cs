// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using KoreBuild.Tasks.ProjectModel;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace KoreBuild.Tasks
{
    /// <summary>
    /// Generates the build/dependencies.props file base on your current project,
    /// and updates all PackageReferences in <see cref="Projects"/>  and <see cref="OtherImports"/> to use MSBuild variables.
    /// </summary>
    public class GenerateDependenciesPropsFile : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The projects to update
        /// </summary>
        [Required]
        public ITaskItem[] Projects { get; set; }

        /// <summary>
        /// other files to update that may have PackageReferences in them
        /// </summary>
        public ITaskItem[] OtherImports { get; set; }

        /// <summary>
        /// Location of the dependenices.props file
        /// </summary>
        [Required]
        public string DependenciesFile { get; set; }

        /// <summary>
        /// Additional properties to use when evaluating the projects
        /// </summary>
        public string[] Properties { get; set; }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public override bool Execute()
        {
            var unifiedPackageList = new Dictionary<string, PackageReferenceInfo>(StringComparer.OrdinalIgnoreCase);

            var projects = new ProjectInfoFactory(Log).CreateMany(Projects, Properties, false, _cts.Token);
            var packageRefs = projects.SelectMany(p => p.Frameworks).SelectMany(f => f.Dependencies);

            foreach (var packageRef in packageRefs)
            {
                if (packageRef.Value.NoWarn.Contains(KoreBuildErrors.Prefix + KoreBuildErrors.ConflictingPackageReferenceVersions))
                {
                    // Make it possible to suppress version conflicts while generating this file.
                    continue;
                }

                if (unifiedPackageList.TryGetValue(packageRef.Value.Id, out var other))
                {
                    if (other.Version != packageRef.Value.Version)
                    {
                        Log.LogKoreBuildError(KoreBuildErrors.ConflictingPackageReferenceVersions, $"Conflicting dependency versions for {packageRef.Value.Id}: {other.Project.FileName} references '{other.Version}' but {packageRef.Value.Project.FileName} references '{packageRef.Value.Version}'");
                    }
                }
                else
                {
                    unifiedPackageList.Add(packageRef.Value.Id, packageRef.Value);
                    Log.LogMessage(MessageImportance.Low, $"Found {packageRef.Value.Id} = {packageRef.Value.Version}");
                }
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            var items = unifiedPackageList.Values.Select(p => new TaskItem(p.Id, new Hashtable { ["Version"] = p.Version })).ToArray();

            var task = new GeneratePackageVersionPropsFile
            {
                AddOverrideImport = true,
                SuppressVariableLabels = true,
                Packages = items,
                BuildEngine = BuildEngine,
                HostObject = HostObject,
                OutputPath = DependenciesFile,
            };

            if (!task.Execute())
            {
                return false;
            }

            var otherImports = OtherImports != null
                ? OtherImports.Select(p => p.ItemSpec)
                : Array.Empty<string>();

            foreach (var proj in projects.Select(p => p.FullPath).Concat(otherImports))
            {
                var project = ProjectRootElement.Open(proj, ProjectCollection.GlobalProjectCollection, preserveFormatting: true);
                var changed = false;
                foreach (var item in project.Items.Where(i => i.ItemType == "PackageReference"))
                {
                    var noWarn = item.Metadata.FirstOrDefault(m => m.Name == "NoWarn");
                    if (noWarn != null && noWarn.Value.Contains(KoreBuildErrors.Prefix + KoreBuildErrors.ConflictingPackageReferenceVersions))
                    {
                        continue;
                    }

                    var versionMetadata = item.Metadata.LastOrDefault(p => p.Name == "Version");
                    if (versionMetadata != null && versionMetadata.Value.StartsWith("$("))
                    {
                        continue;
                    }
                    changed = true;

                    var varName = $"$({GeneratePackageVersionPropsFile.GetVariableName(item.Include)})";
                    if (versionMetadata == null)
                    {
                        item.AddMetadata("Version", varName, expressAsAttribute: true);
                    }
                    else
                    {
                        versionMetadata.Value = varName;
                    }
                }

                if (changed)
                {
                    Log.LogMessage(MessageImportance.High, $"Updated {proj}");
                    project.Save(proj);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Normal, $"Skipping {proj}. Already up to date.");
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
