using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public class UpdateVersionCommand
    {
        public static int Execute(
            CommandOption pathOption,
            CommandOption matchingOption,
            CommandArgument versionArgument)
        {
            return UpdateVersion(pathOption.Values, matchingOption.Values, versionArgument.Value);
        }

        public static int UpdateVersion(
            List<string> paths,
            List<string> matching,
            string versionString)
        {
            if (paths.Count == 0)
            {
                paths.Add(Path.GetFullPath("."));
            }

            foreach (var project in Utilities.EnumerateProjects(paths))
            {
                var root = Utilities.LoadJObject(project.ProjectFilePath);
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
                    Utilities.SaveJObject(project.ProjectFilePath, root);

                    Console.WriteLine($"Updated: {project.ProjectFilePath}");
                }
            }

            return 0;
        }
    }
}
