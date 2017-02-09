using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace VersionTool
{
    class ListCommand
    {
        public static int Execute(CommandOption pathOption)
        {
            var paths = pathOption.Values;
            if (paths.Count == 0)
            {
                paths.Add(Directory.GetCurrentDirectory());
            }

            foreach (var project in Utilities.EnumerateProjects(paths))
            {
                Console.WriteLine($"Name:      {project.Name}");
                Console.WriteLine($"Directory: {project.ProjectDirectory}");
                Console.WriteLine($"Version:   {project.Version}");
                Console.WriteLine();
            }

            return 0;
        }
    }
}
