namespace NuGetPackageVerifier.Logging
{
    public static class PackageVerifierLoggerExtensions
    {
        public static void Log(this IPackageVerifierLogger logger, LogLevel logLevel, string message, params object[] args)
        {
            logger.Log(logLevel, string.Format(message, args));
        }

        public static void LogWarning(this IPackageVerifierLogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Warning, message, args);
        }

        public static void LogError(this IPackageVerifierLogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Error, message, args);
        }

        public static void LogInfo(this IPackageVerifierLogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Info, message, args);
        }
    }
}
