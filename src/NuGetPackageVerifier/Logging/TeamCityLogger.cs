// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetPackageVerifier.Logging
{
    public class TeamCityLogger : IPackageVerifierLogger
    {
        public void Log(LogLevel logLevel, string message)
        {
            string status;
            switch (logLevel)
            {
                case LogLevel.Error:
                    status = "ERROR";
                    break;
                case LogLevel.Warning:
                    status = "WARNING";
                    break;
                default:
                    status = "NORMAL";
                    break;
            }

            message = message
                .Replace("|", "||")
                .Replace("'", "|'")
                .Replace("\r", "|r")
                .Replace("\n", "|n")
                .Replace("]", "|]");

            Console.WriteLine($"##teamcity[message text='{message}' status='{status}']");
        }
    }
}
