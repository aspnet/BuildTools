using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace DependenciesPackager
{
    internal class PackageEntry
    {
        public IDictionary<LibraryAsset, int> CrossGenExitCode { get; set; } = new Dictionary<LibraryAsset, int>();

        public PackageDescription Library { get; set; }

        public IReadOnlyList<LibraryAsset> Assets { get; set; }

        public IDictionary<LibraryAsset, IList<string>> CrossGenOutput { get; } =
            new Dictionary<LibraryAsset, IList<string>>();
    }
}
