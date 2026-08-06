using System.Net;
using System.Net.Http;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json.Linq;

namespace ListForge.Services;

public interface IUpdateDiscoveryService
{
    Task<OperationResult<UpdateCheckInfo>> CheckForUpdatesAsync(
        Version currentVersion,
        DistributionInfo distribution,
        CancellationToken cancellationToken = default);
}

public sealed class GitHubUpdateService : IUpdateDiscoveryService, IDisposable
{
    public const string DefaultApiUrl = "https://pub-62303cd1120248b08beb3454fe0c6316.r2.dev/update.json";
    public const string GitHubApiUrl = "https://api.github.com/repos/NeuberJone/ListForge/releases/latest";
    public const string GitHubReleasesUrl = "https://github.com/NeuberJone/ListForge/releases";
    public const string ApiUrlEnvironmentVariable = "LISTFORGE_UPDATE_API_URL";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<UpdateEndpoint> _endpoints;

    public GitHubUpdateService()
        : this(CreateDefaultHttpClient(), ResolveConfiguredApiUrl(), DefaultApiUrl, GitHubApiUrl)
    {
    }

    public GitHubUpdateService(HttpClient httpClient, string? apiUrl = null)
        : this(httpClient, apiUrl, DefaultApiUrl, GitHubApiUrl)
    {
    }

    public GitHubUpdateService(
        HttpClient httpClient,
        string? configuredApiUrl,
        string officialApiUrl,
        string gitHubApiUrl)
    {
        _httpClient = httpClient;
        EnsureDefaultHeaders(_httpClient);
        _endpoints = BuildEndpoints(configuredApiUrl, officialApiUrl, gitHubApiUrl);
    }

