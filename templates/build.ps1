#!/usr/bin/env powershell
#requires -version 5

<#
.SYNOPSIS
Build this repository

.DESCRIPTION
Downloads korebuild if required. Then builds the repository.

.PARAMETER Path
The folder to build. Defaults to the folder containing this script.

.PARAMETER Channel
The channel of KoreBuild to download.

.PARAMETER DotNetHome
The directory where .NET Core tools will be stored.

.PARAMETER ToolsSource
The base url where build tools can be downloaded.

.PARAMETER Update
Updates KoreBuild to the latest version even if a lock file is present.

.PARAMETER MSBuildArgs
Arguments to be passed to MSBuild

.NOTES
This function will create a file $PSScriptRoot/korebuild-lock.txt. This lock file can be committed to source, but does not have to be.
When the lockfile is not present, KoreBuild will create one using latest available version from $Channel.

#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Alias('p')]
    [string]$Path = $PSScriptRoot,
    [Alias('c')]
    [string]$Channel = 'dev',
    [Alias('d')]
    [string]$DotNetHome = $(`
            if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
            elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
            elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
            else { Join-Path $PSScriptRoot '.dotnet'} ),
    [Alias('s')]
    [string]$ToolsSource = 'https://aspnetcore.blob.core.windows.net/buildtools',
    [Alias('u')]
    [switch]$Update,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$MSBuildArgs
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

#
# Functions
#

function __get_korebuild {

    $lockFile = Join-Path $PSScriptRoot 'korebuild-lock.txt'

    if (!(Test-Path $lockFile) -or $Update) {
        __fetch "$ToolsSource/korebuild/channels/$Channel/latest.txt" $lockFile
    }

    $version = Get-Content $lockFile -Tail 1
    $korebuildPath = __join_paths $DotNetHome ('buildtools', 'korebuild', $version)

    if (!(Test-Path $korebuildPath)) {
        Write-Host "Downloading KoreBuild $version"
        New-Item -ItemType Directory -Path $korebuildPath | Out-Null
        $remotePath = "$ToolsSource/korebuild/artifacts/$version/korebuild.$version.zip"
        $tmpfile = "$(New-TemporaryFile).zip"
        try {
            __fetch $remotePath $tmpfile
            Expand-Archive -Path $tmpfile -DestinationPath $korebuildPath
        }
        finally {
            remove-item $tmpfile -ErrorAction Ignore
        }
    }

    return $korebuildPath
}

function __join_paths($path, $childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}

function __fetch($RemotePath, $LocalPath) {
    if ($RemotePath -notlike 'http*') {
        Copy-Item $RemotePath $LocalPath
        return
    }

    $retries = 10
    while ($retries -gt 0) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $RemotePath -OutFile $LocalPath
            break
        }
        catch {
            Write-Verbose "Request failed. $retries retries remaining"
        }
        $retries -= 1
    }
}

#
# Main
#

try {
    $korebuildPath = __get_korebuild
    Import-Module -Force -Scope Local (Join-Path $korebuildPath 'KoreBuild.psd1')
    Install-Tools $ToolsSource $DotNetHome
    Invoke-RepositoryBuild $Path $MSBuildArgs
}
finally {
    Remove-Module 'KoreBuild' -ErrorAction Ignore
}
