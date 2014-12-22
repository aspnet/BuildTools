using NuGet;

namespace NuGetPackageVerifier
{
    public static class PackageIssueFactory
    {
        public static MyPackageIssue AssemblyNotStrongNameSigned(string assemblyPath, int hResult)
        {
            // TODO: Translate common HRESULTS http://blogs.msdn.com/b/yizhang/
            return new MyPackageIssue("SIGN_STRONGNAME", assemblyPath, string.Format("The managed assembly '{0}' in this package is either not signed or is delay signed. HRESULT=0x{1:X}", assemblyPath, hResult), MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue NotSemanticVersion(SemanticVersion version)
        {
            return new MyPackageIssue("VERSION_NOTSEMANTIC",
                    string.Format("Version '{0}' does not follow semantic versioning guidelines.", version), MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue Satellite_PackageSummaryNotLocalized()
        {
            return new MyPackageIssue("LOC_SUMMARY", "Package summary is not localized correctly", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue Satellite_PackageTitleNotLocalized()
        {
            return new MyPackageIssue("LOC_TITLE", "Package title is not localized correctly", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue Satellite_PackageDescriptionNotLocalized()
        {
            return new MyPackageIssue("LOC_DESC", "Package description is not localized correctly", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue RequiredCopyright()
        {
            return RequiredCore("NUSPEC_COPYRIGHT", "Copyright", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue RequiredLicenseUrl()
        {
            return RequiredCore("NUSPEC_LICENSEURL", "License Url", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue RequiredIconUrl()
        {
            return RequiredCore("NUSPEC_ICONURL", "Icon Url", MyPackageIssueLevel.Warning);
        }

        public static MyPackageIssue RequiredTags()
        {
            return RequiredCore("NUSPEC_TAGS", "Tags", MyPackageIssueLevel.Warning);
        }

        public static MyPackageIssue RequiredTitle()
        {
            return RequiredCore("NUSPEC_TITLE", "Title", MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue RequiredSummary()
        {
            return RequiredCore("NUSPEC_SUMMARY", "Summary", MyPackageIssueLevel.Warning);
        }

        public static MyPackageIssue RequiredProjectUrl()
        {
            return RequiredCore("NUSPEC_PROJECTURL", "Project Url", MyPackageIssueLevel.Warning);
        }

        public static MyPackageIssue RequiredRequireLicenseAcceptanceTrue()
        {
            return new MyPackageIssue("NUSPEC_ACCEPTLICENSE", string.Format("NuSpec Require License Acceptance is not set to true"), MyPackageIssueLevel.Error);
        }

        private static MyPackageIssue RequiredCore(string issueId, string attributeName, MyPackageIssueLevel issueLevel)
        {
            return new MyPackageIssue(issueId, string.Format("NuSpec {0} attribute is missing", attributeName), issueLevel);
        }

        public static MyPackageIssue PowerShellScriptNotSigned(string scriptPath)
        {
            return new MyPackageIssue("SIGN_POWERSHELL", scriptPath, string.Format("The PowerShell script '{0}' is not signed.", scriptPath), MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue PEFileNotAuthenticodeSigned(string assemblyPath)
        {
            return new MyPackageIssue("SIGN_AUTHENTICODE", assemblyPath, string.Format("The PE file '{0}' in this package is not authenticode signed.", assemblyPath), MyPackageIssueLevel.Error);
        }

        public static MyPackageIssue AssemblyHasNoDocFile(string assemblyPath)
        {
            return new MyPackageIssue("DOC_MISSING", assemblyPath, string.Format("The assembly '{0}' doesn't have a corresponding XML document file.", assemblyPath), MyPackageIssueLevel.Warning);
        }
    }
}
