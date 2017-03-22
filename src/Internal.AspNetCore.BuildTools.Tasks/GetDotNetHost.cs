// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETSTANDARD1_6

using System.IO;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// <para>
    /// Uses the current runtime information to find the "dotnet.exe" path.
    /// This requires that MSBuild itself execute on top of Microsoft.NETCore.App.
    /// </para>
    /// <para>
    /// This code is modeled of this API: https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.DotNet.Cli.Utils/Muxer.cs.
    /// We don't use this API directly as using Microsoft.DotNet.Cli.Utils with MSBuild causes assembly loading issues.
    /// </para>
    /// <para>
    /// Also, this task may be not be necessary in future releases. At the time of writing, however, this
    /// feature does not exist in MSBuild of the .NET Core SDK. See https://github.com/Microsoft/msbuild/issues/1669
    /// and https://github.com/dotnet/sdk/issues/20
    /// </para>
    /// </summary>
    public class GetDotNetHost : Task
    {

        /// <summary>
        /// The full path to "dotnet.exe"
        /// </summary>
        [Output]
        public string ExecutablePath { get; set; }

        /// <summary>
        /// The folder containing "dotnet.exe". This is the directory also contains shared frameworks and SDKs.
        /// </summary>
        [Output]
        public string DotNetDirectory { get; set; }

        public override bool Execute()
        {
            ExecutablePath = DotNetMuxer.MuxerPath;

            if (ExecutablePath == null)
            {
                Log.LogError("Failed to find the .NET Core installation");
                return false;
            }

            DotNetDirectory = FileHelpers.EnsureTrailingSlash(Path.GetDirectoryName(DotNetDirectory));

            Log.LogMessage(MessageImportance.Low, "Found dotnet muxer in '{0}'", DotNetDirectory);

            return File.Exists(ExecutablePath);
        }
    }
}

#endif
