// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace PackagePublisher
{
    public class Log
    {
        public static void WriteInformation(string value, params object[] args)
        {
            Console.WriteLine(value, args);
        }

        public static void WriteWarning(string value, params object[] args)
        {
            Console.WriteLine(CreateFormattedMessage(string.Format(value, args), "WARNING"));
        }

        public static void WriteError(string value, params object[] args)
        {
            Console.Error.WriteLine(CreateFormattedMessage(string.Format(value, args), "ERROR"));
        }

        private static string CreateFormattedMessage(string message, string category)
        {
            if (Environment.GetEnvironmentVariable("TEAMCITY_VERSION") != null)
            {
                message = message.Replace("|", "||")
                             .Replace("'", "|'")
                             .Replace("\r", "|r")
                             .Replace("\n", "|n")
                             .Replace("]", "|]");
                return $"##teamcity[message text='{message}' status='{category}']";
            }
            else
            {
                return $"[{category}] {message}";
            }
        }
    }
}