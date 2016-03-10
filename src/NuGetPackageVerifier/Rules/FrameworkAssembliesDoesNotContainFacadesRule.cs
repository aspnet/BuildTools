// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class FrameworkAssembliesDoesNotContainFacadesRule : IPackageVerifierRule
    {
        private static readonly string[] _facades = new[]
        {
            "System.Collections",
            "System.Collections.Concurrent",
            "System.ComponentModel",
            "System.ComponentModel.Annotations",
            "System.ComponentModel.EventBasedAsync",
            "System.Diagnostics.Contracts",
            "System.Diagnostics.Debug",
            "System.Diagnostics.Tools",
            "System.Diagnostics.Tracing",
            "System.Dynamic.Runtime",
            "System.Globalization",
            "System.IO",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Linq.Parallel",
            "System.Linq.Queryable",
            "System.Net.NetworkInformation",
            "System.Net.Primitives",
            "System.Net.Requests",
            "System.Net.WebHeaderCollection",
            "System.ObjectModel",
            "System.Reflection",
            "System.Reflection.Emit",
            "System.Reflection.Emit.ILGeneration",
            "System.Reflection.Emit.Lightweight",
            "System.Reflection.Extensions",
            "System.Reflection.Primitives",
            "System.Resources.ResourceManager",
            "System.Runtime",
            "System.Runtime.Extensions",
            "System.Runtime.Handles",
            "System.Runtime.InteropServices",
            "System.Runtime.InteropServices.WindowsRuntime",
            "System.Runtime.Numerics",
            "System.Runtime.Serialization.Json",
            "System.Runtime.Serialization.Primitives",
            "System.Runtime.Serialization.Xml",
            "System.Security.Principal",
            "System.ServiceModel.Duplex",
            "System.ServiceModel.Http",
            "System.ServiceModel.NetTcp",
            "System.ServiceModel.Primitives",
            "System.ServiceModel.Security",
            "System.Text.Encoding",
            "System.Text.Encoding.Extensions",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Threading.Tasks.Parallel",
            "System.Threading.Timer",
            "System.Xml.ReaderWriter",
            "System.Xml.XDocument",
            "System.Xml.XmlSerializer"
        };

        public IEnumerable<PackageVerifierIssue> Validate(
            FileInfo nupkgFile,
            IPackageMetadata package,
            IPackageVerifierLogger logger)
        {
            return from a in package.FrameworkReferences
                   where _facades.Contains(a.AssemblyName)
                   from f in a.SupportedFrameworks
                   where f.Framework == FrameworkConstants.FrameworkIdentifiers.Net
                   select PackageIssueFactory.FrameworkAssembliesContainFacade(a.AssemblyName, f);
        }
    }
}
