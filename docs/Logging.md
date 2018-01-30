Logging
-------

KoreBuild produces log files to $(RepositoryRoot)/artifacts/logs. The following log formats can be used:

## Binary Logger

Using `build.cmd -Verbose` will produce a binary log file to artifacts/logs/msbuild.binlog.

See <http://msbuildlog.com> for details.

## TeamCity Logger

KoreBuild can produce log messages for TeamCity by using <https://github.com/JetBrains/TeamCity.MSBuild.Logger>.

To configure this,

1. Download the logger from JetBrains. https://github.com/JetBrains/TeamCity.MSBuild.Logger#download
2. Install this on CI agents.
3. Set the environment variable `KOREBUILD_TEAMCITY_LOGGER` to the file path of TeamCity.MSBuild.Logger.dll on CI agents.
