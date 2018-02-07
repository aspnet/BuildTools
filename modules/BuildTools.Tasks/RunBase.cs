// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// A task that runs a process without piping output into the logger.
    /// </summary>
    public abstract class RunBase : Task
    {
        private static readonly char[] EqualsArray = new[] { '=' };

        private const int OK = 0;

        protected abstract string GetExecutable();

        /// <summary>
        /// A list of arguments to be passed to the executable. The task will escape them for spaces and quotes.
        /// Cannot be used with <see cref="Command"/>
        /// </summary>
        public ITaskItem[] Arguments { get; set; }

        /// <summary>
        /// The command to pass to the executable as string.
        /// Cannot be used with <see cref="Arguments"/>
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Environment variables to set on the process.
        /// </summary>
        /// <remarks>
        /// The item spec will split on '='. Alternatively, it will look for the metadata name "Value".
        /// </remarks>
        public ITaskItem[] EnvironmentVariables { get; set; }

        // Additional options
        /// <summary>
        /// The current working directory
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Don't fail the task if the command exits with a non-zero code
        /// </summary>
        public bool IgnoreExitCode { get; set; }

        /// <summary>
        /// Repeat the command up to this many times if the exit code is non-zero. Defaults to 0.
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Set the value for UseShellExecute on the new process.
        /// </summary>
        public bool UseShellExecute { get; set; }

        [Output]
        public int ExitCode { get; set; }

        public override bool Execute()
        {
            // Initialize to non-zero in case of early return
            ExitCode = -1;

            var exe = GetExecutable();

            if (string.IsNullOrEmpty(exe))
            {
                Log.LogError("FileName must be specified.");
                return false;
            }

            if (MaxRetries < 0)
            {
                Log.LogError("MaxRetries must be a non-negative number.");
                return false;
            }

            if (!string.IsNullOrEmpty(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
            {
                Log.LogError("WorkingDirectory does not exist: '{0}'", WorkingDirectory);
                return false;
            }

            // if path is not rooted, it may be a command on the system PATH
            if (Path.IsPathRooted(exe) && !File.Exists(exe))
            {
                Log.LogError("FileName does not exist: '{0}'", WorkingDirectory);
                return false;
            }

            var arguments = string.Empty;
            var cmd = 0;
            if (Arguments != null)
            {
                arguments = ArgumentEscaper.EscapeAndConcatenate(Arguments.Select(i => i.ItemSpec));
                cmd++;
            }

            if (!string.IsNullOrEmpty(Command))
            {
                arguments = Command;
                cmd++;
            }

            if (cmd > 1)
            {
                Log.LogError("Arguments and Command cannot both be used.");
                return false;
            }

            var process = new Process
            {
                StartInfo =
                {
                    FileName = exe,
                    Arguments = arguments,
                    WorkingDirectory = WorkingDirectory ?? Directory.GetCurrentDirectory(),
                    UseShellExecute = UseShellExecute,
                    CreateNoWindow = true,
                },
            };

            foreach (var var in GetEnvVars(EnvironmentVariables))
            {
                process.StartInfo.Environment[var.Key] = var.Value;
            }

            var remainingTries = MaxRetries;
            do
            {
                try
                {
                    Log.LogMessage(MessageImportance.Low, "Starting process: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
                    Log.LogMessage(MessageImportance.Low, "Working directory: {0}", process.StartInfo.WorkingDirectory);
                    if (remainingTries != MaxRetries)
                    {
                        Log.LogMessage(MessageImportance.Normal, "Retrying failed command. Remaining retries {0}", remainingTries);
                    }
                    process.Start();
                }
                catch (Exception e)
                {
                    Log.LogError("Run failed to start the process");
                    Log.LogError(e.Message);
                    return false;
                }

                process.WaitForExit();

                ExitCode = process.ExitCode;

                if (process.ExitCode == OK)
                {
                    break;
                }
            } while (remainingTries-- > 0);

            var success = IgnoreExitCode || process.ExitCode == OK;
            if (!success)
            {
                Log.LogError("Run exited with a non-zero exit code: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
            }
            return success;
        }

        private static Dictionary<string, string> GetEnvVars(ITaskItem[] envVars)
        {
            var values = new Dictionary<string, string>();
            if (envVars == null)
            {
                return values;
            }

            foreach (var var in envVars)
            {
                var splitSpec = var.ItemSpec.Split(EqualsArray, 2);
                if (splitSpec.Length > 1)
                {
                    values.Add(splitSpec[0], splitSpec[1]);
                    continue;
                }
                var value = var.GetMetadata("Value");
                values.Add(splitSpec[0], value);
            }

            return values;
        }
    }
}
