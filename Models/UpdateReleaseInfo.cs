namespace ListForge.Models;

public enum UpdateAvailability
{
    UpdateAvailable,
    UpToDate,
    RemoteOlder,
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
    UpdateAssetInfo? ChecksumsAsset);

public sealed record UpdateCheckInfo(
    Version CurrentVersion,
    UpdateAvailability Availability,
    UpdateReleaseInfo? Release,
    string UserMessage);

public sealed record PreparedUpdateInstaller(
    string InstallerPath,
    UpdateReleaseInfo Release);
