Intro to BuildTools
-------------------

This repo contains console tools, MSBuild tasks, and targets used to build ASP.NET Core.
This document is a high-level overview of how these build tools work.

## Step-by-step how "build.cmd" works

Most KoreBuild repositories will have an identical build.cmd script in the top-level repo directory. This script can be found in [scripts/bootstrapper/build.cmd][build-cmd]. These are the steps the script takes. (The same steps apply to build.sh for Linux builds.)

1. [build.cmd][build-cmd] invokes "[run.ps1][run-ps1] default-build".
1. [run.ps1][run-ps1] downloads and extracts KoreBuild as a zip file
1. [run.ps1][run-ps1] imports the [KoreBuild.psm1][korebuild-psm1] file which contains a few functions for invoking commands. It then invokes `Invoke-KoreBuildCommand 'default-build'`
1. [KoreBuild.psm1][korebuild-psm1] defines the `Invoke-KoreBuildCommand` function. This function will
    1. Ensure dotnet is installed
    1. Build `$RepoRoot/build/tasks/RepoTasks.csproj` if it exists
    1. Starts MSBuild by calling `dotnet msbuild KoreBuild.proj`
1. [KoreBuild.proj][korebuild-proj] is the entry point for building the entire repo in an MSBuild process. By default, this project will restore, compile, package, and test \*.sln files. It has some extensibility points to repos can extend. See [./KoreBuild.md](./KoreBuild.md).


[build-cmd]: ../scripts/bootstrapper/build.cmd
[run-ps1]: ../scripts/bootstrapper/run.ps1
[korebuild-psm1]: ../files/KoreBuild/scripts/KoreBuild.psm1
[korebuild-proj]: ../files/KoreBuild/KoreBuild.proj
[korebuild-common]: ../files/KoreBuild/KoreBuild.proj
