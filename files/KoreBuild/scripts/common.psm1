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

function Join-Paths($path, $childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}
