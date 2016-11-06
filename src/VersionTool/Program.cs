using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();

            var listCommand = app.Command("list", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnList(pathOption));
            });

            var updateVersionCommand = app.Command("update-version", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var versionArgument = c.Argument("version", "the version to set");

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnUpdateVersion(pathOption, matchingOption, versionArgument));
            });

            var listDepdendency = app.Command("list-dependency", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var dependencyArgument = c.Argument("dependency", "the dependency package name");

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnListDependency(pathOption, matchingOption, dependencyArgument));
            });

            var updateDependency = app.Command("update-dependency", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var dependencyArgument = c.Argument("dependency", "the dependency package name");
                var versionArgument = c.Argument("version", "the dependency version");

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnUpdateDependency(pathOption, matchingOption, dependencyArgument, versionArgument));
            });


            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            app.Execute(args);
        }

        private static int OnList(CommandOption pathOption)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Directory.GetCurrentDirectory());
            }

            foreach (var project in EnumerateProjects(paths))
            {
                Console.WriteLine($"Name:      {project.Name}");
                Console.WriteLine($"Directory: {project.ProjectDirectory}");
                Console.WriteLine($"Version:   {project.Version}");
                Console.WriteLine();
            }

            return 0;
        }

        private static int OnUpdateVersion(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument versionArgument)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            foreach (var project in EnumerateProjects(paths))
            {
                var root = LoadJObject(project.ProjectFilePath);
                var version = (JValue)root.SelectToken("version");

                var updated = false;
                if (version == null && !matchingOption.HasValue())
                {
                    root.AddFirst(new JProperty("version", versionArgument.Value));
                    updated = true;
                }
                else if (
                    version != null &&
                    (!matchingOption.HasValue() || matchingOption.Values.Contains(version.Value.ToString())))
                {
                    version.Replace(new JValue(versionArgument.Value));
                    updated = true;
                }

                if (updated)
                {
                    SaveJObject(project.ProjectFilePath, root);

                    Console.WriteLine($"Updated: {project.ProjectFilePath}");
                }
            }

            return 0;
        }

        private static int OnListDependency(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument dependencyArgument)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            var matcher = CreateMatcher(dependencyArgument.Value);

            foreach (var project in EnumerateProjects(paths))
            {
                var root = LoadJObject(project.ProjectFilePath);
                foreach (var dependency in EnumerateDependencies(root))
                {
                    var matches = new List<JProperty>();

                    if (matcher(dependency.Name) &&
                        (!matchingOption.HasValue() ||
                            matchingOption.Values.Contains(dependency.Value.ToString())))
                    {
                        matches.Add(dependency);
                    }

                    if (matches.Count > 0)
                    {
                        Console.WriteLine($"Project: {project.ProjectFilePath}");
                        foreach (var match in matches)
                        {
                            Console.WriteLine($"{match.Name}: {match.Value.ToString()}");
                        }

                        Console.WriteLine();
                    }
                }
            }

            return 0;
        }

        private static int OnUpdateDependency(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument dependencyArgument,
            CommandArgument versionArgument)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            var matcher = CreateMatcher(dependencyArgument.Value);

            foreach (var project in EnumerateProjects(paths))
            {
                var updated = false;

                var root = LoadJObject(project.ProjectFilePath);
                foreach (var dependency in EnumerateDependencies(root))
                {
                    if (matcher(dependency.Name) &&
                        (!matchingOption.HasValue() ||
                            matchingOption.Values.Contains(dependency.Value.ToString())))
                    {
                        if (dependency.Value.Type == JTokenType.Object)
                        {
                            // { type: "build", "version": ".." }
                            // { "target": "project" }
                            // Update the former and skip the latter
                            if (dependency.Value["version"] != null)
                            {
                                dependency.Value["version"] = new JValue(versionArgument.Value);
                            }

                        }
                        else
                        {
                            dependency.Value.Replace(new JValue(versionArgument.Value));
                        }
                        updated = true;
                    }
                }

                if (updated)
                {
                    SaveJObject(project.ProjectFilePath, root);

                    Console.WriteLine($"Updated: {project.ProjectFilePath}");
                }
            }

            return 0;
        }

        private static IEnumerable<Project> EnumerateProjects(IEnumerable<string> paths)
        {
            return paths.SelectMany(p =>
            {
                return
                    Directory.EnumerateFiles(p, "project.json", SearchOption.AllDirectories)
                    .Select(f => ProjectReader.GetProject(f));
            });
        }

        private static IEnumerable<JProperty> EnumerateDependencies(JObject root)
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
        }

        private static Func<string, bool> CreateMatcher(string dependency)
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

        private static JObject LoadJObject(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var reader = new JsonTextReader(new StreamReader(stream));
                return JObject.Load(reader);
            }
        }

        private static void SaveJObject(string path, JObject root)
        {
            using (var stream = File.Open(path, FileMode.Truncate, FileAccess.Write))
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
    }
}
