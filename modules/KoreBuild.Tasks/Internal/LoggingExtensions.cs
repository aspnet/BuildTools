// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Utilities;

namespace KoreBuild.Tasks
{
    public static class LoggingExtensions
    {
        public static void LogKoreBuildError(this TaskLoggingHelper logger, int code, string message, params object[] messageArgs)
            => LogKoreBuildError(logger, null, code, message, messageArgs: messageArgs);

        public static void LogKoreBuildError(this TaskLoggingHelper logger, string filename, int code, string message, params object[] messageArgs)
        {
            logger.LogError(null, KoreBuildErrors.Prefix + code, null, filename, 0, 0, 0, 0, message, messageArgs: messageArgs);
        }
    }
}
