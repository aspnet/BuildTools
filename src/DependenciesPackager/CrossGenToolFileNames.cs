using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DependenciesPackager
{
    internal class CrossGenTool
    {
        private static readonly IDictionary<string, CrossGenTool> _executables = new Dictionary<string, CrossGenTool>
        {
            ["win7"] = new CrossGenTool("crossgen.exe", "clrjit.dll"),
            ["ubuntu.16.04"] = new CrossGenTool("crossgen", "libclrjit.so"),
            ["ubuntu.14.04"] = new CrossGenTool("crossgen", "libclrjit.so"),
            ["debian.8"] = new CrossGenTool("crossgen", "libclrjit.so"),
        };

        private CrossGenTool(string crossgen, string clrjit)
        {
            CrossGen = crossgen;
            ClrJit = clrjit;
        }

        public static CrossGenTool GetCrossGenTool(string runtimeMoniker)
        {
            // eliminate the architecture part from the runtime monitor: debian8-x64 => debian8
            var platform = runtimeMoniker.Substring(0, runtimeMoniker.Length - 4);

            return _executables.TryGetValue(platform, out var result) ? result : null;
        }

        public static IEnumerable<string> FindAllCrossGen(string searchPath) =>
            _executables.Values.Select(f => f.CrossGen).Distinct()
                        .SelectMany(f => Directory.GetFiles(searchPath, f, SearchOption.AllDirectories));

        public static IEnumerable<string> FindAllClrJit(string searchPath) =>
            _executables.Values.Select(f => f.ClrJit).Distinct()
                        .SelectMany(f => Directory.GetFiles(searchPath, f, SearchOption.AllDirectories));

        public string ClrJit { get; }

        public string CrossGen { get; }
    }
}
