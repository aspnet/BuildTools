// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// A task that runs a dotnet command without piping output into the logger.
    /// See <seealso cref="RunBase" /> for more arguments.
    /// </summary>
#if SDK
    public class Sdk_RunDotNet
#elif BuildTools
    public class RunDotNet
#else
#error This must be built either for an SDK or for BuildTools
#endif
        : RunBase
    {
        protected override string ToolName => "dotnet";

        protected override string GenerateFullPathToTool()
#if NET46
            => "dotnet";
#else
            => DotNetMuxer.MuxerPathOrDefault();
#endif
    }
}
