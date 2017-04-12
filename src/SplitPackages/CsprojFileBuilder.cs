// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PackageClassifier;

namespace SplitPackages
{
    public class CsprojFileBuilder
    {
        private readonly string _path;
        private readonly bool _whatIf;
        private readonly bool _quiet;
        private readonly ILogger _logger;

        public CsprojFileBuilder(
            string path,
            bool whatIf,
            bool warningsAsErrors,
            ILogger logger)
        {
            _path = path;
            _whatIf = whatIf;
            _quiet = warningsAsErrors;
            _logger = logger;
        }

        private readonly IList<PackageInformation> _dependencies = new List<PackageInformation>();
        private readonly IList<FrameworkDefinition> _frameworks = new List<FrameworkDefinition>();

        public void AddDependencies(IEnumerable<PackageInformation> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                var classification = Frameworks.ClassifyFramework(dependency.SupportedFrameworks);
                LogClassification(dependency, classification);

                if (classification.IsAll)
                {
                    _dependencies.Add(dependency);
                }

                if (classification.IsNet451 || classification.IsNetCoreApp10)
                {
                    AddToFramework(dependency, classification.Framework);
                }

                if (!(classification.IsAll || classification.IsNet451 || classification.IsNetCoreApp10))
                {
                    throw new InvalidOperationException($"Cannot classify package '{dependency.Identity}'.");
                }
            }
        }

        private void LogClassification(PackageInformation dependency, Frameworks.FrameworkClasification classification)
        {
            var imports = "";
            if (classification.Imports.Any())
            {
                imports = $"with imports {string.Join(", ", classification.Imports.Select(Frameworks.GetMoniker))}";
            }

            _logger.LogInformation($@"'{dependency.Identity}' with frameworks
{string.Join(Environment.NewLine, dependency.SupportedFrameworks)} is classified as
{classification}
{imports}");
        }

        private void AddToFramework(PackageInformation dependency, string framework)
        {
            var fx = _frameworks.FirstOrDefault(f => f.Name == framework);
            if (fx == null)
            {
                var expectedFrameworks = string.Join(", ", _frameworks.Select(f => f.Name));
                var supportedFrameworks = string.Join(", ", dependency.SupportedFrameworks);
                throw new InvalidOperationException($"Package {dependency.Name} does not support any of the following frameworks '{expectedFrameworks}'. " +
                    $"The following frameworks are supported by the package: {supportedFrameworks}.");
            }
            fx.Dependencies.Add(dependency);
        }

        public void AddFramework(string fx)
        {
            if (fx == Frameworks.Net451)
            {
                _frameworks.Add(FrameworkDefinition.Net451);
            }

            if (fx == Frameworks.NetCoreApp10)
            {
                _frameworks.Add(FrameworkDefinition.NetCoreApp10);
            }
        }

        public void AddImports(string fx, string import)
        {
            var framework = _frameworks.FirstOrDefault(f => f.Name == fx);
            if (framework == null)
            {
                throw new InvalidOperationException($"Couldn't find framework '{fx}' in the list of valid frameworks");
            }

            if (!framework.Imports.Contains(import))
            {
                framework.Imports.Add(import);
            }
        }

        public void Execute()
        {
            var document = new XDocument();
            var root = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));
            var projectDefinitionPropertyGroup = new XElement("PropertyGroup");
            var frameworksNames = string.Join(";", _frameworks.Select(i => Frameworks.GetMoniker(i.Name)));
            projectDefinitionPropertyGroup.Add(new XElement("TargetFrameworks", frameworksNames));

            foreach (var framework in _frameworks)
            {
                var monikerName = Frameworks.GetMoniker(framework.Name);
                if (!monikerName.StartsWith("netcoreapp"))
                {
                    var runtimes = Runtime.AllRuntimes.ToList();
                    if (runtimes?.Count != 0)
                    {
                        var runtimesNames = string.Join(";", runtimes.Select(i => i.Name));
                        projectDefinitionPropertyGroup.Add(
                            new XElement("RuntimeIdentifiers",
                            new XAttribute("Condition", $" '$(TargetFramework)' == '{monikerName}'"),
                            runtimesNames));
                    }
                }

                var imports = framework.Imports;
                if (imports?.Count != 0)
                {
                    var importNames = string.Join(";", imports.Select(Frameworks.GetMoniker));
                    projectDefinitionPropertyGroup.Add(
                        new XElement("PackageTargetFallback",
                        new XAttribute("Condition", $" '$(TargetFramework)' == '{monikerName}'"),
                        $"$(PackageTargetFallback);{importNames}"));
                }
            }

            projectDefinitionPropertyGroup.Add(new XElement("DisableImplicitFrameworkReferences", true));
            root.Add(projectDefinitionPropertyGroup);
            var dependenciesDictionary = CreateDependenciesDictionary(_dependencies);
            var packageDependencies = CreatePackageReferenceList(dependenciesDictionary);
            root.Add(new XElement("ItemGroup", packageDependencies));

            foreach (var framework in _frameworks)
            {
                var frameworkDependencies = CreateFrameworksDictionary(framework.Dependencies);
                if (frameworkDependencies?.Count != 0)
                {
                    var itemGroup = new XElement("ItemGroup");
                    var monikerName = Frameworks.GetMoniker(framework.Name);
                    itemGroup.Add(new XAttribute("Condition", $" '$(TargetFramework)' == '{monikerName}'"), frameworkDependencies);
                    root.Add(itemGroup);
                }
            }

            document.Add(root);
            var writer = _whatIf ? StreamWriter.Null : File.CreateText(_path);
            _logger.LogInformation($"Writing csproj file to {_path}");
            document.Save(writer);

            if (_whatIf)
            {
                _logger.LogInformation(document.ToString());
            }
        }

        private IList<XElement> CreatePackageReferenceList(IDictionary<string, string> dependenciesDictionary)
        {
            var packageReferenceList = new List<XElement>();
            foreach (var packageReference in dependenciesDictionary)
            {
                var packageReferenceElement = new XElement("PackageReference");
                packageReferenceElement.Add(new XAttribute("Include", packageReference.Key));
                packageReferenceElement.Add(new XAttribute("Version", packageReference.Value));
                packageReferenceList.Add(packageReferenceElement);
            }

            return packageReferenceList;
        }

        private IList<XElement> CreateFrameworksDictionary(IList<PackageInformation> dependencies)
        {
            var dependenciesDictionary = CreateDependenciesDictionary(dependencies);
            var packageDependencies = CreatePackageReferenceList(dependenciesDictionary);
            return packageDependencies;
        }

        private IDictionary<string, string> CreateDependenciesDictionary(IList<PackageInformation> dependencies)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in dependencies)
            {
                if (dictionary.ContainsKey(dependency.Identity))
                {
                    var message = $"A duplicate dependency exists in {_path}, name = {dependency.Identity}";
                    if (_quiet)
                    {
                        throw new InvalidOperationException(message);
                    }

                    _logger.LogWarning(message);
                }
                else
                {
                    dictionary[dependency.Identity] = dependency.Version;
                }
            }

            return dictionary;
        }

        private class FrameworkDefinition
        {
            public string Name { get; private set; }
            public IList<string> Imports { get; private set; }
            public IList<PackageInformation> Dependencies { get; } = new List<PackageInformation>();

            public static FrameworkDefinition NetCoreApp10 => new FrameworkDefinition
            {
                Name = Frameworks.NetCoreApp10,
                Imports = new List<string>()
            };

            public static FrameworkDefinition Net451 => new FrameworkDefinition
            {
                Name = Frameworks.Net451,
                Imports = new List<string>()
            };
        }
    }
}
