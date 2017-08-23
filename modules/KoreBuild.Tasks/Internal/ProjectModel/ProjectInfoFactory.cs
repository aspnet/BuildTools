// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Frameworks;
using KoreBuild.Tasks.Utilties;

namespace KoreBuild.Tasks.ProjectModel
{
    internal class ProjectInfoFactory
    {
        public static ProjectInfo Create(string path)
        {
            var xml = ProjectRootElement.Open(path, ProjectCollection.GlobalProjectCollection);
            var globalProps = new Dictionary<string, string>()
            {
                ["DesignTimeBuild"] = "true",
                ["PolicyDesignTimeBuild"] = "true",
            };

            var project = new Project(xml, globalProps, toolsVersion: null, projectCollection: ProjectCollection.GlobalProjectCollection)
            {
                IsBuildEnabled = false
            };
            var instance = project.CreateProjectInstance(ProjectInstanceSettings.ImmutableWithFastItemLookup);
            var projExtPath = instance.GetPropertyValue("MSBuildProjectExtensionsPath");

            var targetFrameworks = instance.GetPropertyValue("TargetFrameworks");
            var targetFramework = instance.GetPropertyValue("TargetFramework");

            var frameworks = new List<ProjectFrameworkInfo>();
            if (!string.IsNullOrEmpty(targetFrameworks) && string.IsNullOrEmpty(targetFramework))
            {
                // multi targeting
                foreach (var tfm in targetFrameworks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    project.SetGlobalProperty("TargetFramework", tfm);
                    var innerBuild = project.CreateProjectInstance(ProjectInstanceSettings.ImmutableWithFastItemLookup);

                    var tfmInfo = new ProjectFrameworkInfo(NuGetFramework.Parse(tfm), GetDependencies(innerBuild));

                    frameworks.Add(tfmInfo);
                }

                project.RemoveGlobalProperty("TargetFramework");
            }
            else if (!string.IsNullOrEmpty(targetFramework))
            {
                var tfmInfo = new ProjectFrameworkInfo(NuGetFramework.Parse(targetFramework), GetDependencies(instance));

                frameworks.Add(tfmInfo);
            }

            var projectDir = Path.GetDirectoryName(path);

            var tools = GetTools(instance).ToArray();

            return new ProjectInfo(path, projExtPath, frameworks, tools);
        }

        private static IEnumerable<PackageReferenceInfo> GetDependencies(ProjectInstance project)
        {
            return project.GetItems("PackageReference").Select(item =>
            {
                bool.TryParse(item.GetMetadataValue("IsImplicitlyDefined"), out var isImplicit);
                var noWarn = item.GetMetadataValue("NoWarn");
                IReadOnlyList<string> noWarnItems = string.IsNullOrEmpty(noWarn)
                    ? Array.Empty<string>()
                    : MSBuildListSplitter.SplitItemList(noWarn).ToArray();

                return new PackageReferenceInfo(item.EvaluatedInclude, item.GetMetadataValue("Version"), isImplicit, noWarnItems);
            });
        }

        private static IEnumerable<DotNetCliReferenceInfo> GetTools(ProjectInstance project)
        {
            return project.GetItems("DotNetCliToolReference").Select(item =>
                new DotNetCliReferenceInfo(item.EvaluatedInclude, item.GetMetadataValue("Version")));
        }
    }
}
