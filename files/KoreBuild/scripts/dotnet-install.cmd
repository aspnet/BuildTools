SET Args=%*
SET Args=%Args:"='%
PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%~dp0dotnet-install.ps1' %Args%; exit $LASTEXITCODE"
