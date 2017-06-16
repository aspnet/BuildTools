// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_WaitForDebugger : Task, ICancelableTask
#elif BuildTools
    public class WaitForDebugger : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        private bool _canceled;

        public void Cancel()
        {
            _canceled = true;
        }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"Waiting for debugger. Process ID: {Process.GetCurrentProcess().Id}");

            // 30 seconds
            var maxTimeout = 30 * 1000;
            var step = 150;

            while (!Debugger.IsAttached && maxTimeout > 0 && !_canceled)
            {
                Thread.Sleep(step);
                maxTimeout -= step;
            }

            if (!Debugger.IsAttached && !_canceled)
            {
                Log.LogMessage(MessageImportance.High, "Waiting for debugger timed out. Continuing execution.");
            }

            return true;
        }
    }
}
