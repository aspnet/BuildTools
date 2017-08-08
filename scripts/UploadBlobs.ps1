#!/usr/bin/env powershell
#requires -version 4
[cmdletbinding(SupportsShouldProcess = $true)]
param (
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsDir,
    [Parameter(Mandatory = $true)]
    [string]$Channel,
    [string]$ContainerName = 'buildtools',
    [string]$AzureStorageAccount = $env:AZURE_STORAGE_ACCOUNT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

Import-Module "$PSScriptRoot/../files/KoreBuild/scripts/common.psm1"

## Main

if (!(Get-Command 'az' -ErrorAction Ignore)) {
    Write-Error 'Missing required command: az. Please install the Azure CLI and ensure it is available on PATH.'
}

$korebuildDir = Join-Path (Resolve-Path $ArtifactsDir) 'korebuild'

if (!$AzureStorageAccount) {
    Write-Error 'Expected -AzureStorageAccount or $env:AZURE_STORAGE_ACCOUNT to be set'
}

if (!($env:AZURE_STORAGE_SAS_TOKEN)) {
    Write-Warning 'Expected $env:AZURE_STORAGE_SAS_TOKEN to be set'
}

if (!(Test-Path $korebuildDir)) {
    Write-Warning "Skipping Azure publish because $korebuildDir does not exist"
    exit 0
}

$globs = (
    @{
        pattern     = 'artifacts/*.zip'
        contentType = 'application/zip'
        otherArgs   = @()
    },
    @{
        pattern     = "channels/$Channel/badge.svg"
        contentType = 'image/svg+xml'
        otherArgs   = ('--content-cache-control', 'no-cache, no-store, must-revalidate')
    },
    @{
        pattern     = "channels/$Channel/latest.txt"
        contentType = 'text/plain'
        otherArgs   = ('--content-cache-control', 'no-cache, no-store, must-revalidate')
    }
)

$globs | ForEach-Object {
    $otherArgs = $_.otherArgs
    if (!(Get-ChildItem -Recurse (Join-Path $korebuildDir $_.pattern) -ErrorAction Ignore)) {
        Write-Warning "Expected files in $korebuildDir/$($_.pattern) but found none"
    }

    if ($PSCmdlet.ShouldProcess("$korebuildDir/$($_.pattern) as $($_.contentType)", "Push to Azure")) {
        __exec az storage blob upload-batch `
            --account-name $AzureStorageAccount `
            --verbose `
            --pattern $_.pattern `
            --content-type $_.contentType `
            --destination "$ContainerName/korebuild" `
            --source $korebuildDir `
            @otherArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Error 'Failed to upload Azure artifacts'
        }
    }
}
