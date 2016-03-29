// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using NuGet.Versioning;

namespace NuGetPackageVerifier
{
    public static class PackageIssueFactory
    {
        public static PackageVerifierIssue AssemblyMissingServicingAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "SERVICING_ATTRIBUTE",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the '[assembly: AssemblyMetadata(""Serviceable"", ""True"")]' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingHashAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "ASSEMBLY_COMMIT_HASH",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the '[assembly: AssemblyMetadata(""CommitHash"", ""<text>"")]' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyHasIncorrectBuildConfiguration(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "WRONG_BUILD_CONFIGURATION",
                assemblyPath,
                string.Format("The assembly '{0}' was not built using 'Release' configuration.", assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingNeutralResourcesLanguageAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "NEUTRAL_RESOURCES_LANGUAGE",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the '[assembly: NeutralResourcesLanguage(""en-us"")]' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingCopyrightAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "ASSEMBLY_COPYRIGHT",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the 'AssemblyCopyright' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingCompanyAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "ASSEMBLY_COMPANY",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the 'AssemblyCompany' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingProductAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "ASSEMBLY_PRODUCT",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the 'AssemblyProduct' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingDescriptionAttribute(string assemblyPath)
        {
            return new PackageVerifierIssue(
                "ASSEMBLY_DESCRIPTION",
                assemblyPath,
                string.Format(
                    @"The managed assembly '{0}' in this package is missing the 'AssemblyDescription' attribute.",
                    assemblyPath),
                PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyMissingFileVersionAttribute(string assemblyPath)
        {
            return AssemblyMissingVersionAttributeCore("VERSION_FILEVERSION", assemblyPath, typeof(AssemblyFileVersionAttribute).Name);
        }

        public static PackageVerifierIssue AssemblyMissingInformationalVersionAttribute(string assemblyPath)
        {
            return AssemblyMissingVersionAttributeCore("VERSION_INFORMATIONALVERSION", assemblyPath, typeof(AssemblyInformationalVersionAttribute).Name);
        }

        private static PackageVerifierIssue AssemblyMissingVersionAttributeCore(string issueId, string assemblyPath, string attributeName)
        {
            return new PackageVerifierIssue(issueId, assemblyPath, string.Format("The managed assembly '{0}' in this package is missing the '{1}' attribute.", assemblyPath, attributeName), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyNotStrongNameSigned(string assemblyPath, int hResult)
        {
            // TODO: Translate common HRESULTS http://blogs.msdn.com/b/yizhang/
            return new PackageVerifierIssue("SIGN_STRONGNAME", assemblyPath, string.Format("The managed assembly '{0}' in this package is either not signed or is delay signed. HRESULT=0x{1:X}", assemblyPath, hResult), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyHasWrongPublicKeyToken(string assemblyPath, string expectedToken)
        {
            return new PackageVerifierIssue("WRONG_PUBLICKEYTOKEN", assemblyPath, string.Format("The managed assembly '{0}' in this package does not have the expected public key token ({1}).", assemblyPath, expectedToken), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue NotSemanticVersion(SemanticVersion version)
        {
            return new PackageVerifierIssue("VERSION_NOTSEMANTIC",
                    string.Format("Version '{0}' does not follow semantic versioning guidelines.", version), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue Satellite_PackageSummaryNotLocalized()
        {
            return new PackageVerifierIssue("LOC_SUMMARY", "Package summary is not localized correctly", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue Satellite_PackageTitleNotLocalized()
        {
            return new PackageVerifierIssue("LOC_TITLE", "Package title is not localized correctly", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue Satellite_PackageDescriptionNotLocalized()
        {
            return new PackageVerifierIssue("LOC_DESC", "Package description is not localized correctly", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue RequiredCopyright()
        {
            return RequiredCore("NUSPEC_COPYRIGHT", "Copyright", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue RequiredLicenseUrl()
        {
            return RequiredCore("NUSPEC_LICENSEURL", "License Url", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue RequiredIconUrl()
        {
            return RequiredCore("NUSPEC_ICONURL", "Icon Url", PackageIssueLevel.Warning);
        }

        public static PackageVerifierIssue RequiredTags()
        {
            return RequiredCore("NUSPEC_TAGS", "Tags", PackageIssueLevel.Warning);
        }

        public static PackageVerifierIssue RequiredTitle()
        {
            return RequiredCore("NUSPEC_TITLE", "Title", PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue RequiredSummary()
        {
            return RequiredCore("NUSPEC_SUMMARY", "Summary", PackageIssueLevel.Warning);
        }

        public static PackageVerifierIssue RequiredProjectUrl()
        {
            return RequiredCore("NUSPEC_PROJECTURL", "Project Url", PackageIssueLevel.Warning);
        }

        public static PackageVerifierIssue RequiredRequireLicenseAcceptanceTrue()
        {
            return new PackageVerifierIssue("NUSPEC_ACCEPTLICENSE", string.Format("NuSpec Require License Acceptance is not set to true"), PackageIssueLevel.Error);
        }

        private static PackageVerifierIssue RequiredCore(string issueId, string attributeName, PackageIssueLevel issueLevel)
        {
            return new PackageVerifierIssue(issueId, string.Format("NuSpec {0} attribute is missing", attributeName), issueLevel);
        }

        public static PackageVerifierIssue PowerShellScriptNotSigned(string scriptPath)
        {
            return new PackageVerifierIssue("SIGN_POWERSHELL", scriptPath, string.Format("The PowerShell script '{0}' is not signed.", scriptPath), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue PEFileNotAuthenticodeSigned(string assemblyPath)
        {
            return new PackageVerifierIssue("SIGN_AUTHENTICODE", assemblyPath, string.Format("The PE file '{0}' in this package is not authenticode signed.", assemblyPath), PackageIssueLevel.Error);
        }

        public static PackageVerifierIssue AssemblyHasNoDocFile(string assemblyPath)
        {
            return new PackageVerifierIssue("DOC_MISSING", assemblyPath, string.Format("The assembly '{0}' doesn't have a corresponding XML document file.", assemblyPath), PackageIssueLevel.Warning);
        }

        public static PackageVerifierIssue AssemblyHasWrongJsonNetVersion(string assemblyPath, string targetFramework, string currentVersion)
        {
            return new PackageVerifierIssue(
                "WRONG_JSONNET_VERSION",
                string.Format("{0}; {1}", assemblyPath, targetFramework),
                string.Format("The assembly '{0}' references the wrong Json.NET version. Current version '{1}'; Expected version '8.0.2'.", assemblyPath, currentVersion),
                PackageIssueLevel.Error);
        }
    }
}
