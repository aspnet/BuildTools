using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace VersionTool
{
    public class UpdatePatchCommand
    {
        public static int Execute(
            CommandOption directoryOption,
            CommandOption updatePatchConfigOption,
            CommandOption updatePackageOption,
            CommandOption updateRepoOption,
            CommandArgument patchConfigArgument)
        {
            var directory = directoryOption.Value();
            var patchConfigPath = patchConfigArgument.Value;
            var repoConverter = new RepositoryConverter(directory);
            var patchConfig = JsonConvert.DeserializeObject<PatchConfig>(File.ReadAllText(patchConfigPath), repoConverter);

            // Handle patch config update
            if (updatePatchConfigOption.HasValue())
            {
                // Clear the Packages section
                patchConfig.Packages = new List<Package>();

                // Scan for all packages and create new Packages section
                UpdatePatchConfig(patchConfig, patchConfigPath, repoConverter);

                return 0;
            }

            BuildRepoGraph(patchConfig.Repos);

            // Handle package updates
            if (updatePackageOption.HasValue())
            {
                foreach (var packageUpdate in updatePackageOption.Values)
                {
                    var parsedPackageUpdate = packageUpdate.Split(new[] { ':' }, 2);

                    if (parsedPackageUpdate.Length != 2)
                    {
                        throw new Exception($"Invalid package update option {packageUpdate}.");
                    }

                    var packageName = parsedPackageUpdate[0];
                    var newPackageVersion = parsedPackageUpdate[1];

                    Repository repo;
                    try
                    {
                        repo = patchConfig.Repos.SingleOrDefault(r => r.Packages.Contains(packageName));
                    }
                    catch (InvalidOperationException)
                    {
                        var repos = patchConfig.Repos.Where(r => r.Packages.Contains(packageName));
                        Console.WriteLine($"Error: found more than one repo containing the package {packageName}: {string.Join(", ", repos.Select(r => r.Name))}");
                        throw;
                    }

                    if (repo != null)
                    {
                        // update version
                        UpdateVersionCommand.UpdateVersion(
                            new List<string>(new[] { Path.Combine(repo.Path, "src", packageName) }),
                            new List<string>(),
                            newPackageVersion);

                        // update dependency
                        UpdateDependencyCommand.UpdateDependency(
                            new List<string>(new[] { directory }),
                            new List<string>(),
                            packageName,
                            newPackageVersion);

                        // update entry in packages
                        Package package;
                        try
                        {
                            package = patchConfig.Packages.Single(p => string.Equals(p.Name, packageName, StringComparison.OrdinalIgnoreCase));
                        }
                        catch (InvalidOperationException)
                        {
                            Console.WriteLine($"Error: found more than one entry in patch config for the package {packageName}.");
                            throw;
                        }
                        package.NewVersion = newPackageVersion;
                    }
                    else
                    {
                        // update dependency
                        UpdateDependencyCommand.UpdateDependency(
                            new List<string>(new[] { directory }),
                            new List<string>(),
                            packageName,
                            newPackageVersion);
                    }
                }
            }

            var newlyUpdatedRepos = new List<Repository>();

            // Handle repo updates
            if (updateRepoOption.HasValue())
            {
                foreach (var repoUpdate in updateRepoOption.Values)
                {
                    Repository repo;
                    try
                    {
                        repo = patchConfig.Repos.SingleOrDefault(r => string.Equals(r.Name, repoUpdate, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (InvalidOperationException)
                    {
                        Console.WriteLine($"Error: found more than one entry in the Repo section of the patch config for the repo {repoUpdate}.");
                        throw;
                    }

                    if (repo == null)
                    {
                        throw new InvalidOperationException($"Repo {repoUpdate} not found in patch config.");
                    }

                    UpdatePackagesInRepo(repo, directory, patchConfig, newlyUpdatedRepos);
                }
            }

            // Cascade updates
            foreach (var repo in patchConfig.Repos.OrderBy(r => r.Order))
            {
                if (ContainsModifiedSrcProjectFile(repo.Path))
                {
                    UpdatePackagesInRepo(repo, directory, patchConfig, newlyUpdatedRepos);
                }
            }

            // Update patch config with updated packages
            UpdatePatchConfig(patchConfig, patchConfigPath, repoConverter);

            // Notify check in still required
            Console.WriteLine("**************************************************************************");
            Console.WriteLine("* Updates for patch have been made. Please commit the following changes. *");
            Console.WriteLine("**************************************************************************");

            foreach (var repo in patchConfig.Repos.OrderBy(r => r.Order))
            {
                if (newlyUpdatedRepos.Contains(repo))
                {
                    // Rule of thumb, the branch name should follow the version numbers of the SRC package, which should be identical within the repo.
                    var projects = Utilities.EnumerateProjects(new[] { Path.Combine(repo.Path, "src") });
                    var repoVersion = projects.Select(p => p.Version).Max();

                    // Notify branch creation and commit required
                    Console.WriteLine($"SRC project files modified in {repo.Name}. Please create branch rel/{repoVersion} and merge the changes in {repo.Path}.");
                }
                else if (ContainsModifiedProjectFile(repo.Path))
                {
                    // Notify commit required, but no branch needs to be created
                    Console.WriteLine($"Non-SRC project files modified in {repo.Name}. Please commit and merge the changes in {repo.Path}.");
                }
            }

            return 0;
        }

        private static void UpdatePackagesInRepo(Repository repo, string directory, PatchConfig patchConfig, List<Repository> newlyUpdateRepos)
        {
            foreach (var package in Utilities.EnumerateProjects(new[] { Path.Combine(repo.Path, "src") }))
            {
                Package unUpdatedPackage = null;
                try
                {
                    unUpdatedPackage = patchConfig.Packages.SingleOrDefault(p => p.Name == package.Name && p.CurrentVersion == package.Version.ToString());
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine($"Error: found more than one entry in patch config for the package {unUpdatedPackage?.Name ?? string.Empty}.");
                    throw;
                }

                if (unUpdatedPackage != null)
                {
                    Console.WriteLine($"{package.Name} is a src project and has not been updated in this patch, updating version.");
                    newlyUpdateRepos.Add(repo);

                    var rule = GetRuleForPackage(package, patchConfig.Rules);

                    if (rule == null)
                    {
                        throw new InvalidOperationException(
$@"No update rule found for package {package.Name} {package.Version}. Please add a rule in the patch config with the format:
""Rules"": [
  {{
    ""Pattern"": ""<regex matching package name> or {package.Name}"",
    ""CurrentVersion"": ""{package.Version}"",
    ""NewVersion"": ""<the new version to update to>"",
  }}
]");
                    }

                    // update version
                    UpdateVersionCommand.UpdateVersion(
                        new List<string>(new[] { package.ProjectDirectory }),
                        new List<string>(),
                        rule.NewVersion);

                    // update dependency
                    UpdateDependencyCommand.UpdateDependency(
                        new List<string>(new[] { directory }),
                        new List<string>(),
                        package.Name,
                        rule.NewVersion);

                    // update entry in packages
                    unUpdatedPackage.NewVersion = rule.NewVersion;
                }
            }
        }

        private static void BuildRepoGraph(List<Repository> repos)
        {
            var includedSubDirs = new[] { "src", "test", "samples", "tools" };

            foreach (var repo in repos)
            {
                foreach (var subDir in includedSubDirs)
                {
                    var repoSubDir = Path.Combine(repo.Path, subDir);
                    if (Directory.Exists(repoSubDir))
                    {
                        if (subDir == "src")
                        {
                            repo.Packages.UnionWith(Directory.EnumerateDirectories(repoSubDir).Select(Path.GetFileName));
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
                        parentRepo.DependentRepos.Add(repo);
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

            foreach (var child in repo.DependentRepos)
            {
                child.Order = Math.Max(child.Order, repo.Order + 1);

                DFSRepoGraph(child, visited);
            }

            visited.RemoveAt(visited.Count - 1);
        }

        static void AddDependencies(string projectJsonPath, HashSet<string> dependencies)
        {
            var root = Utilities.LoadJObject(projectJsonPath);
            dependencies.UnionWith(Utilities.EnumerateDependencies(root).Select(d => d.Name));
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
            if (!Directory.Exists(Path.Combine(path, "src")))
            {
                return false;
            }

            var result = Command.Create("git", new[] { "diff", "--", "./src/**/project.json" })
                .WorkingDirectory(path)
                .CaptureStdOut()
                .Execute();

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not run 'git diff'");
            }

            return !string.IsNullOrWhiteSpace(result.StdOut);
        }

        private static bool ContainsModifiedProjectFile(string path)
        {
            var result = Command.Create("git", new[] { "diff", "--", "./**/project.json" })
                .WorkingDirectory(path)
                .CaptureStdOut()
                .Execute();

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not run 'git diff'");
            }

            return !string.IsNullOrWhiteSpace(result.StdOut);
        }

        private static void UpdatePatchConfig(PatchConfig patchConfig, string path, params JsonConverter[] converters)
        {
            var reposWithSrc = patchConfig.Repos.Select(repo => Path.Combine(repo.Path, "src")).Where(Directory.Exists);
            foreach (var project in Utilities.EnumerateProjects(reposWithSrc))
            {
                Package package = null;
                try
                {
                    package = patchConfig.Packages.SingleOrDefault(p => p.Name == project.Name);
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine($"Error: found more than one entry in patch config for the package {package?.Name ?? string.Empty}.");
                    throw;
                }
                if (package != null)
                {
                    package.NewVersion = project.Version.ToString();
                }
                else
                {
                    patchConfig.Packages.Add(new Package
                    {
                        Name = project.Name,
                        CurrentVersion = project.Version.ToString(),
                        NewVersion = project.Version.ToString()
                    });
                }
            }

            // Sort package list by alphabetical order
            patchConfig.Packages.Sort((a, b) => a.Name.CompareTo(b.Name));

            File.WriteAllText(path, JsonConvert.SerializeObject(patchConfig, Formatting.Indented, converters));
        }
    }
}
