using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

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

                c.OnExecute(() => OnExecuteUpdateVersion(pathOption, matchingOption, versionArgument));
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

                c.OnExecute(() => OnExecuteUpdateDependency(pathOption, matchingOption, dependencyArgument, versionArgument));
            });

            var updatePatch = app.Command("update-patch", (c) =>
            {
                var pathOption = c.Option("-p|--path", "Path to projects", CommandOptionType.SingleValue);

                var patchConfig = c.Argument("patchConfig", "configuration for the patch update");

                c.HelpOption("-h|--help");

                c.OnExecute(() => OnUpdatePatch(pathOption, patchConfig));
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

        private static int OnExecuteUpdateVersion(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument versionArgument)
        {
            return OnUpdateVersion(pathOption.Values, matchingOption.Values, versionArgument.Value);
        }

        private static int OnUpdateVersion(
            List<string> paths,
            List<string> matching,
            string versionString)
        {
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            foreach (var project in EnumerateProjects(paths))
            {
                var root = LoadJObject(project.ProjectFilePath);
                var version = (JValue)root.SelectToken("version");

                var updated = false;
                if (version == null && !matching.Any())
                {
                    root.AddFirst(new JProperty("version", versionString));
                    updated = true;
                }
                else if (
                    version != null &&
                    (!matching.Any() || matching.Contains(version.Value.ToString())))
                {
                    version.Replace(new JValue(versionString));
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

        private static int OnExecuteUpdateDependency(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument dependencyArgument,
            CommandArgument versionArgument)
        {
            return OnUpdateDependency(pathOption.Values, matchingOption.Values, dependencyArgument.Value, versionArgument.Value);
        }

        private static int OnUpdateDependency(
            List<string> paths,
            List<string> matching,
            string dependencyString,
            string versionString)
        {
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            var matcher = CreateMatcher(dependencyString);

            foreach (var project in EnumerateProjects(paths))
            {
                var updated = false;

                var root = LoadJObject(project.ProjectFilePath);
                foreach (var dependency in EnumerateDependencies(root))
                {
                    if (matcher(dependency.Name) &&
                        (!matching.Any() || matching.Contains(dependency.Value.ToString())))
                    {
                        if (dependency.Value.Type == JTokenType.Object)
                        {
                            // { type: "build", "version": ".." }
                            // { "target": "project" }
                            // Update the former and skip the latter
                            if (dependency.Value["version"] != null)
                            {
                                dependency.Value["version"] = new JValue(versionString);
                            }

                        }
                        else
                        {
                            dependency.Value.Replace(new JValue(versionString));
                        }
                        updated = true;
                    }
                }

                if (updated)
                {
                    SaveJObject(project.ProjectFilePath, root);

                    //Console.WriteLine($"Updated: {project.ProjectFilePath}");
                }
            }

            return 0;
        }

        private static int OnUpdatePatch(
            CommandOption pathOption,
            CommandArgument patchConfigArgument)
        {
            var path = pathOption.Value();
            var patchConfigPath = patchConfigArgument.Value;
            var patchConfig = LoadJObject(patchConfigPath);

            var repos = (patchConfig["Repos"] as JArray)?.Children()
                .Select(token => new Repository((string)token, Path.Combine(path, (string)token)))
                .ToList();
            var updates = (patchConfig["Updates"] as JArray)?.Children()
                .Select(r => (r as JObject).ToObject<Update>());
            var rules = (patchConfig["Rules"] as JArray)?.Children()
                .Select(r => (r as JObject).ToObject<Rule>());
            var packages = (patchConfig["Packages"] as JArray)?.Children()
                .Select(r => (r as JObject).ToObject<Package>());

            BuildRepoGraph(repos);

            if (updates != null)
            {
                foreach (var update in updates)
                {
                    var repo = repos.Where(r => r.Packages.Contains(update.Name)).SingleOrDefault();
                    if (repo != null)
                    {
                        // update version
                        OnUpdateVersion(
                            new List<string>(new[] { Path.Combine(repo.Path, "src", update.Name) }),
                            new List<string>(),
                            update.NewVersion);
                    }
                    else
                    {
                        // update dependency
                        OnUpdateDependency(
                            new List<string>(new[] { path }),
                            new List<string>(),
                            update.Name,
                            update.NewVersion);
                    }
                }
            }

            foreach (var repo in repos.OrderBy(r => r.Order))
            {
                if (ContainsModifiedSrcProjectFile(repo.Path))
                {
                    Console.WriteLine($"{repo} contains updated packages.");

                    foreach (var package in EnumerateProjects(new[] { Path.Combine(repo.Path, "src") }))
                    {
                        if (packages.Any(p => p.Name == package.Name && p.CurrentVersion == p.NewVersion))
                        {
                            var rule = GetRuleForPackage(package, rules);

                            if (rule == null)
                            {
                                throw new Exception($"No update rule found for package {package.Name} {package.Version}.");
                            }

                            // update version
                            OnUpdateVersion(
                                new List<string>(new[] { package.ProjectDirectory }),
                                new List<string>(new[] { rule.CurrentVersion }),
                                rule.NewVersion);

                            // update dependency
                            OnUpdateDependency(
                                new List<string>(new[] { path }),
                                new List<string>(new[] { rule.CurrentVersion }),
                                package.Name,
                                rule.NewVersion);
                        }
                    }
                }
            }

            UpdatePatchConfig(patchConfig, repos);
            SaveJObject(patchConfigPath, patchConfig);

            return 0;
        }

        private static void BuildRepoGraph(List<Repository> repos)
        {
            var includedSubDirs = new[] { "src", "test", "samples", "tools" };

            foreach (var repo in repos)
            {
                foreach (var subdir in includedSubDirs)
                {
                    var repoSubDir = Path.Combine(repo.Path, subdir);
                    if (Directory.Exists(repoSubDir))
                    {
                        if (subdir == "src")
                        {
                            repo.Packages.UnionWith(Directory.EnumerateDirectories(repoSubDir).Select(path => new DirectoryInfo(path).Name));
                        }

                        foreach (var projectJsonPath in Directory.GetFiles(repoSubDir, "project.json", SearchOption.AllDirectories))
                        {
                            AddDependencies(projectJsonPath, repo.PackageDependencies);
                        }
                    }
                }

                repo.PackageDependencies.ExceptWith(repo.Packages);
            }

            foreach (var repo in repos)
            {
                foreach (var packageDependency in repo.PackageDependencies)
                {
                    var parentRepo = repos.Find(r => r.Packages.Contains(packageDependency));
                    if (parentRepo != null)
                    {
                        parentRepo.ChildDependencies.Add(repo);
                    }
                }
            }

            var visited = new List<Repository>();
            foreach (var repo in repos)
            {
                DFSRepoGraph(repo, visited);
            }
        }

        static void DFSRepoGraph(Repository repo, List<Repository> visited)
        {
            if (visited.Contains(repo))
            {
                throw new Exception("A cyclic dependency between the following repositories has been detected: " +
                    string.Join(" -> ", visited));
            }

            visited.Add(repo);

            foreach (var child in repo.ChildDependencies)
            {
                child.Order = Math.Max(child.Order, repo.Order + 1);

                DFSRepoGraph(child, visited);
            }

            visited.RemoveAt(visited.Count - 1);
        }

        static void AddDependencies(string projectJsonPath, HashSet<string> dependencies)
        {
            var root = LoadJObject(projectJsonPath);
            dependencies.UnionWith(EnumerateDependencies(root).Select(d => d.Name));
        }

        private static Rule GetRuleForPackage(Project package, IEnumerable<Rule> rules)
        {
            foreach (var rule in rules)
            {
                if (Regex.Match(package.Name, rule.Pattern).Success &&
                    string.Equals(package.Version.ToString(), rule.CurrentVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }

            return null;
        }

        private static bool ContainsModifiedSrcProjectFile(string path)
        {
            var srcPath = Path.Combine(path, "src");
            if (!Directory.Exists(srcPath))
            {
                return false;
            }

            var stdout = new StringBuilder();
            var exitCode = new ProcessRunner("git", "diff -- src/**/project.json")
                .WithWorkingDirectory(path)
                .WriteOutputToStringBuilder(stdout, "")
                .Run();

            return !string.IsNullOrWhiteSpace(stdout.ToString());
        }

        private static void UpdatePatchConfig(JObject patchConfig, List<Repository> repos)
        {
            var reposWithSrc = repos.Select(repo => Path.Combine(repo.Path, "src")).Where(srcDir => Directory.Exists(srcDir));
            var packages = patchConfig["Packages"] as JArray ?? new JArray();
            foreach (var project in EnumerateProjects(reposWithSrc))
            {
                var package = packages.Children().Where(p => (string)((p as JObject)["Name"]) == project.Name).Single() as JObject;
                if (package != null)
                {
                    package["NewVersion"] = project.Version.ToString();
                }
                else
                {
                    packages.Add(new JObject(
                        new JProperty("Name", project.Name),
                        new JProperty("CurrentVersion", project.Version.ToString()),
                        new JProperty("NewVersion", project.Version.ToString())));
                }
            }

            patchConfig["Packages"] = packages;
        }

        private class Update
        {
            public string Name;
            public string NewVersion;
        }

        private class Rule
        {
            public string Pattern;
            public string CurrentVersion;
            public string NewVersion;
        }

        private class Package
        {
            public string Name;
            public string CurrentVersion;
            public string NewVersion;
        }

        private class Repository
        {
            public Repository(string name, string path)
            {
                Name = name;
                Path = path;
            }

            public string Name { get; }

            public string Path { get; }

            public HashSet<string> Packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> PackageDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public HashSet<Repository> ChildDependencies = new HashSet<Repository>();

            public int Order { get; set; }

            public override string ToString()
            {
                return Name;
            }
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

            var tools = root.Property("tools")?.Value as JObject;
            if (tools != null)
            {
                foreach (var tool in tools.Properties())
                {
                    yield return tool;
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
    }
}
