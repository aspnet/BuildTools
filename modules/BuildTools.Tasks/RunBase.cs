// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// A task that runs a process without piping output into the logger.
    /// </summary>
    public abstract class RunBase : ToolTask
    {
        private static readonly char[] EqualsArray = new[] { '=' };

        private const int OK = 0;

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

        protected override bool HandleTaskExecutionErrors()
        {
            return IgnoreExitCode || base.HandleTaskExecutionErrors();
        }

        protected override string GetWorkingDirectory() => WorkingDirectory;

        /// <inheritdoc />
        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        /// <inheritdoc />
        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override bool ValidateParameters()
        {
            var exe = GenerateFullPathToTool();

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

            return base.ValidateParameters();
        }

        public override bool Execute()
        {
            var retries = Math.Max(1, MaxRetries);
            for (int i = 0; i < retries; i++)
            {
                if (base.Execute())
                {
                    return true;
                }
            }

            return false;
        }

        protected override string GenerateCommandLineCommands()
        {
            var cmd = 0;
            var arguments = string.Empty;

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
                return null;
            }

            return arguments;
        }
    }
}
