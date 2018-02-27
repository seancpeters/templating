namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal class PackageRestoreInfo
    {
        public PackageRestoreInfo(string projFile, string packageName, string requestedVersion)
        {
            ProjFile = projFile;
            PackageName = packageName;
            RequestedVersion = requestedVersion;
            RestoredVersion = "Did not restore";
            DidntRestore = true;
        }

        public PackageRestoreInfo(string projFile, string packageName, string requestedVersion, string restoredVersion)
        {
            ProjFile = projFile;
            PackageName = packageName;
            RequestedVersion = requestedVersion;
            RestoredVersion = restoredVersion;
            DidntRestore = false;
        }

        public string ProjFile { get; }

        public string PackageName { get; }

        public string RequestedVersion { get; }

        public string RestoredVersion { get; }

        public bool DidntRestore { get; }
    }
}
