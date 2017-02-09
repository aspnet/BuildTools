using System;
using System.Collections.Generic;

namespace VersionTool
{
    public class Repository
    {
        public Repository(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
        public int Order { get; set; }

        public HashSet<string> Packages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PackageDependencies { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<Repository> DependentRepos { get; } = new HashSet<Repository>();
    }
}
