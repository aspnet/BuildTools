using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace DependenciesPackager
{
    internal class PackageEntry
    {
        public IReadOnlyList<LibraryAsset> Assets { get; set; }

        public LibraryDescription Library { get; set; }

        public IDictionary<LibraryAsset, IList<string>> CrossGenOutput { get; } =
            new Dictionary<LibraryAsset, IList<string>>();
    }
}
