using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.CommandLineUtils;
using WinTrustSources;

namespace RuntimeStoreVerifier
{
    public class RuntimeStoreVerifierCommand
    {
        internal static int Execute(
            CommandOption exclusionFileOption,
            CommandOption verboseOption,
            CommandArgument pathArgument)
        {
            var exclusions = exclusionFileOption.HasValue() ? File.ReadAllLines(exclusionFileOption.Value()) : null;
            var unsignedBinaries = new List<string>();

            foreach (var binary in Directory.GetFiles(pathArgument.Value, "*.dll", SearchOption.AllDirectories))
            {
                if (!WinTrust.IsAuthenticodeSigned(binary))
                {
                    if (BinaryIsExcluded(binary, exclusions))
                    {
                        if (verboseOption.HasValue())
                        {
                            Console.WriteLine($"{binary} is not authenticode signed but is explicitly excluded");
                        }
                    }
                    else
                    {
                        unsignedBinaries.Add(binary);
                    }
                }
                else if (verboseOption.HasValue())
                {
                    Console.WriteLine($"{binary} is authenticode signed");
                }
            }

            if (unsignedBinaries.Count > 0)
            {
                Console.WriteLine("One or more binaries are unsigned:");
                foreach (var binary in unsignedBinaries)
                {
                    Console.WriteLine(binary);
                }

                return 1;
            }

            Console.WriteLine("All binaries are signed.");
            return 0;
        }

        internal static bool BinaryIsExcluded(string binary, IEnumerable<string> exclusions)
        {
            foreach (var exclusion in exclusions)
            {
                if (Regex.Match(new FileInfo(binary).Name, exclusion, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(30)).Success)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
