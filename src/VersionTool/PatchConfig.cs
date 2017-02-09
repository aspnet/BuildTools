using System.Collections.Generic;

namespace VersionTool
{
    public class PatchConfig
    {
        public List<Repository> Repos { get; set; }
        public List<Rule> Rules { get; set; }
        public List<Package> Packages { get; set; }
    }
}
