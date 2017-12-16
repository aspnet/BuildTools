// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace KoreBuild.Tasks
{
    internal abstract class DotNetAssetRequest
    {
        public DotNetAssetRequest(string version)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public string Version { get; }
        public abstract string RuntimeName { get; }
        public abstract string DisplayName { get; }
        public string Channel { get; set; }
        public string InstallDir { get; set; }
        public string Arch { get; set; }
        public string Feed { get; set; }
        public string FeedCredential { get; set; }

        public abstract bool IsInstalled(string basePath);

        public class DotNetSdk : DotNetAssetRequest
        {
            public DotNetSdk(string version) : base(version)
            {
            }

            public override string RuntimeName { get; }

            public override string DisplayName { get; } = ".NET Core SDK";

            public override bool IsInstalled(string basePath)
                => File.Exists(Path.Combine(basePath, "sdk", Version, "dotnet.dll"));
        }

        public class DotNetRuntime : DotNetAssetRequest
        {
            public DotNetRuntime(string version) : base(version)
            {
            }

            public override string RuntimeName { get; } = "dotnet";

            public override string DisplayName { get; } = ".NET Core Runtime";

            public override bool IsInstalled(string basePath)
                => File.Exists(Path.Combine(basePath, "shared", "Microsoft.NETCore.App", Version, ".version"));
        }

        public class AspNetCoreRuntime : DotNetAssetRequest
        {
            public AspNetCoreRuntime(string version) : base(version)
            {
            }

            public override string RuntimeName { get; } = "aspnetcore";

            public override string DisplayName { get; } = "ASP.NET Core Runtime";

            public override bool IsInstalled(string basePath)
                => File.Exists(Path.Combine(basePath, "shared", "Microsoft.AspNetCore.All", Version, ".version"));
        }
    }
}
