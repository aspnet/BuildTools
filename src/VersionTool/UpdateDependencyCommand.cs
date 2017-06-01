using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public class UpdateDependencyCommand
    {
        internal static int Execute(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument dependencyArgument,
            CommandArgument versionArgument)
        {
            return UpdateDependency(pathOption.Values, matchingOption.Values, dependencyArgument.Value, versionArgument.Value);
        }

        public static int UpdateDependency(
            List<string> paths,
            List<string> matching,
            string dependencyString,
            string versionString)
        {
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            var matcher = Utilities.CreateMatcher(dependencyString);

            foreach (var project in Utilities.EnumerateProjects(paths))
            {
                var updated = false;

                var root = Utilities.LoadJObject(project.ProjectFilePath);
                foreach (var dependency in Utilities.EnumerateDependencies(root))
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
                    Utilities.SaveJObject(project.ProjectFilePath, root);

                    Console.WriteLine($"Updated: {project.ProjectFilePath}");
                }
            }

            return 0;
        }
    }
}
