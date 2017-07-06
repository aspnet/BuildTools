#!/usr/bin/env powershell
#requires -version 4

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
    [string]$Path = $PSScriptRoot,
    [Alias('c')]
    [string]$Channel = 'rel/2.0.0',
    [Alias('d')]
    [string]$DotNetHome,
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

function Get-KoreBuild {

    $lockFile = Join-Path $Path 'korebuild-lock.txt'

    if (!(Test-Path $lockFile) -or $Update) {
        Get-RemoteFile "$ToolsSource/korebuild/channels/$Channel/latest.txt" $lockFile
    }

    $version = Get-Content $lockFile -Tail 1
    $korebuildPath = Join-Paths $DotNetHome ('buildtools', 'korebuild', $version)

    if (!(Test-Path $korebuildPath)) {
        Write-Host -ForegroundColor Magenta "Downloading KoreBuild $version"
        New-Item -ItemType Directory -Path $korebuildPath | Out-Null
        $remotePath = "$ToolsSource/korebuild/artifacts/$version/korebuild.$version.zip"

        try {
            $tmpfile = Join-Path ([IO.Path]::GetTempPath()) "KoreBuild-$([guid]::NewGuid()).zip"
            Get-RemoteFile $remotePath $tmpfile
            if (Get-Command -Name 'Expand-Archive' -ErrorAction Ignore) {
                # Use built-in commands where possible as they are cross-plat compatible
                Expand-Archive -Path $tmpfile -DestinationPath $korebuildPath
            }
            else {
                # Fallback to old approach for old installations of PowerShell
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                [System.IO.Compression.ZipFile]::ExtractToDirectory($tmpfile, $korebuildPath)
            }
        }
        finally {
            remove-item $tmpfile -ErrorAction Ignore
        }
    }

    return $korebuildPath
}

function Join-Paths([string]$path, [string[]]$childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}

function Get-RemoteFile([string]$RemotePath, [string]$LocalPath) {
    if ($RemotePath -notlike 'http*') {
        Copy-Item $RemotePath $LocalPath
        return
    }

    $retries = 10
    while ($retries -gt 0) {
        $retries -= 1
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $RemotePath -OutFile $LocalPath
            break
        }
        catch {
            if ($retries -le 0) {
                Write-Error "Download failed: '$RemotePath'."
            }
            else {
                Write-Verbose "Request failed. $retries retries remaining"
            }
        }
    }
}

#
# Main
#

if (!$DotNetHome) {
    $DotNetHome = if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
        elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
        elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
        else { Join-Path $PSScriptRoot '.dotnet'}
}

$korebuildPath = Get-KoreBuild
Import-Module -Force -Scope Local (Join-Path $korebuildPath 'KoreBuild.psd1')

try {
    Install-Tools $ToolsSource $DotNetHome
    Invoke-RepositoryBuild $Path @MSBuildArgs
}
finally {
    Remove-Module 'KoreBuild' -ErrorAction Ignore
}
