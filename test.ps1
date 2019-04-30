#requires -version 4
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter()]
    [string]$Command = 'default-build',
    [Parameter(Mandatory = $true)]
    [string]$RepoPath,
    [switch]$NoBuild = $false,
    [switch]$CI = $false,
    [switch]$NoReinstall = $false,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'

# Don't not use this to not build first
if (-not $NoBuild) {
    & git clean .\artifacts -xdf

    & .\build.ps1 '-p:SkipTests=true'
}

[xml] $versionProps = Get-Content "$PSScriptRoot/version.props"
$channel = $versionProps.Project.PropertyGroup.KoreBuildChannel
$toolsSource = "$PSScriptRoot/artifacts/"
$latestFile = Join-Path $toolsSource "korebuild/channels/$channel/latest.txt"
$toolsVersion = $null
foreach ($line in Get-Content $latestFile) {
    $toolsVersion = $line.Split(":")[1]
    break
}

$packageDir = Join-Path $toolsSource "build\"

$Arguments += , "/p:InternalAspNetCoreSdkPackageVersion=$toolsVersion"
$Arguments += , "/p:DotNetRestoreSources=$packageDir"

foreach ($pkg in @(
        "Internal.AspNetCore.Sdk",
        "Internal.AspNetCore.SiteExtension.Sdk")) {

    $pkgRoot = "${env:USERPROFILE}/.nuget/packages/$pkg/$toolsVersion/"
    if (Test-Path $pkgRoot) {
        Remove-Item -Recurse -Force $pkgRoot
    }
}

$Reinstall = -not $NoReinstall

& .\scripts\bootstrapper\run.ps1 -Update -Reinstall:$Reinstall -Command $Command -Path $RepoPath -ToolsSource $toolsSource -Ci:$CI @Arguments
