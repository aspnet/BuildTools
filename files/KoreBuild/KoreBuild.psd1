@{
    GUID = 'e572c360-3572-4558-821f-4bed8511a5e2'
    RootModule = 'scripts/KoreBuild.psm1'
    Author = 'ASP.NET Core'
    CompanyName = 'Microsoft'
    Copyright = '.NET Foundation'
    ModuleVersion = '0.1'
    Description = 'Functions for using KoreBuild'
    PowerShellVersion = '4.0'
    FunctionsToExport = @('Install-Tools', 'Set-KoreBuildSettings', 'Invoke-RepositoryBuild', 'Push-NuGetPackage', 'Invoke-KoreBuildCommand')
    AliasesToExport = @('')
    VariablesToExport = @('')
}
