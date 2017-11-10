#!/usr/bin/env powershell
#requires -version 4
[cmdletbinding(SupportsShouldProcess = $true)]
param(
    [string]$NuGetFeed = 'https://dotnet.myget.org/F/aspnetcore-tools/api/v2/package',
    [string]$AzureStorageAccount = 'aspnetcore'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

if ($env:BUILD_IS_PERSONAL) {
    $WhatIfPreference = $true
    Write-Host -ForegroundColor Yellow 'Automatically setting -WhatIf for personal builds'
}

Import-Module -Force -Scope Local $PSScriptRoot/files/KoreBuild/KoreBuild.psd1
Import-Module -Force -Scope Local $PSScriptRoot/scripts/Push.psm1

$artifacts = Join-Path $PSScriptRoot 'artifacts'

Get-ChildItem "$artifacts/build/*.nupkg" | Push-NuGetPackage -Feed $NuGetFeed -ApiKey $env:APIKEY -WhatIf:$WhatIfPreference

$settings = [xml] (Get-Content (Join-Path $PSScriptRoot 'version.props'))
$channelName = $settings.Project.PropertyGroup.KoreBuildChannel

Write-Host "Pushing azure artifacts for '$channelName' channel"

& "$PSScriptRoot/scripts/UploadBlobs.ps1" `
    -Channel $channelName `
    -ArtifactsDir $artifacts `
    -AzureStorageAccount $AzureStorageAccount `
    -WhatIf:$WhatIfPreference
