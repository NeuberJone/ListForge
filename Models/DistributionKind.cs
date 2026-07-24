namespace ListForge.Models;

public enum DistributionKind
{
    Development,
    Installed,
    PortableOneFile,
    TrialPortableOneFile,
}

public sealed record DistributionInfo(
    DistributionKind Kind,
    string DisplayName,
    bool IsTrial,
    bool CanRunInstallerUpdate)
{
    public bool IsDevelopment => Kind == DistributionKind.Development;
    public bool IsPortable => Kind is DistributionKind.PortableOneFile or DistributionKind.TrialPortableOneFile;
}
