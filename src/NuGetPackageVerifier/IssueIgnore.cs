namespace NuGetPackageVerifier
{
    public class IssueIgnore
    {
        public string PackageId { get; set; }
        public string IssueId { get; set; }
        public string Instance { get; set; }
        public string Justification { get; set; }
    }
}
