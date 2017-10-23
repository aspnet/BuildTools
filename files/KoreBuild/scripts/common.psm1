function __exec($_cmd) {
    $cmdName = [IO.Path]::GetFileName($_cmd)

    Write-Host -ForegroundColor Cyan ">>> $cmdName $args"
    $originalErrorPref = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $_cmd @args
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $originalErrorPref
    if ($exitCode -ne 0) {
        throw "$cmdName failed with exit code: $exitCode"
    }
    else {
        Write-Verbose "<<< $cmdName [$exitCode]"
    }
}

function Join-Paths($path, $childPaths) {
    $childPaths | ForEach-Object { $path = Join-Path $path $_ }
    return $path
}
