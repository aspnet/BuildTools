// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Writes a message to console output that does not flow through the MSBuild logger.
    /// </summary>
#if SDK
    public class Sdk_ConsoleMessage : Task
#elif BuildTools
    public class ConsoleMessage : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        public string Text { get; set; }

        public override bool Execute()
        {
            Console.WriteLine(Text);
            return true;
        }
    }
}
