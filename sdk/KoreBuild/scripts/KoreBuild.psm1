#requires -version 4

Set-StrictMode -Version 2

if (Get-Command 'dotnet' -ErrorAction Ignore) {
    $global:dotnet = (Get-Command 'dotnet').Path
}

function Join-Paths($path, $childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}

### constants
Set-Variable 'IS_WINDOWS' -Scope Script -Option Constant -Value $((Get-Variable -Name IsWindows -ValueOnly -ErrorAction Ignore) -or !(Get-Variable -Name IsCoreClr -ValueOnly -ErrorAction Ignore))
Set-Variable 'EXE_EXT' -Scope Script -Option Constant -Value $(if ($IS_WINDOWS) { '.exe' } else { '' })

<#
.SYNOPSIS
Builds a repository

.DESCRIPTION
Invokes the default MSBuild lifecycle on a repostory. This will download any required tools.

.PARAMETER Path
The path to the repository to be compiled

.PARAMETER MSBuildArgs
Arguments to be passed to the main MSBuild invocation

.EXAMPLE
Invoke-RepositoryBuild $PSScriptRoot /p:Configuration=Release /t:Verify

.NOTES
This is the main function used by most repos.
#>
function Invoke-RepositoryBuild(
    [Parameter(Mandatory = $true)]
    [string] $Path,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $MSBuildArgs) {

    $ErrorActionPreference = 'Stop'

    if (-not $PSBoundParameters.ContainsKey('Verbose')) {
        $VerbosePreference = $PSCmdlet.GetVariableValue('VerbosePreference')
    }

    $Path = Resolve-Path $Path
    Push-Location $Path | Out-Null
    try {
        Write-Verbose "Building $Path"
        Write-Verbose "dotnet = ${global:dotnet}"

        $versionFile = Join-Paths $PSScriptRoot ('..', '.version')
        if (Test-Path $versionFile) {
            Write-Host -ForegroundColor Magenta "Using KoreBuild $(Get-Content $versionFile -Tail 1)"
        }

        # Generate global.json to ensure the repo uses the right SDK version
        "{ `"sdk`": { `"version`": `"$(__get_dotnet_sdk_version)`" } }" | Out-File (Join-Path $Path 'global.json') -Encoding ascii

        $makeFileProj = Join-Paths $PSScriptRoot ('..', 'KoreBuild.proj')
        $msbuildArtifactsDir = Join-Paths $Path ('artifacts', 'msbuild')
        $msBuildResponseFile = Join-Path $msbuildArtifactsDir msbuild.rsp

        $msBuildLogArgument = ""

        if ($VerbosePreference -eq 'Continue' -or $env:KOREBUILD_ENABLE_BINARY_LOG -eq "1") {
            Write-Verbose 'Enabling binary logging'
            $msbuildLogFilePath = Join-Path $msbuildArtifactsDir msbuild.binlog
            $msBuildLogArgument = "/bl:$msbuildLogFilePath"
        }

        $msBuildArguments = @"
/nologo
/m
/p:RepositoryRoot="$Path/"
"$msBuildLogArgument"
/clp:Summary
"$makeFileProj"
"@

        $MSBuildArgs | ForEach-Object { $msBuildArguments += "`n`"$_`"" }

        if (!(Test-Path $msbuildArtifactsDir)) {
            New-Item -Type Directory $msbuildArtifactsDir | Out-Null
        }

        $msBuildArguments | Out-File -Encoding ASCII -FilePath $msBuildResponseFile

        __build_task_project $Path

        Write-Verbose "Invoking msbuild with '$(Get-Content $msBuildResponseFile)'"

        __exec $global:dotnet msbuild `@"$msBuildResponseFile"
    }
    finally {
        Pop-Location
    }
}

<#
.SYNOPSIS
Installs tools if required.

.PARAMETER ToolsSource
The base url where build tools can be downloaded.

.PARAMETER DotNetHome
The directory where tools will be stored on the local machine.
#>
function Install-Tools(
    [Parameter(Mandatory = $true)]
    [string]$ToolsSource,
    [Parameter(Mandatory = $true)]
    [string]$DotNetHome) {

    $ErrorActionPreference = 'Stop'
    if (-not $PSBoundParameters.ContainsKey('Verbose')) {
        $VerbosePreference = $PSCmdlet.GetVariableValue('VerbosePreference')
    }

    if (!(Test-Path $DotNetHome)) {
        New-Item -ItemType Directory $DotNetHome | Out-Null
    }

    $DotNetHome = Resolve-Path $DotNetHome
    $installDir = if ($IS_WINDOWS) { Join-Path $DotNetHome 'x64' } else { $DotNetHome }
    $global:dotnet = Join-Path $installDir "dotnet$EXE_EXT"
    $env:PATH = "$(Split-Path -Parent $global:dotnet);$env:PATH"

    if ($env:KOREBUILD_SKIP_RUNTIME_INSTALL -eq "1") {
        Write-Host "Skipping runtime installation because KOREBUILD_SKIP_RUNTIME_INSTALL = 1"
        return
    }

    $scriptPath = `
        if ($IS_WINDOWS) { Join-Path $PSScriptRoot 'dotnet-install.ps1' } `
        else { Join-Path $PSScriptRoot 'dotnet-install.sh' }

    if (!$IS_WINDOWS) {
        & chmod +x $scriptPath
    }

    $channel = "preview"
    $runtimeChannel = "master"
    $version = __get_dotnet_sdk_version
    $runtimeVersion = Get-Content (Join-Paths $PSScriptRoot ('..', 'config', 'runtime.version'))

    if ($env:KOREBUILD_DOTNET_CHANNEL) {
        $channel = $env:KOREBUILD_DOTNET_CHANNEL
    }
    if ($env:KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL) {
        $runtimeChannel = $env:KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL
    }
    if ($env:KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION) {
        $runtimeVersion = $env:KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION
    }

    # Temporarily install these runtimes to prevent build breaks for repos not yet converted
    # 1.0.5 - for tools
    __install_shared_runtime $scriptPath $installDir -version "1.0.5" -channel "preview"
    # 1.1.2 - for test projects which haven't yet been converted to netcoreapp2.0
    __install_shared_runtime $scriptPath $installDir -version "1.1.2" -channel "release/1.1.0"

    if ($runtimeVersion) {
        __install_shared_runtime $scriptPath $installDir -version $runtimeVersion -channel $runtimeChannel
    }

    # Install the main CLI
    Write-Verbose "Installing dotnet $version to $installDir"
    & $scriptPath -Channel $channel `
        -Version $version `
        -Architecture x64 `
        -InstallDir $installDir
}

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
        $packagesToPush = @()
        foreach ($package in $Packages) {
            if ($package -like '*.symbols.nupkg') {
                Write-Host -ForegroundColor DarkCyan "Skipping symbols package: $package"
                continue
            }

            if ($PSCmdlet.ShouldProcess((Split-Path -Leaf $package), "dotnet nuget push")) {
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
                param($dotnet, $feed, $apikey, $package, $remaining)

                $ErrorActionPreference = 'Stop'
                Set-StrictMode -Version Latest

                while ($remaining -ge 0) {
                    $arguments = @()
                    if ($apikey) {
                        $arguments = ('--api-key', $apikey)
                    }

                    try {
                        & $dotnet nuget push `
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
            } -ArgumentList ($global:dotnet, $Feed, $ApiKey, $package, $Retries)
            $jobs += $job
        }
    }

    end {
        $jobs | Wait-Job | Out-Null
        $jobs | Receive-Job
        $jobs | Remove-Job | Out-Null
    }
}

