// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace KoreBuild.Tasks.ProjectModel
{
    internal class ProjectInfoFactory
    {
        private readonly TaskLoggingHelper _logger;

        public ProjectInfoFactory(TaskLoggingHelper logger)
        {
           _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<ProjectInfo> CreateMany(ITaskItem[] projectItems, string[] properties, bool policyDesignBuild, CancellationToken token)
        {
            var cts = new CancellationTokenSource();
            token.Register(() => cts.Cancel());
            var solutionProps = MSBuildListSplitter.GetNamedProperties(properties);
            var projectFiles = projectItems.SelectMany(p => SolutionInfoFactory.GetProjects(p, solutionProps)).Distinct();
            var projects = new ConcurrentBag<ProjectInfo>();
            var stop = Stopwatch.StartNew();

            Parallel.ForEach(projectFiles, projectFile =>
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    projects.Add(Create(projectFile, policyDesignBuild));
                }
                catch (Exception ex)
                {
                    _logger.LogErrorFromException(ex);
                    cts.Cancel();
                }
            });

            stop.Stop();
            _logger.LogMessage(MessageImportance.Low, $"Finished design-time build in {stop.ElapsedMilliseconds}ms");
            return projects.ToArray();
        }

        public ProjectInfo Create(string path, bool policyDesignBuild)
        {
            var project = GetProject(path, ProjectCollection.GlobalProjectCollection, policyDesignBuild);
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

        private static Project GetProject(string path, ProjectCollection projectCollection, bool policyDesignBuild)
        {
            var projects = projectCollection.GetLoadedProjects(path);
            foreach (var proj in projects)
            {
                if (proj.GetPropertyValue("DesignTimeBuild") == "true")
                {
                    return proj;
                }
            }
            var xml = ProjectRootElement.Open(path, projectCollection);
            var globalProps = new Dictionary<string, string>()
            {
                ["DesignTimeBuild"] = "true",
            };
            if (policyDesignBuild)
            {
                globalProps["PolicyDesignTimeBuild"] = "true";
            }
            var project = new Project(xml,
                globalProps,
                toolsVersion: "15.0",
                projectCollection: projectCollection)
            {
                IsBuildEnabled = false
            };
            return project;
        }

        private IReadOnlyDictionary<string, PackageReferenceInfo> GetDependencies(ProjectInstance project)
        {
            var references = new Dictionary<string, PackageReferenceInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in  project.GetItems("PackageReference"))
            {
                bool.TryParse(item.GetMetadataValue("IsImplicitlyDefined"), out var isImplicit);
                var noWarn = item.GetMetadataValue("NoWarn");
                IReadOnlyList<string> noWarnItems = string.IsNullOrEmpty(noWarn)
                    ? Array.Empty<string>()
                    : noWarn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                var info = new PackageReferenceInfo(item.EvaluatedInclude, item.GetMetadataValue("Version"), isImplicit, noWarnItems);

                if (references.ContainsKey(info.Id))
                {
                    _logger.LogKoreBuildWarning(project.ProjectFileLocation.File, KoreBuildErrors.DuplicatePackageReference, $"Found a duplicate PackageReference for {info.Id}. Restore results may be unpredictable.");
                }

                references[info.Id] = info;
            }

            return references;
        }

        private static IEnumerable<DotNetCliReferenceInfo> GetTools(ProjectInstance project)
        {
            return project.GetItems("DotNetCliToolReference").Select(item =>
                new DotNetCliReferenceInfo(item.EvaluatedInclude, item.GetMetadataValue("Version")));
        }
    }
}
