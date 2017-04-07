// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace PackageClassifier
{
    public class PackageInformation
    {
        public PackageInformation(string fullPath, string identity, string version, IEnumerable<string> supportedFrameworks)
        {
            FullPath = fullPath;
            Identity = identity;
            Version = version;
            SupportedFrameworks = supportedFrameworks;
        }

        public string Name => Path.GetFileName(FullPath);

        public string FullPath { get; }
        public string Identity { get; }
        public string Version { get; }
        public IEnumerable<string> SupportedFrameworks { get; }

        public bool HasId(string id)
        {
            return string.Equals(Identity, id, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"Identity '{Identity}', Version '{Version}', Path '{FullPath}'";
        }
    }
}
