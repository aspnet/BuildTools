Build Tools
===========

[![AppVeyor build status][appveyor-badge]](https://ci.appveyor.com/project/aspnetci/dnxtools/branch/dev)

[appveyor-badge]: https://img.shields.io/appveyor/ci/aspnetci/dnxtools/dev.svg?label=appveyor&style=flat-square

Utilities used in the build system for projects that are used with ASP.NET Core and Entity Framework Core.

This project is part of ASP.NET Core. You can find samples, documentation and getting started instructions for ASP.NET Core at the [Home](https://github.com/aspnet/home) repo.

## Docs

See [docs/Intro.md](./docs/Intro.md).

## Latest build

Channel        | Latest Build
---------------|:---------------
dev            | ![badge][dev-badge]
release/2.1    | ![badge][rel-2.1-badge]
release/2.0    | ![badge][rel-2.0-badge]

[dev-badge]: https://aspnetcore.blob.core.windows.net/buildtools/korebuild/channels/dev/badge.svg
[rel-2.1-badge]: https://aspnetcore.blob.core.windows.net/buildtools/korebuild/channels/release/2.1/badge.svg
[rel-2.0-badge]: https://aspnetcore.blob.core.windows.net/buildtools/korebuild/channels/release/2.0/badge.svg

This tool contains build scripts, console tools, MSBuild targets, and other settings required to build ASP.NET Core.


## Local testing
To test changes to this project locally we recomend you do:

```ps1
./test.ps1 -Command $CommandToTest -RepoPath C:\repo\to\test\against\
```
