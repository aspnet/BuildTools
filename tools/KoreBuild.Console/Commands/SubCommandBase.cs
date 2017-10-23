// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace KoreBuild.Console.Commands
{
    internal abstract class SubCommandBase : CommandBase
    {
        protected SubCommandBase(CommandContext context)
        {
            Context = context;
        }

        protected CommandContext Context { get; }

        protected IReporter Reporter => Context.Reporter;

        protected override bool IsValid()
        {
            if (!Directory.Exists(Context.RepoPath))
            {
                Context.Reporter.Error($"The RepoPath '{Context.RepoPath}' doesn't exist.");
                return false;
            }

            return base.IsValid();
        }

        protected int RunDotnet(params string[] arguments)
            => RunDotnet(arguments, Directory.GetCurrentDirectory());

        protected int RunDotnet(IEnumerable<string> arguments, string workingDir)
        {
            var args = ArgumentEscaper.EscapeAndConcatenate(arguments);

            // use the dotnet.exe file used to start this process
            var dotnet = DotNetMuxer.MuxerPath;
            // if it could not be found, fallback to detecting DOTNET_HOME or PATH
            dotnet = string.IsNullOrEmpty(dotnet) || !Path.IsPathRooted(dotnet)
                ? Context.GetDotNetExecutable()
                : dotnet;

            var psi = new ProcessStartInfo
            {
                FileName = dotnet,
                Arguments = args,
                WorkingDirectory = workingDir,
            };

            Reporter.Verbose($"Executing '{psi.FileName} {psi.Arguments}'");

            var process = Process.Start(psi);
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}
