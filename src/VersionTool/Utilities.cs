using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ProjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public static class Utilities
    {
        public static IEnumerable<Project> EnumerateProjects(IEnumerable<string> paths)
        {
            return paths.SelectMany(p =>
            {
                return
                    Directory.EnumerateFiles(p, "project.json", SearchOption.AllDirectories)
                    .Where(filePath => !filePath.Contains(".build"))
                    .Select(f => ProjectReader.GetProject(f));
            });
        }

        public static JObject LoadJObject(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var reader = new JsonTextReader(new StreamReader(stream));
                return JObject.Load(reader);
            }
        }

        public static void SaveJObject(string path, JObject root)
        {
            using (var stream = File.Exists(path)
                ? File.Open(path, FileMode.Truncate, FileAccess.Write)
                : File.Create(path))
            {
                using (var writer = new StreamWriter(stream))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        jsonWriter.Formatting = Formatting.Indented;

                        root.WriteTo(jsonWriter);
                    }
                }
            }
        }

        public static IEnumerable<JProperty> EnumerateDependencies(JObject root)
        {
            var dependencies = root.Property("dependencies")?.Value as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies.Properties())
                {
                    yield return dependency;
                }
            }

            var frameworks = root.Property("frameworks")?.Value as JObject;
            if (frameworks != null)
            {
                foreach (var tfm in frameworks.Properties())
                {
                    var frameworkDependencies = (tfm?.Value as JObject)?.Property("dependencies")?.Value as JObject;
                    if (frameworkDependencies != null)
                    {
                        foreach (var dependency in frameworkDependencies.Properties())
                        {
                            yield return dependency;
                        }
                    }
                }
            }

            var runtimes = root.Property("runtimes")?.Value as JObject;
            if (runtimes != null)
            {
                var runtimeDependencies = runtimes.Property("dependencies")?.Value as JObject;
                if (runtimeDependencies != null)
                {
                    foreach (var dependency in runtimeDependencies.Properties())
                    {
                        yield return dependency;
                    }
                }
            }

            var tools = root.Property("tools")?.Value as JObject;
            if (tools != null)
            {
                foreach (var tool in tools.Properties())
                {
                    yield return tool;
                }
            }
        }

        public static Func<string, bool> CreateMatcher(string dependency)
        {
            if (dependency.Contains('*'))
            {
                var regex = "^" + Regex.Escape(dependency).Replace("\\*", ".*") + "$";
                return (s) => Regex.IsMatch(s, regex, RegexOptions.IgnoreCase);
            }
            else
            {
                return (s) => string.Equals(s, dependency, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
