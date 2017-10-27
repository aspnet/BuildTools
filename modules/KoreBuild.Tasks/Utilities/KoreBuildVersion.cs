// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Reflection;

namespace KoreBuild.Tasks
{
    internal class KoreBuildVersion
    {
        private static string _version;

        public static string Current
        {
            get
            {
                if (_version == null)
                {
                    var assembly = typeof(KoreBuildVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (assembly != null)
                    {
                        _version = assembly.InformationalVersion;
                    }
                }

                return _version;
            }
        }
    }
}
