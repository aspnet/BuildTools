<#
.SYNOPSIS
Uploads NuGet packages to a remote feed.

.PARAMETER Feed
The NuGet feed.

.PARAMETER ApiKey
The API key for the NuGet feed.

.PARAMETER Packages
The files to upload

.PARAMETER Retries
The number of times to retry pushing when the `nuget push` command fails.

.PARAMETER MaxParallel
The maxiumum number of parallel pushes to execute.
#>
function Push-NuGetPackage {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Feed,
        [string]$ApiKey,
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string[]] $Packages,
        [int]$Retries = 5,
        [int]$MaxParallel = 4
    )

    begin {
        $ErrorActionPreference = 'Stop'
        if (-not $PSBoundParameters.ContainsKey('Verbose')) {
            $VerbosePreference = $PSCmdlet.GetVariableValue('VerbosePreference')
        }

        if (-not $PSBoundParameters.ContainsKey('WhatIf')) {
            $WhatIfPreference = $PSCmdlet.GetVariableValue('WhatIfPreference')
        }

        if (!$ApiKey) {
            Write-Warning 'The parameter -ApiKey was missing. This may be required to push to the remote feed.'
        }

        if ($Packages | ? { $_ -like '*.symbols.nupkg' }) {
            Write-Warning "Push-NuGetPackage does not yet support pushing symbols packages."
        }

        $jobs = @()
    }

    process {
        if ($env:DOTNET_HOME -ne $null) {
            $dotnetExe = "$env:DOTNET_HOME/x64/dotnet"
        }
        else {
            $dotnetExe = "dotnet"
        }

        $packagesToPush = @()
        foreach ($package in $Packages) {
            if ($package -like '*.symbols.nupkg') {
                Write-Host -ForegroundColor DarkCyan "Skipping symbols package: $package"
                continue
            }

            if ($PSCmdlet.ShouldProcess((Split-Path -Leaf $package), "$dotnetExe nuget push")) {
                $packagesToPush += $package
            }
        }

        foreach ($package in $packagesToPush) {
            $running = $jobs | ? { $_.State -eq 'Running' }
            if (($running | Measure-Object).Count -ge $MaxParallel) {
                Write-Debug "Waiting for a job to complete because max parallel pushes is set to $MaxParallel"
                $finished = $running | Wait-Job -Any
                $finished | Receive-Job
            }

            Write-Verbose "Starting job to push $(Split-Path -Leaf $package)"
            $job = Start-Job -ScriptBlock {
                param($feed, $apikey, $package, $remaining)

                $ErrorActionPreference = 'Stop'
                Set-StrictMode -Version Latest

                while ($remaining -ge 0) {
                    $arguments = @()
                    if ($apikey) {
                        $arguments = ('--api-key', $apikey)
                    }

                    try {
                        & $Using:dotnetExe nuget push `
                            $package `
                            --source $feed `
                            --timeout 300 `
                            @arguments

                        if ($LASTEXITCODE -ne 0) {
                            throw "Exit code $LASTEXITCODE. Failed to push $package."
                        }
                        break
                    }
                    catch {
                        if ($remaining -le 0) {
                            throw
                        }
                        else {
                            Write-Host "Push failed. Retries left $remaining"
                        }
                    }
                    finally {
                        $remaining--
                    }
                }
            } -ArgumentList ($Feed, $ApiKey, $package, $Retries)
            $jobs += $job
        }
    }

    end {
        $jobs | Wait-Job | Out-Null
        $jobs | Receive-Job
        $jobs | Remove-Job | Out-Null
    }
}
