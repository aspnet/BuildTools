// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// A better version of the Exec task. See <seealso cref="RunBase" /> for more arguments.
    /// </summary>
    public class Run : RunBase
    {
        /// <summary>
        /// The executable to run. Can be a file path or a command for an executable on the system PATH.
        /// </summary>
        [Required]
        public string FileName { get; set; }

        protected override string GetExecutable() => FileName;
    }
}
