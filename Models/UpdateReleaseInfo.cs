namespace ListForge.Models;

public enum UpdateAvailability
{
    UpdateAvailable,
    UpToDate,
    RemoteOlder,
}

public enum UpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Downloaded,
    ReadyToInstall,
    Installing,
    Offline,
    Failed,
}

public enum UpdateSource
{
    ConfiguredManifest,
    OfficialManifest,
    GitHub,
}

public sealed record UpdateAssetInfo(
    string Name,
    string DownloadUrl,
    long SizeBytes,
    string? Sha256);

public sealed record UpdateReleaseInfo(
    Version Version,
    string TagName,
    string HtmlUrl,
    string Notes,
    UpdateAssetInfo InstallerAsset,
    UpdateAssetInfo? ChecksumsAsset,
    UpdateAssetInfo? PortableAsset = null,
    UpdateAssetInfo? TrialAsset = null,
    UpdateSource Source = UpdateSource.OfficialManifest)
{
    public UpdateAssetInfo? GetAssetFor(DistributionKind distributionKind) => distributionKind switch
    {
        DistributionKind.Installed => InstallerAsset,
        DistributionKind.PortableOneFile => PortableAsset,
        DistributionKind.TrialPortableOneFile => TrialAsset,
        _ => null,
    };
}

public sealed record UpdateCheckInfo(
    Version CurrentVersion,
    UpdateAvailability Availability,
    UpdateReleaseInfo? Release,
    string UserMessage);

public sealed record PreparedUpdateInstaller(
    string InstallerPath,
    UpdateReleaseInfo Release);

public sealed record PreparedUpdatePackage(
    string FilePath,
    UpdateReleaseInfo Release,
    UpdateAssetInfo Asset,
    DistributionKind DistributionKind,
    string ExpectedSha256);

public sealed record UpdateDownloadProgressInfo(long DownloadedBytes, long TotalBytes)
{
    public double Percentage => TotalBytes <= 0
        ? 0
        : Math.Clamp(DownloadedBytes * 100d / TotalBytes, 0, 100);
}
