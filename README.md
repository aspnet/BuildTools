Build Tools
===========

[![AppVeyor build status][appveyor-badge]](https://ci.appveyor.com/project/aspnetci/dnxtools/branch/dev)

[appveyor-badge]: https://img.shields.io/appveyor/ci/aspnetci/dnxtools/dev.svg?label=appveyor&style=flat-square

Utilities used in the build system for projects that are used with ASP.NET Core and Entity Framework Core.

This project is part of ASP.NET Core. You can find samples, documentation and getting started instructions for ASP.NET Core at the [Home](https://github.com/aspnet/home) repo.

## Utilities

### KoreBuild

Channel      | Latest Build
-------------|----------------
dev          | ![badge][dev-badge]
rel/2.0.0    | ![badge][rel-2.0.0-badge]

[dev-badge]: https://aspnetcore.blob.core.windows.net/buildtools/korebuild/channels/dev/badge.svg
[rel-2.0.0-badge]: https://aspnetcore.blob.core.windows.net/buildtools/korebuild/channels/rel/2.0.0/badge.svg

This tool contains build scripts, console tools, MSBuild targets, and other settings required to build ASP.NET Core.

#### KoreBuild commands

Previously repositories were runable in only one way, by doing `.\build.cmd`. KoreBuild commands allow you to take wider and more granular actions.

Command      | Purpose    | Example
-------------|------------|----------
install-tools| Installs dotnet, CLI and Shared runtimes. | .\run.ps1 install-tools
docker-build | Runs the build inside docker. | .\run.ps1 docker-build {jessie\|winservercore} /t:SomeTarget /p:Parameters
default-build| Runs install-tools followed by msbuild (like build.cmd used to). | .\run.ps1 default-build /t:SomeTarget /p:Parameters
msbuild      | Runs the build normally. | .\run.ps1 msbuild /t:SomeTarget /p:Parameters

### Local testing
To test changes to this project locally we recomend you do:
```
./test.ps1 -Command $CommandToTest -RepoPath C:\repo\to\test\against\
```
