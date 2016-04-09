// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace SplitPackages
{
    public static class Frameworks
    {
        public static string NetCoreApp10 => FrameworkConstants.CommonFrameworks.NetCoreApp10.DotNetFrameworkName;
        public static string Net451 => FrameworkConstants.CommonFrameworks.Net451.DotNetFrameworkName;
        public static string DnxCore50 => FrameworkConstants.CommonFrameworks.DnxCore50.DotNetFrameworkName;
        public static string Dotnet56 => FrameworkConstants.CommonFrameworks.DotNet56.DotNetFrameworkName;
        public static string PortableNet45Win8 => NuGetFramework.Parse("portable-net45+win8").DotNetFrameworkName;

        public static string GetMoniker(string frameworkName)
        {
            var fx = NuGetFramework.ParseFrameworkName(frameworkName, DefaultFrameworkNameProvider.Instance);
            return fx.GetShortFolderName();
        }

        public static FrameworkClasification ClassifyFramework(IList<string> supportedFrameworks)
        {
            var frameworks = supportedFrameworks
                .Select(f => NuGetFramework.ParseFrameworkName(f, DefaultFrameworkNameProvider.Instance));

            var supportsNet451 = GetCompatibleFrameworks(frameworks, FrameworkConstants.CommonFrameworks.Net451);
            var supportsNetStandard = GetCompatibleFrameworks(frameworks, FrameworkConstants.CommonFrameworks.NetStandardApp15);

            if ((supportsNet451.Any() && supportsNetStandard.Any()) ||
                supportedFrameworks.Count == 0)
            {
                return FrameworkClasification.All();
            }
            else if (frameworks.Any(f => SupportsNet451(f)))
            {
                var imports = supportsNet451.Except(new[] { FrameworkConstants.CommonFrameworks.Net451 });
                return FrameworkClasification.Net451(imports.Select(f => f.DotNetFrameworkName));
            }
            else
            {
                var imports = supportsNet451.Except(new[] { FrameworkConstants.CommonFrameworks.NetStandardApp15 });
                return FrameworkClasification.NetStandardApp15(imports.Select(f => f.DotNetFrameworkName));
            }
        }

        private static IEnumerable<NuGetFramework> GetCompatibleFrameworks(
            IEnumerable<NuGetFramework> frameworks,
            NuGetFramework framework)
        {
            return frameworks
                .Where(f => DefaultCompatibilityProvider.Instance.IsCompatible(framework, f));
        }

        public class FrameworkClasification
        {
            public FrameworkClasification(string framework)
            {
                Framework = framework;
            }

            public FrameworkClasification(string framework, IEnumerable<string> imports)
                : this(framework)
            {
                Imports = imports;
            }

            public bool IsAll => Framework == null;

            public bool IsNet451 => Framework == Frameworks.Net451;

            public bool IsNetStandardApp15 => Framework == Frameworks.NetCoreApp10;

            public string Framework { get; }

            public IEnumerable<string> Imports { get; } = Enumerable.Empty<string>();


            public static FrameworkClasification All()
            {
                return new FrameworkClasification(null);
            }

            public static FrameworkClasification Net451(IEnumerable<string> imports)
            {
                return new FrameworkClasification(Frameworks.Net451, imports);
            }

            public static FrameworkClasification NetStandardApp15(IEnumerable<string> imports)
            {
                return new FrameworkClasification(Frameworks.NetCoreApp10, imports);
            }

            public override string ToString()
            {
                return Framework ?? "All";
            }
        }

        private static bool SupportsNet451(NuGetFramework framework)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(
                FrameworkConstants.CommonFrameworks.Net451,
                framework);
        }

        private static bool SupportsNetStandardApp15(NuGetFramework framework)
        {
            return DefaultCompatibilityProvider.Instance.IsCompatible(
                FrameworkConstants.CommonFrameworks.NetStandard15,
                framework);
        }
    }
}