#
# Private functions
#

function __install_shared_runtime($installScript, $installDir, [string] $version, [string] $channel) {
    $sharedRuntimePath = Join-Paths $installDir ('shared', 'Microsoft.NETCore.App', $version)
    # Avoid redownloading the CLI if it's already installed.
    if (!(Test-Path $sharedRuntimePath)) {
        Write-Verbose "Installing .NET Core runtime $version"
        & $installScript -Channel $channel `
            -SharedRuntime `
            -Version $version `
            -Architecture x64 `
            -InstallDir $installDir
    }
}

function __get_dotnet_sdk_version {
    if ($env:KOREBUILD_DOTNET_VERSION) {
        return $env:KOREBUILD_DOTNET_VERSION
    }
    return Get-Content (Join-Paths $PSScriptRoot ('..', 'config', 'sdk.version'))
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
        Write-Verbose "<<< $cmdName [$exitCode]"
    }
}

function __build_task_project($RepoPath) {
    $taskProj = Join-Paths $RepoPath ('build', 'tasks', 'RepoTasks.csproj')
    $publishFolder = Join-Paths $RepoPath ('build', 'tasks', 'bin', 'publish')

    if (!(Test-Path $taskProj)) {
        return
    }

    if (Test-Path $publishFolder) {
        Remove-Item $publishFolder -Recurse -Force
    }

    $sdkPath = "/p:RepoTasksSdkPath=$(Join-Paths $PSScriptRoot ('..', 'msbuild', 'KoreBuild.RepoTasks.Sdk', 'Sdk'))"

    __exec $global:dotnet restore $taskProj $sdkPath
    __exec $global:dotnet publish $taskProj --configuration Release --output $publishFolder /nologo $sdkPath
}
