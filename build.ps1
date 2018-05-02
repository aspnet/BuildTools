#!/usr/bin/env pwsh
#requires -version 4

<#
.DESCRIPTION
    Builds this repository
.PARAMETER CI
    Treat build as a CI build
.PARAMETER SkipTests
    Skip tests
.PARAMETER DotNetHome
    The location of .NET Core runtimes and build tools
.PARAMETER ToolsSource
    The feed for other build tools
.PARAMETER PackageVersionPropsUrl
    (optional) the url of the package versions props path containing dependency versions.
.PARAMETER AccessTokenSuffix
    (optional) the query string to append to any blob store access for PackageVersionPropsUrl, if any.
.PARAMETER RestoreSources
    (optional) an additional NuGet feed used when restoring this project.
.PARAMETER MSBuildArguments
    Additional MSBuild arguments
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [switch]$CI,
    [switch]$SkipTests,
    [string]$DotNetHome = $null,
    [string]$ToolsSource = 'https://aspnetcore.blob.core.windows.net/buildtools',
    [string]$PackageVersionPropsUrl = $null,
    [string]$AccessTokenSuffix = $null,
    [string]$RestoreSources = $null,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$MSBuildArguments
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 1

if (!$DotNetHome) {
    $DotNetHome = if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
        elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
        elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
        else { Join-Path $PSScriptRoot '.dotnet'}
}

$IntermediateDir = Join-Path $PSScriptRoot 'obj'
$ConfigFile = Join-Path $PSScriptRoot 'korebuild.json'

try {
    Import-Module -Force -Scope Local "$PSScriptRoot/files/KoreBuild/KoreBuild.psd1"

    if ($PackageVersionPropsUrl) {
        $PropsFilePath = Join-Path $IntermediateDir 'external-dependencies.props'
        New-Item -ItemType Directory $IntermediateDir -ErrorAction Ignore | Out-Null
        Invoke-WebRequest "${PackageVersionPropsUrl}${AccessTokenSuffix}" -OutFile $PropsFilePath -UseBasicParsing
        $MSBuildArguments += "-p:DotNetPackageVersionPropsPath=$PropsFilePath"
    }

    if ($SkipTests) {
        $MSBuildArguments += '-p:SkipTests=true'
    }

    if ($RestoreSources) {
        $MSBuildArguments += "-p:DotNetRestoreSources=$RestoreSources"
    }

    Set-KoreBuildSettings -ToolsSource $ToolsSource -DotNetHome $DotNetHome -RepoPath $PSScriptRoot -ConfigFile $ConfigFile -CI:$CI
    Invoke-KoreBuildCommand "default-build" @MSBuildArguments
}
finally {
    Remove-Module 'KoreBuild' -ErrorAction Ignore
}
