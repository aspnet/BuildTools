#!/usr/bin/env powershell
#requires -version 4
param (
    [Parameter(Mandatory = $true)]
    $ArtifactsDir,
    $ContainerName = 'buildtools'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 1

function Join-Paths($path, $childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}

function __exec($cmd) {
    $cmdName = [IO.Path]::GetFileName($cmd)

    Write-Host -ForegroundColor Cyan ">>> $cmdName $args"
    $originalErrorPref = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $cmd @args
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $originalErrorPref
    if ($exitCode -ne 0) {
        Write-Error "$cmdName failed with exit code: $exitCode"
    }
    else {
        Write-Host -ForegroundColor Green "<<< $cmdName [$exitCode]"
    }
}

## Main

if (!(Get-Command 'az' -ErrorAction Ignore)) {
    Write-Error 'Missing required command: az. Please install the Azure CLI and ensure it is available on PATH.'
}

$korebuildDir = Join-Path (Resolve-Path $ArtifactsDir) 'korebuild'

if (!($env:AZURE_STORAGE_ACCOUNT)) {
    Write-Error 'Expected $env:AZURE_STORAGE_ACCOUNT to be set'
}

if (!($env:AZURE_STORAGE_SAS_TOKEN)) {
    Write-Error 'Expected $env:AZURE_STORAGE_SAS_TOKEN to be set'
}

if (!(Test-Path $korebuildDir)) {
    Write-Warning "Skipping Azure publish because $korebuildDir does not exist"
    exit 0
}

$dryrun = ''
if ($env:BUILD_IS_PERSONAL) {
    Write-Host "Running publish as a dryrun for personal builds"
    $dryrun = '--dryrun'
}

$globs = (
    @{
        pattern     = 'artifacts/*.zip'
        contentType = 'application/zip'
    },
    @{
        pattern     = '*/badge.svg'
        contentType = 'image/svg+xml'
        otherArgs   = ('--content-cache-control', 'no-cache, no-store, must-revalidate')
    },
    @{
        pattern     = '*/latest.txt'
        contentType = 'text/plain'
        otherArgs   = ('--content-cache-control', 'no-cache, no-store, must-revalidate')
    }
)

$globs | ForEach-Object {
    $otherArgs = $_.otherArgs
    __exec az storage blob upload-batch `
        $dryrun `
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
