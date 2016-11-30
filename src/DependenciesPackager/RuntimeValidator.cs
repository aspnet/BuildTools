using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    public class RuntimeValidator
    {
        private static readonly ISet<string> _runtimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "win7-x86",
            "win7-x64",
            "ubuntu.14.04-x64",
            "ubuntu.16.04-x64",
            "debian.8-x64"
        };

        public static bool IsValid(IEnumerable<string> runtimes, ILogger logger)
        {
            foreach (var runtime in runtimes)
            {
                if (!_runtimes.Contains(runtime))
                {
                    logger.LogError($"Invalid runtime {runtime}");
                    return false;
                }
            }

            return true;
        }
    }
}
