#!/usr/bin/env powershell
#requires -version 4
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Mandatory=$true)]
    [string]$Command,
    [Parameter(Mandatory=$true)]
    [string]$RepoPath,
    [switch]$NoBuild = $false,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

if (!$NoBuild) {
    & .\build.ps1 /t:PackageKoreBuild
}

$toolsSource = "$PSScriptRoot/artifacts/"

& .\scripts\bootstrapper\run.ps1 -Command $Command -Path $RepoPath -s $toolsSource -u @Arguments