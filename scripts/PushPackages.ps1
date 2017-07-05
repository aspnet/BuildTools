#!/usr/bin/env powershell
#requires -version 4
[cmdletbinding(SupportsShouldProcess = $true)]
param (
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsDir,
    [Parameter(Mandatory = $true)]
    [string]$Feed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 1

Import-Module -Force -Scope Local $PSScriptRoot/../sdk/KoreBuild/KoreBuild.psd1

Get-ChildItem "$ArtifactsDir/build/*.nupkg" | Push-NuGetPackages -Feed $Feed
