using Microsoft.Extensions.CommandLineUtils;

namespace RuntimeStoreVerifier
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            var exclusionFileOption = app.Option("-e|--exclusion-file", "Path to the exclusions file for the runtime store verifier.", CommandOptionType.SingleValue);
            var verboseOption = app.Option("-v|--verbose", "Enable verbose output and log all files checked. By default only unsigned binaries are logged.", CommandOptionType.NoValue);

            var pathArgument = app.Argument("directory", "Directory containing the unzipped runtime stores.");

            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                return RuntimeStoreVerifierCommand.Execute(exclusionFileOption, verboseOption, pathArgument);
            });

            return app.Execute(args);
        }
    }
}
