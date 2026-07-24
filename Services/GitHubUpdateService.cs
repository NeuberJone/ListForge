using System.Net;
using System.Net.Http;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json.Linq;

namespace ListForge.Services;

public sealed class GitHubUpdateService : IDisposable
{
    public const string DefaultApiUrl = "https://api.github.com/repos/NeuberJone/ListForge/releases/latest";
    public const string ApiUrlEnvironmentVariable = "LISTFORGE_UPDATE_API_URL";

    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public GitHubUpdateService()
        : this(CreateDefaultHttpClient(), ResolveApiUrl())
    {
    }

    public GitHubUpdateService(HttpClient httpClient, string? apiUrl = null)
    {
        _httpClient = httpClient;
        _apiUrl = string.IsNullOrWhiteSpace(apiUrl) ? ResolveApiUrl() : apiUrl.Trim();
        EnsureDefaultHeaders(_httpClient);
    }

    public async Task<OperationResult<UpdateCheckInfo>> CheckForUpdatesAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_apiUrl, UriKind.Absolute, out var apiUri)
            || !string.Equals(apiUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Não foi possível verificar atualizações: endereço de atualização inválido.",
                $"Update API URL inválida: {_apiUrl}",
                errorCode: "InvalidUpdateApiUrl");
        }

        try
        {
            using var response = await _httpClient.GetAsync(apiUri, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    "Nenhuma Release estável foi encontrada para atualização.",
                    "GitHub Releases retornou 404.",
                    errorCode: "ReleaseNotFound");
            }

            if (!response.IsSuccessStatusCode)
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    "Não foi possível verificar atualizações agora. Tente novamente mais tarde.",
                    $"GitHub Releases retornou HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    errorCode: "HttpError");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var releaseResult = ParseRelease(json);
            if (!releaseResult.Success || releaseResult.Value == null)
                return OperationResult<UpdateCheckInfo>.Fail(
                    releaseResult.UserMessage,
                    releaseResult.TechnicalMessage,
                    releaseResult.Exception,
                    releaseResult.ErrorCode);

            var release = releaseResult.Value;
            var comparison = CompareVersions(release.Version, currentVersion);
            if (comparison > 0)
            {
                return OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
                    currentVersion,
                    UpdateAvailability.UpdateAvailable,
                    release,
                    "Uma nova versão do ListForge está disponível."));
            }

            if (comparison == 0)
            {
                return OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
                    currentVersion,
                    UpdateAvailability.UpToDate,
                    release,
                    "O ListForge está atualizado."));
            }

            return OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
                currentVersion,
                UpdateAvailability.RemoteOlder,
                release,
                "O ListForge está atualizado."));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Tempo esgotado ao verificar atualizações. Tente novamente mais tarde.",
                "Timeout ao consultar GitHub Releases.",
                ex,
                "Timeout");
        }
        catch (OperationCanceledException ex)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Verificação de atualização cancelada.",
                "Verificação de atualização cancelada.",
                ex,
                "Canceled");
        }
        catch (Exception ex)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Não foi possível verificar atualizações agora. Verifique sua conexão e tente novamente.",
                "Falha ao consultar GitHub Releases.",
                ex,
                "UpdateCheckFailed");
        }
    }

    internal static OperationResult<UpdateReleaseInfo> ParseRelease(string json)
    {
        try
        {
            var root = JObject.Parse(json);
            if (root.Value<bool?>("draft") == true || root.Value<bool?>("prerelease") == true)
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "Nenhuma Release estável foi encontrada para atualização.",
                    "Release marcada como draft ou prerelease.",
                    errorCode: "ReleaseNotStable");
            }

            var tagName = root.Value<string>("tag_name")?.Trim();
            if (!TryParseReleaseVersion(tagName, out var releaseVersion))
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "A Release encontrada possui uma versão inválida.",
                    $"tag_name inválida: {tagName ?? "(vazia)"}",
                    errorCode: "InvalidTag");
            }

            var versionText = ToThreePartVersion(releaseVersion);
            var expectedInstallerName = $"ListForge-Setup-{versionText}.exe";
            var htmlUrl = root.Value<string>("html_url")?.Trim() ?? "";
            var notes = root.Value<string>("body")?.Trim() ?? "";
            var assets = root["assets"] as JArray;
            if (assets == null)
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "A Release encontrada não possui instalador para atualização.",
                    "Campo assets ausente ou inválido.",
                    errorCode: "AssetsMissing");
            }

            UpdateAssetInfo? installerAsset = null;
            UpdateAssetInfo? checksumsAsset = null;
            foreach (var assetToken in assets.OfType<JObject>())
            {
                var asset = ParseAsset(assetToken);
                if (asset == null)
                    continue;

                if (string.Equals(asset.Name, expectedInstallerName, StringComparison.OrdinalIgnoreCase))
                    installerAsset = asset;
                else if (string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    checksumsAsset = asset;
            }

            if (installerAsset == null)
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "A Release encontrada não possui o instalador esperado para esta versão.",
                    $"Instalador esperado não encontrado: {expectedInstallerName}",
                    errorCode: "InstallerAssetMissing");
            }

            if (!IsHttps(installerAsset.DownloadUrl))
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "O instalador da Release possui um endereço inválido.",
                    $"URL do instalador não é HTTPS: {installerAsset.DownloadUrl}",
                    errorCode: "InstallerUrlNotHttps");
            }

            if (checksumsAsset != null && !IsHttps(checksumsAsset.DownloadUrl))
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "O arquivo de verificação da Release possui um endereço inválido.",
                    $"URL do SHA256SUMS não é HTTPS: {checksumsAsset.DownloadUrl}",
                    errorCode: "ChecksumsUrlNotHttps");
            }

            return OperationResult<UpdateReleaseInfo>.Ok(new UpdateReleaseInfo(
                releaseVersion,
                tagName ?? $"v{versionText}",
                htmlUrl,
                notes,
                installerAsset,
                checksumsAsset));
        }
        catch (Exception ex)
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A resposta de atualização não pôde ser lida.",
                "JSON inválido ao ler GitHub Releases.",
                ex,
                "InvalidJson");
        }
    }

    internal static bool TryParseReleaseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
            return false;

        var normalized = tagName.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        if (!Version.TryParse(normalized, out var parsed))
            return false;

        if (parsed.Major < 0 || parsed.Minor < 0 || parsed.Build < 0)
            return false;

        version = new Version(parsed.Major, parsed.Minor, parsed.Build);
        return true;
    }

    public static string ToThreePartVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private static int CompareVersions(Version remoteVersion, Version currentVersion) =>
        NormalizeVersion(remoteVersion).CompareTo(NormalizeVersion(currentVersion));

    private static Version NormalizeVersion(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));

    private static UpdateAssetInfo? ParseAsset(JObject asset)
    {
        var name = asset.Value<string>("name")?.Trim();
        var downloadUrl = asset.Value<string>("browser_download_url")?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        var size = asset.Value<long?>("size") ?? 0;
        var digest = NormalizeDigest(asset.Value<string>("digest"));
        return new UpdateAssetInfo(name, downloadUrl, size, digest);
    }

    private static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return null;

        var value = digest.Trim();
        const string prefix = "sha256:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value[prefix.Length..];

        return value.Length == 64 && value.All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : null;
    }

    private static bool IsHttps(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static string ResolveApiUrl()
    {
        var value = Environment.GetEnvironmentVariable(ApiUrlEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? DefaultApiUrl : value.Trim();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        EnsureDefaultHeaders(client);
        return client;
    }

    private static void EnsureDefaultHeaders(HttpClient client)
    {
        if (!client.DefaultRequestHeaders.UserAgent.Any())
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ConfigManager.AppName}/update-check");
        if (!client.DefaultRequestHeaders.Accept.Any())
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public void Dispose() =>
        _httpClient.Dispose();
}
