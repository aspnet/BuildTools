using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public class ListDependencyCommand
    {
        public static int Execute(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument dependencyArgument)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            var matcher = Utilities.CreateMatcher(dependencyArgument.Value);

            foreach (var project in Utilities.EnumerateProjects(paths))
            {
                var root = Utilities.LoadJObject(project.ProjectFilePath);
                foreach (var dependency in Utilities.EnumerateDependencies(root))
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
                            Console.WriteLine($"{match.Name}: {match.Value}");
                        }

                        Console.WriteLine();
                    }
                }
            }

            return 0;
        }
    }
}
