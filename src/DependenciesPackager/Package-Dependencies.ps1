Param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectJson,
    [Parameter(Mandatory=$true)]
    [string []]$SourceFolders,
    [Parameter(Mandatory=$true)]
    [string []]$FallbackFeeds,
    [Parameter(Mandatory=$true)]
    [string] $PackagesFolder,
    [Parameter(Mandatory=$true)]
    [string] $PackagesVersion,
    [Parameter(Mandatory=$true)]
    [string] $ZipPath,
    [Parameter(Mandatory=$false)]
    [string]$DotnetPath
)

if([string]::IsNullOrEmpty($DotnetPath)){
    $DotnetPath = "dotnet";
}

$restorePath = Join-Path $PackagesFolder $PackagesVersion;

$sources = $SourceFolders | %{ "--source ""$_"""};
$fallbacksources = $FallbackFeeds | %{ "--fallbacksource ""$_"""};
$packages = "--packages ""$restorePath""";

if(Test-Path $restorePath){
    Remove-Item $restorePath -Force -Recurse;
    New-Item $restorePath -ItemType Directory | Out-Null;
}

$arguments = (@("restore",$ProjectJson, $packages, "-v Verbose") + $sources + $fallbackSources);

$restore = Start-Process -NoNewWindow -Wait -FilePath $DotnetPath -ArgumentList $arguments -PassThru;

Get-ChildItem $restorePath -File -Recurse -Exclude "*.dll", "*.sha512" | Remove-Item;

$zipFilesPath = Get-ChildItem $restorePath | select -ExpandProperty FullName;
$zipFullPath = Join-Path $ZipPath "AspNetCore.$PackagesVersion.zip";

Compress-Archive -Path $zipFilesPath -DestinationPath $zipFullPath -CompressionLevel Optimal -Force;