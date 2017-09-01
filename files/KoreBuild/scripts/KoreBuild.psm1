#requires -version 4

Set-StrictMode -Version 2

Import-Module -Force -Scope Local "$PSScriptRoot/common.psm1"

if (Get-Command 'dotnet' -ErrorAction Ignore) {
    $global:dotnet = (Get-Command 'dotnet').Path
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

        # Generate global.json to ensure the repo uses the right SDK version
        $sdkVersion = __get_dotnet_sdk_version
        if ($sdkVersion -ne 'latest') {
            "{ `"sdk`": { `"version`": `"$sdkVersion`" } }" | Out-File (Join-Path $Path 'global.json') -Encoding ascii
        } else {
            Write-Verbose "Skipping global.json generation because the `$sdkVersion = $sdkVersion"
        }

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

        $noop = ($MSBuildArgs -contains '/t:Noop' -or $MSBuildArgs -contains '/t:Cow')
        Write-Verbose "Noop = $noop"
        $firstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
        if ($noop) {
            $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        }
        else {
            __build_task_project $Path
        }

        Write-Verbose "Invoking msbuild with '$(Get-Content $msBuildResponseFile)'"

        __exec $global:dotnet msbuild `@"$msBuildResponseFile"
    }
    finally {
        Pop-Location
        $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $firstTime
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
    [Parameter(Mandatory = $false)]
    [string] $ToolsSource = $global:KoreBuildSettings.ToolsSource,
    [Parameter(Mandatory = $false)]
    [string] $DotNetHome = $global:KoreBuildSettings.DotNetHome) {

    $ErrorActionPreference = 'Stop'
    if (-not $PSBoundParameters.ContainsKey('Verbose')) {
        $VerbosePreference = $PSCmdlet.GetVariableValue('VerbosePreference')
    }

    if (!(Test-Path $DotNetHome)) {
        New-Item -ItemType Directory $DotNetHome | Out-Null
    }

    $DotNetHome = Resolve-Path $DotNetHome
    $arch = __get_dotnet_arch
    $installDir = if ($IS_WINDOWS) { Join-Path $DotNetHome $arch } else { $DotNetHome }
    Write-Verbose "Installing tools to '$installDir'"
    if ($env:DOTNET_INSTALL_DIR -and $env:DOTNET_INSTALL_DIR -ne $installDir) {
        # DOTNET_INSTALL_DIR is used by dotnet-install.ps1 only, and some repos used it in their automation to isolate dotnet.
        # DOTNET_HOME is used by the rest of our KoreBuild tools and is set by the bootstrappers.
        Write-Verbose "installDir = $installDir"
        Write-Verbose "DOTNET_INSTALL_DIR = ${env:DOTNET_INSTALL_DIR}"
        Write-Warning 'The environment variable DOTNET_INSTALL_DIR is deprecated. The recommended alternative is DOTNET_HOME.'
    }

    $global:dotnet = Join-Path $installDir "dotnet$EXE_EXT"

    $dotnetOnPath = Get-Command dotnet -ErrorAction Ignore
    if ($dotnetOnPath -and ($dotnetOnPath.Path -ne $global:dotnet)) {
        $dotnetDir = Split-Path -Parent $global:dotnet
        Write-Warning "dotnet found on the system PATH is '$($dotnetOnPath.Path)' but KoreBuild will use '${global:dotnet}'."
        Write-Warning "Adding '$dotnetDir' to system PATH permanently may be required for applications like Visual Studio or VS Code to work correctly."
    }

    $pathPrefix = Split-Path -Parent $global:dotnet
    if ($env:PATH -notlike "${pathPrefix};*") {
        # only prepend if PATH doesn't already start with the location of dotnet
        Write-Host "Adding $pathPrefix to PATH"
        $env:PATH = "$pathPrefix;$env:PATH"
    }

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

    if ($runtimeVersion) {
        __install_shared_runtime $scriptPath $installDir -arch $arch -version $runtimeVersion -channel $runtimeChannel
    }

    # Install the main CLI
    if (!(Test-Path (Join-Paths $installDir ('sdk', $version, 'dotnet.dll')))) {
        Write-Verbose "Installing dotnet $version to $installDir"
        & $scriptPath `
            -Channel $channel `
            -Version $version `
            -Architecture $arch `
            -InstallDir $installDir
    }
    else {
        Write-Host -ForegroundColor DarkGray ".NET Core SDK $version is already installed. Skipping installation."
    }
}

<#
.SYNOPSIS
Ensure that Dotnet exists.

.DESCRIPTION
Check if a dotnet of at least 2.0.0 exists, and install it if it doesn't.
#>
function Ensure-Dotnet()
{
    $dotnetVersion = Get-DotnetMajorVersion
    if($dotnetVersion -lt 2)
    {
        Write-Verbose "Ensuring dotnet because $dotnetVersion wasn't >= 2.0.0"
        Install-Tools
    }
}

function Get-DotnetMajorVersion() {
    if(Get-Variable "dotnet" -Scope Global -ErrorAction SilentlyContinue)
    {
        $infoOutput = dotnet --version

        $version = $infoOutput.SubString(0, $infoOutput.IndexOf('.'))
        $versionInt = [convert]::ToInt32($version, 10)
        return $versionInt
    }
    else {
        return 0
    }
}

<#
.SYNOPSIS
Set the settings.

.DESCRIPTION
Set the settings which will be used by other commands

.PARAMETER ToolsSource
The base url where build tools can be downloaded.

.PARAMETER DotNetHome
The directory where tools will be stored on the local machine.

.PARAMETER RepoPath
The directory to execute the command against.

.PARAMETER ConfigFile
The korebuild.json file. (Ignored right now: may be used in the future)
#>
function Set-KoreBuildSettings(
    [Parameter()]
    [string]$ToolsSource,
    [Parameter()]
    [string]$DotNetHome,
    [Parameter()]
    [string]$RepoPath,
    [Parameter()]
    [string]$ConfigFile = $null)
{
    if (!$DotNetHome) {
        $DotNetHome = if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
            elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
            elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
            else { Join-Path $PSScriptRoot '.dotnet'}
    }

    if (!$ToolsSource) { $ToolsSource = 'https://aspnetcore.blob.core.windows.net/buildtools' }

    $global:KoreBuildSettings = @{
        ToolsSource = $ToolsSource
        DotNetHome = $DotNetHome
        RepoPath = $RepoPath
    }
}

<#
.SYNOPSIS
Execute the given command.

.PARAMETER Command
The command to be executed.

.PARAMETER Arguments
Arguments to be passed to the command.

.EXAMPLE
Invoke-KoreBuildCommand "docker-build" /t:Package

.NOTES
Set-KoreBuildSettings must be called before Invoke-KoreBuildCommand.
#>
function Invoke-KoreBuildCommand(
    [Parameter(Mandatory=$true)]
    [string]$Command,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)
{
    if(!(Get-Variable KoreBuildSettings -Scope Global -ErrorAction SilentlyContinue))
    {
        throw "Set-KoreBuildSettings must be called before Invoke-KoreBuildCommand."
    }

    if($Command -eq "default-build")
    {
        Install-Tools
        Invoke-RepositoryBuild $global:KoreBuildSettings.RepoPath @Arguments
    }
    elseif($Command -eq "msbuild")
    {
        Invoke-RepositoryBuild $global:KoreBuildSettings.RepoPath @Arguments
    }
    elseif ($Command -eq "install-tools") {
        Install-Tools
    }
    else {
        Ensure-Dotnet

        $korebuildConsoleDll = Get-KoreBuildConsole

        & dotnet $korebuildConsoleDll $Command `
            --tools-source $global:KoreBuildSettings.ToolsSource `
            --dotnet-home $global:KoreBuildSettings.DotNetHome `
            --repo-path $global:KoreBuildSettings.RepoPath `
            @Arguments
    }
}

function Get-KoreBuildConsole()
{
    return Join-Paths $PSScriptRoot ("..", "tools", "KoreBuild.Console.dll")
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

function __get_dotnet_arch {
    if ($env:KOREBUILD_DOTNET_ARCH) {
        return $env:KOREBUILD_DOTNET_ARCH
    }
    return 'x64'
}

function __install_shared_runtime($installScript, $installDir, [string]$arch, [string] $version, [string] $channel) {
    $sharedRuntimePath = Join-Paths $installDir ('shared', 'Microsoft.NETCore.App', $version)
    # Avoid redownloading the CLI if it's already installed.
    if (!(Test-Path $sharedRuntimePath)) {
        Write-Verbose "Installing .NET Core runtime $version"
        & $installScript `
            -Channel $channel `
            -SharedRuntime `
            -Version $version `
            -Architecture $arch `
            -InstallDir $installDir
    }
    else {
        Write-Host -ForegroundColor DarkGray ".NET Core runtime $version is already installed. Skipping installation."
    }
}

function __get_dotnet_sdk_version {
    if ($env:KOREBUILD_DOTNET_VERSION) {
        return $env:KOREBUILD_DOTNET_VERSION
    }
    return Get-Content (Join-Paths $PSScriptRoot ('..', 'config', 'sdk.version'))
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

function __show_version_info {
    $versionFile = Join-Paths $PSScriptRoot ('..', '.version')
    if (Test-Path $versionFile) {
        $version = Get-Content $versionFile | Where-Object { $_ -like 'version:*' } | Select-Object -first 1
        if (!$version) {
            Write-Host -ForegroundColor Gray "Failed to parse version from $versionFile. Expected a line that begins with 'version:'"
        }
        else {
            $version = $version.TrimStart('version:').Trim()
            Write-Host -ForegroundColor Magenta "Using KoreBuild $version"
        }
    }
}

try {
    # show version info on console when KoreBuild is imported
    __show_version_info
}
catch { }
