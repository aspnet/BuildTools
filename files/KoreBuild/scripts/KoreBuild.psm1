#requires -version 4

Set-StrictMode -Version 2

Import-Module -Force -Scope Local "$PSScriptRoot/common.psm1"

if (Get-Command 'dotnet' -ErrorAction Ignore) {
    $global:dotnet = (Get-Command 'dotnet').Path
}

### constants
Set-Variable 'IS_WINDOWS' -Scope Script -Option Constant -Value $((Get-Variable -Name IsWindows -ValueOnly -ErrorAction Ignore) -or !(Get-Variable -Name IsCoreClr -ValueOnly -ErrorAction Ignore))
Set-Variable 'EXE_EXT' -Scope Script -Option Constant -Value $(if ($IS_WINDOWS) { '.exe' } else { '' })

### setup config

$script:config = @{
    'dotnet.feed.cdn'        = 'https://dotnetcli.azureedge.net/dotnet'
    'dotnet.feed.uncached'   = 'https://dotnetcli.blob.core.windows.net/dotnet'
    'dotnet.feed.credential' = $null
}

if ($env:KOREBUILD_DOTNET_FEED_CDN) {
    $script:config.'dotnet.feed.cdn' = $env:KOREBUILD_DOTNET_FEED_CDN
}
if ($env:KOREBUILD_DOTNET_FEED_UNCACHED) {
    $script:config.'dotnet.feed.uncached' = $env:KOREBUILD_DOTNET_FEED_UNCACHED
}
if ($env:KOREBUILD_DOTNET_FEED_CREDENTIAL) {
    $script:config.'dotnet.feed.credential' = $env:KOREBUILD_DOTNET_FEED_CREDENTIAL
}

# Set required environment variables

