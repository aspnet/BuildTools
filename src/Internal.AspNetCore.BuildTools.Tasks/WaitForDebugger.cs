// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.BuildTools
{
    public class WaitForDebugger : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"Waiting for debugger. Process ID: {Process.GetCurrentProcess().Id}");

            // 30 seconds
            var maxTimeout = 30 * 1000; 
            var step = 150;

            while (!Debugger.IsAttached && maxTimeout > 0)
            {
                Thread.Sleep(step);
                maxTimeout -= step;
            }

            if (!Debugger.IsAttached)
            {
                Log.LogMessage(MessageImportance.High, "Waiting for debugger timed out. Continuing execution.");
            }

            return true;
        }
    }
}
