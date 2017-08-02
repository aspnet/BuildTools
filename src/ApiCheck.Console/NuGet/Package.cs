// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace ApiCheck.NuGet
{
    public class Package : IEquatable<Package>
    {
        public Package(
            string name,
            string version,
            string path,
            string signaturePath,
            IEnumerable<Package> dependencies,
            IEnumerable<string> runtimeAssemblies)
        {
            Name = name;
            Version = version;
            Path = path;
            PackageHash = signaturePath;
            Dependencies = dependencies;
            Assemblies = runtimeAssemblies.Select(ra => new PackageAssembly(ra, GetAssemblyPath(ra)));
        }

        public Package(Package package)
        {
            Name = package.Name;
            Version = package.Version;
            Path = package.Path;
            PackageHash = package.PackageHash;
            Dependencies = package.Dependencies;
            Assemblies = package.Assemblies;
        }

        public LibraryIdentity Identity => new LibraryIdentity(Name, NuGetVersion.Parse(Version), LibraryType.Package);
        public string Name { get; set; }
        public string Version { get; set; }
        public IEnumerable<Package> Dependencies { get; set; }
        public string Path { get; set; }
        public string PackageHash { get; set; }
        public IEnumerable<PackageAssembly> Assemblies { get; set; }

        public bool Equals(Package other) =>
            Name.Equals(other?.Name, StringComparison.OrdinalIgnoreCase) &&
                Version.Equals(other?.Version, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => Equals(obj as Package);

        public override int GetHashCode() => Name.GetHashCode() ^ Version.GetHashCode();

        public override string ToString() => $"{Name} {Version}";

        public string GetAssemblyPath(string relativeAssemblyPath) =>
            System.IO.Path.Combine(Path, relativeAssemblyPath);
    }
}
