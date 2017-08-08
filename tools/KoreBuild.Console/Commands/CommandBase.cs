// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;

namespace KoreBuild.Console.Commands
{
    internal abstract class CommandBase
    {
        private CommandLineApplication _application;

        public virtual void Configure(CommandLineApplication application)
        {
            _application = application;

            application.HelpOption("-h|--help");

            application.OnExecute(
                () =>
                {
                    if(IsValid())
                    {
                        return Execute();
                    }
                    else
                    {
                        application.ShowHelp();
                        return 1;
                    }
                });
        }

        protected virtual int Execute()
        {
            _application.ShowHelp();

            return 0;
        }

        protected virtual bool IsValid()
        {
            return true;
        }
    }
}
