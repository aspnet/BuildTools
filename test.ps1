#!/usr/bin/env powershell
#requires -version 4
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoPath,
    [switch]$NoBuild = $false,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'

if (!$NoBuild) {
    & $PSScriptRoot\build.ps1 '-p:SkipTests=true'
}

$toolsSource = "$PSScriptRoot/artifacts/"
$latestFile = Join-Path $toolsSource "korebuild/channels/release/2.0/latest.txt"
$toolsVersion = $null
foreach ($line in Get-Content $latestFile) {
    $toolsVersion = $line.Split(":")[1]
    break
}

$packageDir = Join-Path $toolsSource "build\"

$Arguments += , "/p:InternalAspNetCoreSdkPackageVersion=$toolsVersion"
$Arguments += , "/p:DotNetRestoreSources=$packageDir"

& $PSScriptRoot\scripts\bootstrapper\build.ps1 -Update -Reinstall -Path $RepoPath -ToolsSource $toolsSource @Arguments