    public Task<OperationResult<UpdateCheckInfo>> CheckForUpdatesAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default) =>
        CheckForUpdatesAsync(
            currentVersion,
            DistributionInfoService.FromKind(DistributionKind.Installed),
            cancellationToken);

    public async Task<OperationResult<UpdateCheckInfo>> CheckForUpdatesAsync(
        Version currentVersion,
        DistributionInfo distribution,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var endpoint in _endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppLogger.Info("Update", $"Consultando fonte de atualização: {endpoint.DisplayName}.");

            var result = await CheckEndpointAsync(endpoint, currentVersion, distribution, cancellationToken)
                .ConfigureAwait(false);
            if (result.Success)
            {
                AppLogger.Info("Update", $"Verificação concluída pela fonte: {endpoint.DisplayName}.");
                return result;
            }

            failures.Add($"{endpoint.DisplayName}: {result.TechnicalMessage}");
            AppLogger.Warning("Update", $"Fonte {endpoint.DisplayName} indisponível ou inválida. {result.TechnicalMessage}");

            if (string.Equals(result.ErrorCode, "Canceled", StringComparison.Ordinal))
                return result;
        }

        return OperationResult<UpdateCheckInfo>.Fail(
            "Não foi possível consultar o servidor de atualizações. Verifique sua conexão e tente novamente.",
            string.Join(" | ", failures),
            errorCode: "AllUpdateSourcesFailed");
    }

    private async Task<OperationResult<UpdateCheckInfo>> CheckEndpointAsync(
        UpdateEndpoint endpoint,
        Version currentVersion,
        DistributionInfo distribution,
        CancellationToken cancellationToken)
    {
        if (!TryCreateHttpsUri(endpoint.Url, out var endpointUri))
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Não foi possível verificar atualizações: endereço inválido.",
                $"A fonte {endpoint.DisplayName} não usa HTTPS.",
                errorCode: "InvalidUpdateApiUrl");
        }

        try
        {
            using var response = await _httpClient.GetAsync(endpointUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    "Não foi possível consultar esta fonte de atualizações.",
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    errorCode: response.StatusCode == HttpStatusCode.NotFound ? "ReleaseNotFound" : "HttpError");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    "A fonte de atualizações retornou uma resposta vazia.",
                    "Resposta vazia.",
                    errorCode: "EmptyResponse");
            }

            var releaseResult = ParseRelease(json);
            if (!releaseResult.Success || releaseResult.Value == null)
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    releaseResult.UserMessage,
                    releaseResult.TechnicalMessage,
                    releaseResult.Exception,
                    releaseResult.ErrorCode);
            }

            var release = releaseResult.Value with { Source = endpoint.Source };
            var validation = ValidateReleaseForDistribution(release, distribution);
            if (!validation.Success)
            {
                return OperationResult<UpdateCheckInfo>.Fail(
                    validation.UserMessage,
                    validation.TechnicalMessage,
                    validation.Exception,
                    validation.ErrorCode);
            }

            var comparison = CompareVersions(release.Version, currentVersion);
            var availability = comparison switch
            {
                > 0 => UpdateAvailability.UpdateAvailable,
                0 => UpdateAvailability.UpToDate,
                _ => UpdateAvailability.RemoteOlder,
            };
            var message = availability == UpdateAvailability.UpdateAvailable
                ? $"Uma nova versão está disponível: {ToThreePartVersion(release.Version)}."
                : "O ListForge está atualizado.";

            return OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
                currentVersion,
                availability,
                release,
                message));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "O servidor de atualizações demorou para responder.",
                $"Timeout na fonte {endpoint.DisplayName}.",
                ex,
                "Timeout");
        }
        catch (OperationCanceledException ex)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Verificação de atualização cancelada.",
                "Verificação cancelada pelo usuário.",
                ex,
                "Canceled");
        }
        catch (Exception ex)
        {
            return OperationResult<UpdateCheckInfo>.Fail(
                "Não foi possível consultar esta fonte de atualizações.",
                $"Falha na fonte {endpoint.DisplayName}.",
                ex,
                "UpdateCheckFailed");
        }
    }

    internal static OperationResult<UpdateReleaseInfo> ParseRelease(string json)
    {
        try
        {
            var root = JObject.Parse(json);
            return root["assets"] == null && root["tag_name"] == null
                ? ParseManifest(root)
                : ParseGitHubRelease(root);
        }
        catch (Exception ex)
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A resposta de atualização não pôde ser lida.",
                "JSON inválido ao ler a fonte de atualização.",
                ex,
                "InvalidJson");
        }
    }

    private static OperationResult<UpdateReleaseInfo> ParseGitHubRelease(JObject root)
    {
        if (root.Value<bool?>("draft") == true || root.Value<bool?>("prerelease") == true)
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "Nenhuma versão estável foi encontrada para atualização.",
                "Release marcada como draft ou prerelease.",
                errorCode: "ReleaseNotStable");
        }

        var tagName = root.Value<string>("tag_name")?.Trim();
        if (!TryParseReleaseVersion(tagName, out var releaseVersion))
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A versão encontrada é inválida.",
                $"tag_name inválida: {tagName ?? "(vazia)"}",
                errorCode: "InvalidTag");
        }

        var versionText = ToThreePartVersion(releaseVersion);
        var assets = root["assets"] as JArray;
        if (assets == null)
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A versão encontrada não possui arquivos para atualização.",
                "Campo assets ausente ou inválido.",
                errorCode: "AssetsMissing");
        }

        var parsedAssets = assets
            .OfType<JObject>()
            .Select(ParseAsset)
            .Where(asset => asset != null)
            .Cast<UpdateAssetInfo>()
            .ToList();
        var installerName = $"ListForge-Setup-{versionText}.exe";
        var installer = FindAsset(parsedAssets, installerName);
        if (installer == null)
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A versão encontrada não possui o instalador esperado.",
                $"Asset ausente: {installerName}",
                errorCode: "InstallerAssetMissing");
        }

        if (!IsValidHttpsAsset(installer))
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "O instalador da versão possui dados inválidos.",
                $"Asset sem tamanho válido ou URL HTTPS: {installerName}.",
                errorCode: "InstallerAssetInvalid");
        }

        var checksums = FindAsset(parsedAssets, "SHA256SUMS.txt");
        if (checksums != null && !IsValidHttpsAsset(checksums))
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "O arquivo de verificação da versão possui dados inválidos.",
                "SHA256SUMS.txt sem tamanho válido ou URL HTTPS.",
                errorCode: "ChecksumsInvalid");
        }

        var portable = FindAsset(parsedAssets, $"ListForge-v{versionText}.exe");
        var trial = FindAsset(parsedAssets, $"ListForge-Trial-v{versionText}.exe");
        if ((portable != null && !IsValidHttpsAsset(portable))
            || (trial != null && !IsValidHttpsAsset(trial)))
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "Um arquivo da atualização possui dados inválidos.",
                "Asset portátil ou Trial sem tamanho válido ou URL HTTPS.",
                errorCode: "AssetInvalid");
        }

        return OperationResult<UpdateReleaseInfo>.Ok(new UpdateReleaseInfo(
            releaseVersion,
            tagName ?? $"v{versionText}",
            root.Value<string>("html_url")?.Trim() ?? GitHubReleasesUrl,
            root.Value<string>("body")?.Trim() ?? "",
            installer,
            checksums,
            portable,
            trial,
            UpdateSource.GitHub));
    }

    private static OperationResult<UpdateReleaseInfo> ParseManifest(JObject root)
    {
        var versionValue = ReadString(root, "version");
        if (!TryParseReleaseVersion(versionValue, out var releaseVersion))
        {
            return OperationResult<UpdateReleaseInfo>.Fail(
                "A versão encontrada é inválida.",
                $"version inválida no manifesto: {versionValue ?? "(vazia)"}",
                errorCode: "InvalidTag");
        }

        var versionText = ToThreePartVersion(releaseVersion);
        var installerResult = ParseManifestAsset(
            root["installer"] as JObject,
            $"ListForge-Setup-{versionText}.exe",
            required: true);
        if (!installerResult.Success || installerResult.Value == null)
            return OperationResult<UpdateReleaseInfo>.Fail(
                installerResult.UserMessage,
                installerResult.TechnicalMessage,
                installerResult.Exception,
                installerResult.ErrorCode);

        var portableResult = ParseManifestAsset(root["portable"] as JObject, $"ListForge-v{versionText}.exe", required: false);
        if (!portableResult.Success)
            return OperationResult<UpdateReleaseInfo>.Fail(portableResult.UserMessage, portableResult.TechnicalMessage, portableResult.Exception, portableResult.ErrorCode);

        var trialResult = ParseManifestAsset(root["trial"] as JObject, $"ListForge-Trial-v{versionText}.exe", required: false);
        if (!trialResult.Success)
            return OperationResult<UpdateReleaseInfo>.Fail(trialResult.UserMessage, trialResult.TechnicalMessage, trialResult.Exception, trialResult.ErrorCode);

        UpdateAssetInfo? checksums = null;
        var checksum = root["checksums"] as JObject;
        if (checksum != null)
        {
            checksums = new UpdateAssetInfo(
                ReadString(checksum, "name") ?? "SHA256SUMS.txt",
                ReadString(checksum, "url") ?? ReadString(checksum, "downloadUrl") ?? "",
                ReadLong(checksum, "size") ?? ReadLong(checksum, "sizeBytes") ?? 0,
                NormalizeDigest(ReadString(checksum, "sha256") ?? ReadString(checksum, "digest")));
            if (!IsValidHttpsAsset(checksums))
            {
                return OperationResult<UpdateReleaseInfo>.Fail(
                    "O arquivo de verificação da versão possui dados inválidos.",
                    "SHA256SUMS.txt ausente, sem tamanho ou sem URL HTTPS.",
                    errorCode: "ChecksumsInvalid");
            }
        }

        return OperationResult<UpdateReleaseInfo>.Ok(new UpdateReleaseInfo(
            releaseVersion,
            ReadString(root, "tagName") ?? ReadString(root, "tag_name") ?? $"v{versionText}",
            ReadString(root, "releaseUrl") ?? ReadString(root, "htmlUrl") ?? ReadString(root, "html_url") ?? GitHubReleasesUrl,
            ReadString(root, "notes") ?? ReadString(root, "body") ?? "",
            installerResult.Value,
            checksums,
            portableResult.Value,
            trialResult.Value));
    }

    private static OperationResult<UpdateAssetInfo?> ParseManifestAsset(JObject? asset, string expectedName, bool required)
    {
        if (asset == null)
        {
            return required
                ? OperationResult<UpdateAssetInfo?>.Fail(
                    "A versão encontrada não possui o arquivo esperado.",
                    $"Seção ausente para {expectedName}.",
                    errorCode: "InstallerAssetMissing")
                : OperationResult<UpdateAssetInfo?>.Ok(null);
        }

        var name = ReadString(asset, "name") ?? expectedName;
        var parsed = new UpdateAssetInfo(
            name,
            ReadString(asset, "url") ?? ReadString(asset, "downloadUrl") ?? "",
            ReadLong(asset, "size") ?? ReadLong(asset, "sizeBytes") ?? 0,
            NormalizeDigest(ReadString(asset, "sha256") ?? ReadString(asset, "digest")));

        if (!string.Equals(parsed.Name, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<UpdateAssetInfo?>.Fail(
                "A versão encontrada não possui o arquivo esperado.",
                $"Nome recebido: {parsed.Name}; esperado: {expectedName}.",
                errorCode: "AssetNameMismatch");
        }

        if (!IsValidHttpsAsset(parsed))
        {
            return OperationResult<UpdateAssetInfo?>.Fail(
                "O arquivo da atualização possui dados inválidos.",
                $"Asset sem tamanho válido ou URL HTTPS: {expectedName}.",
                errorCode: "AssetInvalid");
        }

        return OperationResult<UpdateAssetInfo?>.Ok(parsed);
    }

    private static OperationResult ValidateReleaseForDistribution(UpdateReleaseInfo release, DistributionInfo distribution)
    {
        var asset = release.GetAssetFor(distribution.Kind);
        if (asset == null)
        {
            return distribution.Kind == DistributionKind.Installed
                ? OperationResult.Fail(
                    "A versão encontrada não possui o instalador esperado.",
                    "Asset instalado ausente.",
                    errorCode: "InstallerAssetMissing")
                : OperationResult.Ok();
        }

        if (!IsValidHttpsAsset(asset))
        {
            return OperationResult.Fail(
                "O arquivo da atualização possui dados inválidos.",
                $"Asset inválido para {distribution.DisplayName}.",
                errorCode: "AssetInvalid");
        }

        if (string.IsNullOrWhiteSpace(asset.Sha256) && release.ChecksumsAsset == null)
        {
            return OperationResult.Fail(
                "A versão encontrada não possui informações de integridade suficientes.",
                $"SHA-256 ausente para {asset.Name}.",
                errorCode: "ChecksumMissing");
        }

        return OperationResult.Ok();
    }

    internal static bool TryParseReleaseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
            return false;

        var normalized = tagName.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        if (!Version.TryParse(normalized, out var parsed)
            || parsed.Major < 0
            || parsed.Minor < 0
            || parsed.Build < 0)
        {
            return false;
        }

        version = new Version(parsed.Major, parsed.Minor, parsed.Build);
        return true;
    }

    public static string ToThreePartVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private static int CompareVersions(Version remoteVersion, Version currentVersion) =>
        NormalizeVersion(remoteVersion).CompareTo(NormalizeVersion(currentVersion));

    private static Version NormalizeVersion(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));

    private static IReadOnlyList<UpdateEndpoint> BuildEndpoints(string? configuredUrl, string officialUrl, string gitHubUrl)
    {
        var endpoints = new List<UpdateEndpoint>();
        AddUnique(endpoints, configuredUrl, UpdateSource.ConfiguredManifest, "manifesto configurado");
        AddUnique(endpoints, officialUrl, UpdateSource.OfficialManifest, "manifesto oficial");
        AddUnique(endpoints, gitHubUrl, UpdateSource.GitHub, "GitHub Releases");
        return endpoints;
    }

    private static void AddUnique(List<UpdateEndpoint> endpoints, string? url, UpdateSource source, string displayName)
    {
        if (string.IsNullOrWhiteSpace(url)
            || endpoints.Any(item => string.Equals(item.Url, url.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        endpoints.Add(new UpdateEndpoint(url.Trim(), source, displayName));
    }

    private static UpdateAssetInfo? ParseAsset(JObject asset)
    {
        var name = asset.Value<string>("name")?.Trim();
        var downloadUrl = asset.Value<string>("browser_download_url")?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        return new UpdateAssetInfo(
            name,
            downloadUrl,
            asset.Value<long?>("size") ?? 0,
            NormalizeDigest(asset.Value<string>("digest")));
    }

    private static UpdateAssetInfo? FindAsset(IEnumerable<UpdateAssetInfo> assets, string expectedName) =>
        assets.FirstOrDefault(asset => string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase));

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

    private static string? ReadString(JObject? root, string name) =>
        root?.Value<string>(name)?.Trim();

    private static long? ReadLong(JObject? root, string name) =>
        root?.Value<long?>(name);

    private static bool IsValidHttpsAsset(UpdateAssetInfo asset) =>
        asset.SizeBytes > 0 && TryCreateHttpsUri(asset.DownloadUrl, out _);

    private static bool TryCreateHttpsUri(string url, out Uri uri)
    {
        var valid = Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        uri = parsed ?? new Uri("https://invalid.local");
        return valid;
    }

    private static string? ResolveConfiguredApiUrl()
    {
        var value = Environment.GetEnvironmentVariable(ApiUrlEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        EnsureDefaultHeaders(client);
        return client;
    }

    private static void EnsureDefaultHeaders(HttpClient client)
    {
        if (!client.DefaultRequestHeaders.UserAgent.Any())
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ConfigManager.AppName}/update-check");
        if (!client.DefaultRequestHeaders.Accept.Any())
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public void Dispose() =>
        _httpClient.Dispose();

    private sealed record UpdateEndpoint(string Url, UpdateSource Source, string DisplayName);
}
