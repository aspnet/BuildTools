// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Runs a dotnet command
    /// </summary>
    public class RunDotNet : RunBase
    {
        protected override string GetExecutable()
#if NET46
            => "dotnet";
#else
            => DotNetMuxer.MuxerPathOrDefault();
#endif
    }
}
