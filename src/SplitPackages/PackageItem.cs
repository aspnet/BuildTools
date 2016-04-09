// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace SplitPackages
{
    public class PackageItem : IEquatable<PackageItem>
    {
        public string Name { get; set; }
        public string OriginPath { get; set; }
        public string DestinationFolderName { get; set; }
        public string DestinationPath { get; set; }
        public bool FoundInCsv { get; set; }
        public bool FoundInFolder { get; set; }
        public string Identity { get; set; }
        public string Version { get; set; }

        public IList<string> SupportedFrameworks { get; set; } = new List<string>();

        public bool Equals(PackageItem other)
        {
            return string.Equals(OriginPath, other?.OriginPath);
        }

        public override int GetHashCode()
        {
            return OriginPath != null ? OriginPath.GetHashCode() : 1;
        }

        public override string ToString()
        {
            return $"Name {Name}, Identity {Identity}, Version {Version}, Csv {FoundInCsv}, Source {FoundInFolder}";
        }
    }
}
