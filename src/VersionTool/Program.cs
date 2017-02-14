using Microsoft.Extensions.CommandLineUtils;

namespace VersionTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();

            var listCommand = app.Command("list", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);

                c.HelpOption("-h|--help");

                c.OnExecute(() => ListCommand.Execute(pathOption));
            });

            var updateVersionCommand = app.Command("update-version", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var versionArgument = c.Argument("version", "the version to set");

                c.HelpOption("-h|--help");

                c.OnExecute(() => UpdateVersionCommand.Execute(pathOption, matchingOption, versionArgument));
            });

            var listDepdendency = app.Command("list-dependency", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var dependencyArgument = c.Argument("dependency", "the dependency package name");

                c.HelpOption("-h|--help");

                c.OnExecute(() => ListDependencyCommand.Execute(pathOption, matchingOption, dependencyArgument));
            });

            var updateDependency = app.Command("update-dependency", (c) =>
            {
                var pathOption = c.Option("-p|--path", "path to search for projects", CommandOptionType.MultipleValue);
                var matchingOption = c.Option("-m|--matching", "current version to match", CommandOptionType.MultipleValue);

                var dependencyArgument = c.Argument("dependency", "the dependency package name");
                var versionArgument = c.Argument("version", "the dependency version");

                c.HelpOption("-h|--help");

                c.OnExecute(() => UpdateDependencyCommand.Execute(pathOption, matchingOption, dependencyArgument, versionArgument));
            });

            var updatePatch = app.Command("update-patch", (c) =>
            {
                var pathOption = c.Option("-d|--directory", "Directory containing all repos", CommandOptionType.SingleValue);
                var updatePatchConfigOption = c.Option("-u|--update-patchconfig", "Update patchconfig with all packages found and their current versions", CommandOptionType.NoValue);
                var updatePackageOption = c.Option("-p|--update-package", "Update the given package before patch following the format <package>:<version>", CommandOptionType.MultipleValue);
                var updateRepoOption = c.Option("-r|--update-repo", "Update the packages in the given repo before patch ", CommandOptionType.MultipleValue);

                var patchConfig = c.Argument("patchConfig", "Path to the configuration file for patch update");

                c.HelpOption("-h|--help");

                c.OnExecute(() => UpdatePatchCommand.Execute(pathOption, updatePatchConfigOption, updatePackageOption, updateRepoOption, patchConfig));
            });

            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            app.Execute(args);
        }
    }
}
