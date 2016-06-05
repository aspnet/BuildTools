// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PackageClassifier;

namespace SplitPackages
{
    public class ProjectJsonFileBuilder
    {
        private readonly string _path;
        private readonly bool _whatIf;
        private readonly bool _quiet;
        private readonly ILogger _logger;

        public ProjectJsonFileBuilder(
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

        private IList<PackageInformation> _dependencies = new List<PackageInformation>();
        private IList<FrameworkDefinition> _frameworks = new List<FrameworkDefinition>();

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
                imports = $"with imports {string.Join(", ", classification.Imports.Select(i => Frameworks.GetMoniker(i)))}";
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
                throw new InvalidOperationException($"Framework '{framework.ToString()}' in package '{dependency.Name}' is not valid.");
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
            var document = new JObject();
            document["dependencies"] = JObject.FromObject(CreateDependenciesDictionary(_dependencies));
            document["frameworks"] = JObject.FromObject(CreateFrameworksDictionary());
            document["runtimes"] = JObject.FromObject(CreateRuntimesDictionary());

            var writer = _whatIf ? StreamWriter.Null : File.CreateText(_path);
            using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
            {
                _logger.LogInformation($"Writing project.json file to {_path}");
                document.WriteTo(jsonWriter);

                if (_whatIf)
                {
                    _logger.LogInformation(document.ToString());
                }
            }
        }

        private IDictionary<string, JObject> CreateRuntimesDictionary()
        {
            return new[] { Runtime.Win7x64, Runtime.Win7x86 }.ToDictionary(r => r.Name, r => new JObject());
        }

        private Dictionary<string, JObject> CreateFrameworksDictionary()
        {
            return _frameworks
                .ToDictionary(
                f => Frameworks.GetMoniker(f.Name),
                f => new JObject()
                {
                    ["dependencies"] = JObject.FromObject(CreateDependenciesDictionary(f.Dependencies)),
                    ["imports"] = new JArray(f.Imports.Select(i => Frameworks.GetMoniker(i)))
                });
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
            public string Name { get; set; }
            public IList<string> Imports { get; set; }
            public IList<PackageInformation> Dependencies { get; set; } = new List<PackageInformation>();

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
