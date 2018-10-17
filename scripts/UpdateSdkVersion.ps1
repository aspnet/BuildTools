#!/usr/bin/env pwsh
#requires -version 4

<#
.SYNOPSIS
The goal of the script is to make it easier for CI to automatically update and make changes to this repository, without needing
to know the internal details of how config files are layed out in this repo.
#>
[cmdletbinding(SupportsShouldProcess = $true, PositionalBinding = $false)]
param(
    [Parameter()]
    [string]$Version = $null,
    [string[]]$GitCommitArgs = @(),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$git = Get-Command git -ErrorAction Ignore

if (!$git) {
    Write-Warning 'Missing recommmended command: git'
}

$updates = @()

if ($Version -and $PSCmdlet.ShouldProcess("Update dotnet SDK to $Version")) {
    $path = "$PSScriptRoot/../files/KoreBuild/config/sdk.version"
    $currentVersion = (Get-Content -path $path -Encoding Ascii).Trim()
    if ($currentVersion -ne $Version) {
        $Version | Set-Content -path $path -Encoding Ascii
        if ($git) { & git add $path }
        $updates += "SDK to $Version"
    }
}

$message = "Updating $($updates -join ' and ')"
if ($updates -and $git -and ($Force -or $PSCmdlet.ShouldContinue("Commit: $message", "Create a new commit with these changes?"))) {
    & $git commit -m $message @GitCommitArgs
}
elseif (!$updates) {
    Write-Warning "No changes made"
}
