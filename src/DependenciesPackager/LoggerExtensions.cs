using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    internal static class LoggerExtensions
    {
        public static void LogFileCopy(this ILogger logger, string source, string destination)
        {
            logger.LogInformation($@"Copying file
    from: {source}
      to: {destination}");
        }

        public static void LogCrossgenArguments(
            this ILogger logger,
            string workingDirectory,
            string assemblyPath,
            string targetPath,
            string arguments)
        {
            logger.LogInformation($@"Crossgen assembly
     workdir: {workingDirectory}
    assembly: {assemblyPath}
      output: {targetPath}
   arguments: {arguments}");
        }

        public static void LogCrossgenResult(this ILogger logger, int exitCode, string targetPath)
        {
            if (exitCode == 0)
            {
                logger.LogInformation($"Native image {targetPath} generated successfully.");
            }
            else
            {
                logger.LogWarning($"Crossgen failed for {targetPath}. Exit code {exitCode}.");
            }
        }

        public static void LogMissingFile(this ILogger logger, string target, string missing)
        {
            logger.LogWarning($@"Missing file in output
    look for: {target}
     missing: {missing}");
        }
    }
}
