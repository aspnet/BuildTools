// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;

namespace ApiCheck
{
    public abstract class AssemblyLoader
    {
        public static Assembly LoadAssembly(
                string assemblyPath,
                string projectJson,
                string configuration,
                string packagesFolder)
        {
            var ctx = CreateProjectContext(projectJson, packagesFolder);
            var exporter = ctx.CreateExporter(configuration);
            var dependencies = exporter.GetDependencies().ToArray();
            
#if NETCOREAPP1_0
            var loader = new CoreClrAssemblyLoader(dependencies, ctx.RuntimeIdentifier, ctx.GetOutputPaths(configuration)?.CompilationOutputPath);
#else
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var loader = new FullFrameworkAssemblyLoader(assemblyDirectory);
#endif            
            return loader.Load(assemblyPath);
        }

        private static ProjectContext CreateProjectContext(string projectJson, string packagesFolder)
        {
#if NETCOREAPP1_0
            var resolvedFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
#else
            var resolvedFramework = FrameworkConstants.CommonFrameworks.Net452;
#endif
            var project = ProjectReader.GetProject(projectJson);

            var targetFramework = project.GetTargetFrameworks().Where(t =>
#if NETCOREAPP1_0
                    t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.NetCoreApp10.Framework) ||
                        t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.NetStandard.Framework)
#else
                    t.FrameworkName.Framework.Equals(FrameworkConstants.CommonFrameworks.Net45.Framework)
#endif
            ).First();

            var builder = new ProjectContextBuilder()
            .WithProject(project)
            .WithTargetFramework(targetFramework.FrameworkName)
            .WithRuntimeIdentifiers(RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            return builder.Build();
        }
    }
}
