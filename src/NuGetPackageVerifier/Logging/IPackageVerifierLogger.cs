namespace NuGetPackageVerifier.Logging
{
    public interface IPackageVerifierLogger
    {
        void Log(LogLevel logLevel, string message);
    }
}
