KoreBuild
=========

This is a set of tools for building a repository with MSBuild. It is designed for use with ASP.NET Core projects.

Layout
------

File/Folder                 | Purpose
----------------------------|--------------
.version                    | Contains the current version of korebuild. For diagnostics and logging.
KoreBuild.psd1              | PowerShell module for executing korebuild.
KoreBuild.sh                | Bash module for executing korebuild. (Should be `source`'d).
config/                     | Contains configuration data.
scripts/                    | Bash/PowerShell scripts
modules/                    | Extensions to the KoreBuild lifecycle.
