// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// A task that runs a process without piping output into the logger.
    /// See <seealso cref="RunBase" /> for more arguments.
    /// </summary>
#if SDK
    public class Sdk_Run
#elif BuildTools
    public class Run
#else
#error This must be built either for an SDK or for BuildTools
#endif
        : RunBase
    {
        /// <summary>
        /// The executable to run. Can be a file path or a command for an executable on the system PATH.
        /// </summary>
        [Required]
        public string FileName { get; set; }

        protected override string ToolName => FileName;

        protected override string GenerateFullPathToTool()
        {
            return FileName;
        }
    }
}
