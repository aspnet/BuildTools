#!/usr/bin/env powershell
#requires -version 4
[CmdletBinding(PositionalBinding = $true)]
param(
    [Parameter(Mandatory=$true)]
    [string]$Command,
    [Parameter(Mandatory=$true)]
    [string]$RepoPath,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

& .\build.ps1 /t:PackageKoreBuild

$toolsSource = "./artifacts/"

& .\scripts\bootstrapper\run.ps1 -Command $Command -Path $RepoPath -s $toolsSource -u @Arguments