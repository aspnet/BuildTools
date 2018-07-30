#!/usr/bin/env pwsh -c
#requires -version 4
[cmdletbinding(SupportsShouldProcess = $true)]
param (
    [string]$ArtifactsDir = $PSScriptRoot,
    [string]$Channel,
    [string]$ContainerName = 'buildtools',
    [string]$AzureStorageAccount = $env:AZURE_STORAGE_ACCOUNT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2


function Invoke-Block([scriptblock]$cmd) {
    $cmd | Out-String | Write-Verbose
    & $cmd

    # Need to check both of these cases for errors as they represent different items
    # - $?: did the powershell script block throw an error
    # - $lastexitcode: did a windows command executed by the script block end in error
    if ((-not $?) -or ($lastexitcode -ne 0)) {
        if ($error -ne $null)
        {
            Write-Warning $error[0]
        }
        throw "Command failed to execute: $cmd"
    }
}

## Main

if (!(Get-Command 'az' -ErrorAction Ignore)) {
    Write-Error 'Missing required command: az. Please install the Azure CLI and ensure it is available on PATH.'
}

if (-not $Channel) {
    $Channel = Get-Content "$PSScriptRoot/channel.txt" -Raw -ErrorAction Ignore
}

if (-not $Channel) {
    throw 'Could not determine the channel name to publish.'
}

if (!$AzureStorageAccount) {
    Write-Error 'Expected -AzureStorageAccount or $env:AZURE_STORAGE_ACCOUNT to be set'
}

if (!($env:AZURE_STORAGE_SAS_TOKEN)) {
    Write-Warning 'Expected $env:AZURE_STORAGE_SAS_TOKEN to be set'
}

if (!(Test-Path $ArtifactsDir)) {
    Write-Warning "Skipping Azure publish because $ArtifactsDir does not exist"
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

Write-Verbose "Uploading KoreBuild for channel $Channel"

$globs | ForEach-Object {
    $otherArgs = $_.otherArgs
    if (!(Get-ChildItem -Recurse (Join-Path $ArtifactsDir $_.pattern) -ErrorAction Ignore)) {
        Write-Warning "Expected files in $ArtifactsDir/$($_.pattern) but found none"
    }

    if ($PSCmdlet.ShouldProcess("$ArtifactsDir/$($_.pattern) as $($_.contentType)", "Push to Azure")) {
        Invoke-Block { & az storage blob upload-batch `
            --account-name $AzureStorageAccount `
            --verbose `
            --pattern $_.pattern `
            --content-type $_.contentType `
            --destination "$ContainerName/korebuild" `
            --source $ArtifactsDir `
            @otherArgs
        }
    }
}
