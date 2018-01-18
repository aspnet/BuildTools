#!/usr/bin/env pwsh -c
#requires -version 4
[cmdletbinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('default', 'blob', '')]
    [string]$PublishType = 'default',

    # used for PublishType = default
    [string]$NuGetFeed = 'https://dotnet.myget.org/F/aspnetcore-tools/api/v2/package',
    [string]$AzureStorageAccount = 'aspnetcore',

    # used for PublishType = blob
    [string]$PublishBlobFeedUrl,
    [string]$PublishBlobFeedKey,
    [Alias('d')]
    [string]$DotNetHome = $null
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

if (-not $PublishType) {
    Write-Warning "Skipping push because `$PublishType was empty"
    exit 0
}

if ($env:BUILD_IS_PERSONAL) {
    $WhatIfPreference = $true
    Write-Host -ForegroundColor Yellow 'Automatically setting -WhatIf for personal builds'
}

if (!$DotNetHome) {
    $DotNetHome = if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
        elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
        elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
        else { Join-Path $PSScriptRoot '.dotnet'}
}

$ToolsSource = "https://$AzureStorageAccount.blob.core.windows.net/buildtools"
$ConfigFile = Join-Path $PSScriptRoot 'korebuild.json'

# The default publish type for builds on aspnetci
if ($PublishType -eq 'default') {
    Import-Module -Force -Scope Local $PSScriptRoot/files/KoreBuild/KoreBuild.psd1
    Import-Module -Force -Scope Local $PSScriptRoot/scripts/PushNuGet.psm1

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
}
# Publishing for orchestrated builds
elseif ($PublishType -eq 'blob') {
    # Requires that you first build korebuild locally
    Import-Module -Force -Scope Local $PSScriptRoot/obj/korebuild/KoreBuild.psd1

    Set-KoreBuildSettings -ToolsSource $ToolsSource -DotNetHome $DotNetHome -RepoPath $PSScriptRoot -ConfigFile $ConfigFile
    Invoke-KoreBuildCommand 'default-build' '-t:Publish' "-p:PublishBlobFeedUrl=$PublishBlobFeedUrl" "-p:PublishBlobFeedKey=$PublishBlobFeedKey" '-bl:artifacts/logs/push.binlog'
}
else {
    throw "PublishType $PublishType not supported"
}
