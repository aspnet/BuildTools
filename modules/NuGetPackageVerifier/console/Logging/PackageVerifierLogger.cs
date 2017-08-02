// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetPackageVerifier.Logging
{
    public class PackageVerifierLogger : IPackageVerifierLogger
    {
        private readonly bool _hideInfoLogs;

        public PackageVerifierLogger(bool hideInfoLogs)
        {
            _hideInfoLogs = hideInfoLogs;
        }

        public void Log(LogLevel logLevel, string message)
        {
            if (_hideInfoLogs && logLevel == LogLevel.Info)
            {
                return;
            }

            var output = Console.Out;
            ConsoleColor foreColor;
            switch (logLevel)
            {
                case LogLevel.Error:
                    output = Console.Error;
                    foreColor = ConsoleColor.Red;
                    break;

                case LogLevel.Warning:
                    foreColor = ConsoleColor.Yellow;
                    break;

                case LogLevel.Normal:
                    foreColor = ConsoleColor.Gray;
                    break;
                default:
                    foreColor = ConsoleColor.White;
                    break;
            }

            Console.ForegroundColor = foreColor;
            output.WriteLine("{0}: {1}", logLevel.ToString().ToUpperInvariant(), message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
