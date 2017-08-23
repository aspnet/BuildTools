// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace KoreBuild.Tasks.ProjectModel
{
    internal class SolutionInfoFactory
    {
        public static SolutionInfo Create(string filePath, string configName)
        {
            var sln = SolutionFile.Parse(filePath);

            if (string.IsNullOrEmpty(configName))
            {
                configName = sln.GetDefaultConfigurationName();
            }

            var projects = new List<string>();

            var config = sln.SolutionConfigurations.FirstOrDefault(c => c.ConfigurationName == configName);
            if (config == null)
            {
                throw new InvalidOperationException($"A solution configuration by the name of '{configName}' was not found in '{filePath}'");
            }

            foreach (var project in sln.ProjectsInOrder
                .Where(p =>
                    p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat // skips solution folders
                    && p.ProjectConfigurations.TryGetValue(config.FullName, out var projectConfig)))
            {
                projects.Add(project.AbsolutePath.Replace('\\', '/'));
            }

            return new SolutionInfo(filePath, projects.ToArray());
        }
    }
}