# This disables automatic rollforward to C:\Program Files\ and other global locations.
# We want to ensure are tests are running against the exact runtime specified by the project.
$env:DOTNET_MULTILEVEL_LOOKUP = 0

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

    $firstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE

    try {
        Write-Verbose "Building $Path"
        Write-Verbose "dotnet = ${global:dotnet}"

        $makeFileProj = Join-Paths $PSScriptRoot ('..', 'KoreBuild.proj')
        $msbuildArtifactsDir = Join-Paths $Path ('artifacts', 'logs')
        $msBuildResponseFile = Join-Path $msbuildArtifactsDir msbuild.rsp
        $msBuildLogRspFile = Join-Path $msbuildArtifactsDir msbuild.logger.rsp

        $msBuildLogArgument = ""

        if ($VerbosePreference -eq 'Continue' -or $env:KOREBUILD_ENABLE_BINARY_LOG -eq "1") {
            Write-Verbose 'Enabling binary logging'
            $msbuildLogFilePath = Join-Path $msbuildArtifactsDir msbuild.binlog
            $msBuildLogArgument = "/bl:$msbuildLogFilePath"
        }

        $koreBuildVersion = Get-KoreBuildVersion

        $msBuildArguments = @"
/nologo
/m
/nodeReuse:false
/verbosity:minimal
/p:KoreBuildVersion=$koreBuildVersion
/p:SuppressNETCoreSdkPreviewMessage=true
/p:RepositoryRoot="$Path\\"
"$msBuildLogArgument"
"$makeFileProj"
"@

        $MSBuildArgs | ForEach-Object { $msBuildArguments += "`n`"$_`"" }

        if (!(Test-Path $msbuildArtifactsDir)) {
            New-Item -Type Directory $msbuildArtifactsDir | Out-Null
        }

        $msBuildArguments | Out-File -Encoding ASCII -FilePath $msBuildResponseFile

        if ($env:KOREBUILD_TEAMCITY_LOGGER) {
            @"
/noconsolelogger
/verbosity:normal
"/logger:TeamCity.MSBuild.Logger.TeamCityMSBuildLogger,${env:KOREBUILD_TEAMCITY_LOGGER};teamcity"
"@ | Out-File -Encoding ascii -FilePath $msBuildLogRspFile
        }
        else {
            "/clp:Summary" | Out-File -Encoding ascii -FilePath $msBuildLogRspFile
        }

        $noop = ($MSBuildArgs -contains '/t:Noop' -or $MSBuildArgs -contains '/t:Cow')
        Write-Verbose "Noop = $noop"
        if ($noop) {
            $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        }
        else {
            [string[]]$repoTasksArgs = $MSBuildArgs | Where-Object { ($_ -like '-p:*') -or ($_ -like '/p:*') -or ($_ -like '-property:') -or ($_ -like '/property:') }
            $repoTasksArgs += ,"@$msBuildLogRspFile"
            __build_task_project $Path $repoTasksArgs
        }

        Write-Verbose "Invoking msbuild with '$(Get-Content $msBuildResponseFile)'"

        Invoke-MSBuild `@"$msBuildResponseFile" `@"$msBuildLogRspFile"
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
        Write-Warning "Skipping runtime installation because KOREBUILD_SKIP_RUNTIME_INSTALL = 1"
        return
    }

    $scriptPath = `
        if ($IS_WINDOWS) { Join-Path $PSScriptRoot 'dotnet-install.ps1' } `
        else { Join-Path $PSScriptRoot 'dotnet-install.sh' }

    if (!$IS_WINDOWS) {
        & chmod +x $scriptPath
    }

    $version = __get_dotnet_sdk_version

    # Install the main CLI
    if (!(Test-Path (Join-Paths $installDir ('sdk', $version, 'dotnet.dll')))) {
        Write-Verbose "Installing dotnet $version to $installDir"
        & $scriptPath `
            -Version $version `
            -Architecture $arch `
            -InstallDir $installDir `
            -AzureFeed $script:config.'dotnet.feed.cdn' `
            -UncachedFeed $script:config.'dotnet.feed.uncached' `
            -FeedCredential $script:config.'dotnet.feed.credential'
    }
    else {
        Write-Host -ForegroundColor DarkGray ".NET Core SDK $version is already installed. Skipping installation."
    }

    # This is a workaround for https://github.com/Microsoft/msbuild/issues/2914.
    # Currently, the only way to configure the NuGetSdkResolver is with NuGet.config, which is not generally used in aspnet org projects.
    # This project is restored so that it pre-populates the NuGet cache with SDK packages.
    $restorerfile = "$PSScriptRoot/../modules/BundledPackages/BundledPackageRestorer.csproj"
    $restorerfilelock="$env:NUGET_PACKAGES/internal.aspnetcore.sdk/$(Get-KoreBuildVersion)/korebuild.sentinel"
    if ((Test-Path $restorerfile) -and -not (Test-Path $restorerfilelock)) {
        New-Item -ItemType Directory $(Split-Path -Parent $restorerfilelock) -ErrorAction Ignore | Out-Null
        New-Item -ItemType File $restorerfilelock -ErrorAction Ignore | Out-Null
        __exec $global:dotnet msbuild '-t:restore' '-v:q' "$restorerfile"
    }
    # end workaround
}

<#
.SYNOPSIS
Ensure that Dotnet exists.

.DESCRIPTION
Check if a dotnet of at least 2.0.0 exists, and install it if it doesn't.
#>
function Ensure-Dotnet() {
    $dotnetVersion = Get-DotnetMajorVersion
    if ($dotnetVersion -lt 2) {
        Write-Verbose "Ensuring dotnet because $dotnetVersion wasn't >= 2.0.0"
        Install-Tools
    }
}

function Get-DotnetMajorVersion() {
    if (Get-Variable "dotnet" -Scope Global -ErrorAction SilentlyContinue) {
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
    [string]$ConfigFile = $null,
    [switch]$CI) {
    if (!$DotNetHome) {
        $DotNetHome = if ($env:DOTNET_HOME) { $env:DOTNET_HOME } `
            elseif ($CI) { Join-Path $RepoPath '.dotnet'}
            elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dotnet'} `
            elseif ($env:HOME) {Join-Path $env:HOME '.dotnet'}`
            else { Join-Path $RepoPath '.dotnet'}
    }

    if (!$ToolsSource) { $ToolsSource = 'https://aspnetcore.blob.core.windows.net/buildtools' }

    if ($CI) {
        $env:CI = 'true'
        $env:DOTNET_HOME = $DotNetHome
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        $env:NUGET_SHOW_STACK = 'true'
        $env:MSBUILDDEBUGPATH = Join-Paths $RepoPath ('artifacts', 'logs')
        if (-not $env:NUGET_PACKAGES) {
            $env:NUGET_PACKAGES = Join-Paths $RepoPath ('.nuget', 'packages')
        }
    }
    else {
        if (-not $env:NUGET_PACKAGES) {
            $env:NUGET_PACKAGES = Join-Paths $env:USERPROFILE ('.nuget', 'packages')
        }
    }

    $env:NUGET_PACKAGES = $env:NUGET_PACKAGES.TrimEnd('\') + '\'

    $arch = __get_dotnet_arch
    $env:DOTNET_ROOT = if ($IS_WINDOWS) { Join-Path $DotNetHome $arch } else { $DotNetHome }

    $MSBuildType = 'core'
    $toolsets = $Null
    if (Test-Path $ConfigFile) {
        try {
            $config = Get-Content -Raw -Encoding UTF8 -Path $ConfigFile | ConvertFrom-Json
            if (__has_member $config msbuildType) {
                [string] $MSBuildType = $config.msbuildType
            }
            if (__has_member $config toolsets) {
                $toolsets = $config.toolsets
            }
        }
        catch {
            Write-Host -ForegroundColor Red $Error[0]
            Write-Error "$ConfigFile contains invalid JSON."
            exit 1
        }
    }

    # Workaround perpetual issues in node reuse and custom task assemblies
    $env:MSBUILDDISABLENODEREUSE = 1

    $global:KoreBuildSettings = @{
        MSBuildType = $MSBuildType
        ToolsSource = $ToolsSource
        DotNetHome  = $DotNetHome
        RepoPath    = $RepoPath
        CI          = $CI
        Toolsets    = $toolsets
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
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
) {
    if (!(Get-Variable KoreBuildSettings -Scope Global -ErrorAction SilentlyContinue)) {
        throw "Set-KoreBuildSettings must be called before Invoke-KoreBuildCommand."
    }

    $sdkVersion = __get_dotnet_sdk_version
    $korebuildVersion = Get-KoreBuildVersion
    if ($sdkVersion -ne 'latest') {
        "{ `"sdk`": { `n`"version`": `"$sdkVersion`" }`n }" | Out-File (Join-Path $global:KoreBuildSettings.RepoPath 'global.json') -Encoding ascii
    }
    else {
        Write-Verbose "Skipping global.json generation because the `$sdkVersion = $sdkVersion"
    }

    if ($Command -eq "default-build") {
        Install-Tools
        Invoke-RepositoryBuild $global:KoreBuildSettings.RepoPath @Arguments
    }
    elseif ($Command -eq "msbuild") {
        Invoke-RepositoryBuild $global:KoreBuildSettings.RepoPath @Arguments
    }
    elseif ($Command -eq "install-tools") {
        Install-Tools
    }
    elseif ($Command -eq 'noop') {
        Write-Host -ForegroundColor DarkGreen 'Doing nothing because command = noop'
    }
    else {
        Ensure-Dotnet

        $korebuildConsoleDll = Get-KoreBuildConsole

        & dotnet $korebuildConsoleDll `
            --tools-source $global:KoreBuildSettings.ToolsSource `
            --dotnet-home $global:KoreBuildSettings.DotNetHome `
            --repo-path $global:KoreBuildSettings.RepoPath `
            $Command `
            @Arguments
    }
}

#
# Private functions
#

function Get-KoreBuildConsole() {
    return Join-Paths $PSScriptRoot ("..", "tools", "KoreBuild.Console.dll")
}

function Invoke-MSBuild {
    if ($global:KoreBuildSettings.MSBuildType -eq 'full') {
        if (-not (Test-Path 'Variable:\script:msbuild')) {
            $script:msbuild = Get-MSBuildPath
        }
        __exec $script:msbuild @args
    }
    else {
        __exec $global:dotnet msbuild @args
    }
}

function Get-MSBuildPath() {
    if ($env:VSINSTALLDIR) {
        # If running from the VS Developer Command Prompt, use the MSBuild it contains.
        return 'msbuild.exe'
    }

    $vswherePath = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"

    if (-not (Test-Path $vswherePath)) {
        $vswherePath = "$PSScriptRoot/../modules/KoreBuild.Tasks/vswhere.exe"
    }

    if (-not (Test-Path $vswherePath)) {
        # Couldn't use vswhere, return 'msbuild.exe' and let PATH do its thing.
        Write-Verbose "Could not find VSWhere. Relying on MSBuild to exist on PATH"
        return 'msbuild.exe'
    }

    [string[]] $vswhereArgs = @('-latest', '-format', 'json', '-products', '*')

    if ($global:KoreBuildSettings.Toolsets -and (__has_member $global:KoreBuildSettings.Toolsets visualstudio)) {
        $vs = $global:KoreBuildSettings.Toolsets.visualstudio

        if ((__has_member $vs includePrerelease) -and $vs.includePrerelease) {
            $vswhereArgs += '-prerelease'
        }
        if (__has_member $vs minVersion) {
            $vswhereArgs += '-version', $vs.minVersion
        }
        if (__has_member $vs versionRange) {
            $vswhereArgs += '-version', $vs.versionRange
        }
        if (__has_member $vs requiredWorkloads) {
            foreach ($workload in $vs.requiredWorkloads) {
                $vswhereArgs += '-requires', $workload
            }
        }
    }

    Write-Verbose "vswhere = $vswherePath $vswhereArgs"

    $installations = & $vswherePath @vswhereArgs | ConvertFrom-Json

    $latest = $null
    if ($installations) {
        $latest = $installations | Select-Object -first 1
    }

    if ($latest -and (__has_member $latest installationPath)) {
        Write-Host "Detected $($latest.displayName) ($($latest.installationVersion)) in '$($latest.installationPath)'"
        $msbuildVersions = @('Current', '16.0', '15.0')
        foreach ($msbuildVersion in $msbuildVersions) {
            $msbuildPath = Join-Paths $latest.installationPath ('MSBuild', $msbuildVersion, 'Bin', 'msbuild.exe')
            if (Test-Path $msbuildPath) {
                Write-Verbose "Using MSBuild.exe = $msbuildPath"
                return $msbuildPath
            }
        }
    }

    $vsInstallScript = "$($global:KoreBuildSettings.RepoPath)\eng\scripts\InstallVisualStudio.ps1"
    if (Test-Path $vsInstallScript) {
        Write-Host ''
        Write-Host -ForegroundColor Magenta "Run $vsInstallScript to install Visual Studio prerequisites."
        Write-Host ''
    }

    throw 'Could not find an installation of MSBuild which satisfies the requirements specified in korebuild.json.'
}

function __has_member($obj, $name) {
    return [bool](Get-Member -Name $name -InputObject $obj)
}

function __get_dotnet_arch {
    if ($env:KOREBUILD_DOTNET_ARCH) {
        return $env:KOREBUILD_DOTNET_ARCH
    }
    return 'x64'
}

function __get_dotnet_sdk_version {
    if ($env:KOREBUILD_DOTNET_VERSION) {
        Write-Warning "dotnet SDK version overridden by KOREBUILD_DOTNET_VERSION"
        return $env:KOREBUILD_DOTNET_VERSION
    }
    return Get-Content (Join-Paths $PSScriptRoot ('..', 'config', 'sdk.version'))
}

function __build_task_project($RepoPath, [string[]]$msbuildArgs) {
    $taskProj = Join-Paths $RepoPath ('build', 'tasks', 'RepoTasks.csproj')
    $publishFolder = Join-Paths $RepoPath ('build', 'tasks', 'bin', 'publish')

    if (!(Test-Path $taskProj)) {
        return
    }

    $sdkPath = "-p:RepoTasksSdkPath=$(Join-Paths $PSScriptRoot ('..', 'msbuild', 'KoreBuild.RepoTasks.Sdk', 'Sdk'))"

    Invoke-MSBuild $taskProj '-v:m' -restore '-t:Publish' '-p:Configuration=Release' "-p:PublishDir=$publishFolder" -nologo $sdkPath @msbuildArgs
}

function Get-KoreBuildVersion {
    $versionFile = Join-Paths $PSScriptRoot ('..', '.version')
    $version = $null
    if (Test-Path $versionFile) {
        $version = Get-Content $versionFile | Where-Object { $_ -like 'version:*' } | Select-Object -first 1
        if (!$version) {
            Write-Host -ForegroundColor Gray "Failed to parse version from $versionFile. Expected a line that begins with 'version:'"
        }
        else {
            $version = $version.TrimStart('version:').Trim()
        }
    }
    return $version
}

function __show_version_info {
    $version = Get-KoreBuildVersion
    if ($version) {
        Write-Host -ForegroundColor Magenta "Using KoreBuild $version"
    }
}

try {
    # show version info on console when KoreBuild is imported
    __show_version_info
}
catch { }
