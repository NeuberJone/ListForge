using System.Net.Http;
using System.Security.Cryptography;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public interface IUpdatePackageService
{
    Task<OperationResult<PreparedUpdatePackage>> DownloadAndValidateAsync(
        UpdateReleaseInfo release,
        DistributionInfo distribution,
        IProgress<UpdateDownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    OperationResult ValidatePreparedPackage(PreparedUpdatePackage package);
    OperationResult StartInstaller(PreparedUpdatePackage package);
    OperationResult OpenReleasePage(UpdateReleaseInfo release);
    OperationResult OpenDownloadFolder(PreparedUpdatePackage package);
}

public sealed class UpdateInstallerService : IUpdatePackageService, IDisposable
{
    public const string InstallerArguments = "/CLOSEAPPLICATIONS /NORESTART";
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PartialFileRetention = TimeSpan.FromDays(7);

    private readonly HttpClient _httpClient;
    private readonly IUpdateProcessLauncher _processLauncher;
    private readonly string _updatesRoot;

    public UpdateInstallerService()
        : this(new HttpClient { Timeout = DownloadTimeout }, new UpdateProcessLauncher())
    {
    }

    public UpdateInstallerService(
        HttpClient httpClient,
        IUpdateProcessLauncher processLauncher,
        string? updatesRoot = null)
    {
        _httpClient = httpClient;
        _processLauncher = processLauncher;
        _updatesRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(updatesRoot)
            ? ResolveDefaultUpdatesRoot()
            : updatesRoot);
        EnsureDefaultHeaders(_httpClient);
        CleanupOldPartialFiles();
    }

    public async Task<OperationResult<PreparedUpdatePackage>> DownloadAndValidateAsync(
        UpdateReleaseInfo release,
        DistributionInfo distribution,
        IProgress<UpdateDownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var asset = release.GetAssetFor(distribution.Kind);
        if (asset == null)
        {
            return OperationResult<PreparedUpdatePackage>.Fail(
                distribution.IsTrial
                    ? "A edição Trial deve ser atualizada manualmente pela página da versão."
                    : "A edição portátil deve ser substituída manualmente pela página da versão.",
                $"Manifesto sem asset para {distribution.Kind}.",
                errorCode: "DistributionAssetMissing");
        }

        var expectedName = ExpectedAssetName(release.Version, distribution.Kind);
        if (string.IsNullOrWhiteSpace(expectedName)
            || !string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<PreparedUpdatePackage>.Fail(
                "A versão encontrada não possui o arquivo esperado para esta edição.",
                $"Asset recebido: {asset.Name}; esperado: {expectedName ?? "(nenhum)"}.",
                errorCode: "AssetNameMismatch");
        }

        if (!IsHttps(asset.DownloadUrl) || asset.SizeBytes <= 0)
        {
            return OperationResult<PreparedUpdatePackage>.Fail(
                "O arquivo da atualização possui informações inválidas.",
                $"URL ou tamanho inválido para {asset.Name}.",
                errorCode: "AssetInvalid");
        }

        var versionText = GitHubUpdateService.ToThreePartVersion(release.Version);
        var versionDir = Path.Combine(_updatesRoot, $"v{versionText}");
        var finalPath = Path.Combine(versionDir, expectedName);
        var partialPath = finalPath + ".partial";

        try
        {
            var expectedHashResult = await ResolveExpectedHashAsync(release, asset, cancellationToken).ConfigureAwait(false);
            if (!expectedHashResult.Success || string.IsNullOrWhiteSpace(expectedHashResult.Value))
            {
                return OperationResult<PreparedUpdatePackage>.Fail(
                    expectedHashResult.UserMessage,
                    expectedHashResult.TechnicalMessage,
                    expectedHashResult.Exception,
                    expectedHashResult.ErrorCode);
            }

            var expectedHash = expectedHashResult.Value;
            Directory.CreateDirectory(versionDir);
            DeleteIfExists(partialPath);

            var package = new PreparedUpdatePackage(
                finalPath,
                release,
                asset,
                distribution.Kind,
                expectedHash);

            if (File.Exists(finalPath))
            {
                var existingValidation = ValidatePreparedPackage(package);
                if (existingValidation.Success)
                {
                    progress?.Report(new UpdateDownloadProgressInfo(asset.SizeBytes, asset.SizeBytes));
                    AppLogger.Info("Update", $"Arquivo de atualização existente reutilizado: {asset.Name}.");
                    return OperationResult<PreparedUpdatePackage>.Ok(
                        package,
                        "A atualização já está baixada e pronta.");
                }

                AppLogger.Warning("Update", $"Arquivo de atualização existente rejeitado: {existingValidation.TechnicalMessage}");
                DeleteIfExists(finalPath);
            }

            AppLogger.Info("Update", $"Iniciando download do asset {asset.Name}.");
            await DownloadFileAsync(asset, partialPath, progress, cancellationToken).ConfigureAwait(false);

            var partialInfo = new FileInfo(partialPath);
            if (partialInfo.Length != asset.SizeBytes)
            {
                DeleteIfExists(partialPath);
                return OperationResult<PreparedUpdatePackage>.Fail(
                    "O arquivo baixado não corresponde à versão oficial publicada.",
                    $"Tamanho esperado: {asset.SizeBytes}; obtido: {partialInfo.Length}.",
                    errorCode: "AssetSizeMismatch");
            }

            var actualHash = ComputeSha256(partialPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                DeleteIfExists(partialPath);
                return OperationResult<PreparedUpdatePackage>.Fail(
                    "O arquivo baixado não passou na verificação de integridade.",
                    $"SHA-256 divergente para {asset.Name}.",
                    errorCode: "AssetHashMismatch");
            }

            File.Move(partialPath, finalPath);
            progress?.Report(new UpdateDownloadProgressInfo(asset.SizeBytes, asset.SizeBytes));
            AppLogger.Info("Update", $"Download e SHA-256 validados para {asset.Name}.");
            return OperationResult<PreparedUpdatePackage>.Ok(
                package,
                distribution.CanRunInstallerUpdate
                    ? "Atualização baixada e validada."
                    : "Atualização baixada e validada. Abra a pasta para substituir esta edição manualmente.");
        }
        catch (OperationCanceledException ex)
        {
            DeleteIfExists(partialPath);
            AppLogger.Info("Update", $"Download cancelado para {asset.Name}.");
            return OperationResult<PreparedUpdatePackage>.Fail(
                "Download da atualização cancelado.",
                "Download cancelado pelo usuário.",
                ex,
                "DownloadCanceled");
        }
        catch (Exception ex)
        {
            DeleteIfExists(partialPath);
            return OperationResult<PreparedUpdatePackage>.Fail(
                "Não foi possível baixar a atualização.",
                $"Falha ao baixar ou validar {asset.Name}.",
                ex,
                "UpdateDownloadFailed");
        }
    }

    public OperationResult ValidatePreparedPackage(PreparedUpdatePackage package)
    {
        try
        {
            if (!IsPathInsideUpdatesRoot(package.FilePath)
                || package.FilePath.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(package.FilePath)
                || !string.Equals(Path.GetFileName(package.FilePath), package.Asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail(
                    "O arquivo da atualização não está pronto.",
                    "Arquivo ausente, parcial, fora do cache ou com nome divergente.",
                    errorCode: "UpdateFileNotReady");
            }

            var fileInfo = new FileInfo(package.FilePath);
            if (fileInfo.Length != package.Asset.SizeBytes)
            {
                return OperationResult.Fail(
                    "O arquivo baixado não corresponde à versão oficial publicada.",
                    $"Tamanho esperado: {package.Asset.SizeBytes}; obtido: {fileInfo.Length}.",
                    errorCode: "AssetSizeMismatch");
            }

            var hash = ComputeSha256(package.FilePath);
            return string.Equals(hash, package.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
                ? OperationResult.Ok("Arquivo validado.")
                : OperationResult.Fail(
                    "O arquivo baixado não passou na verificação de integridade.",
                    $"SHA-256 divergente para {package.Asset.Name}.",
                    errorCode: "AssetHashMismatch");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                "Não foi possível validar o arquivo da atualização.",
                "Falha ao validar o pacote preparado.",
                ex,
                "UpdateValidationFailed");
        }
    }

    public OperationResult StartInstaller(PreparedUpdatePackage package)
    {
        if (package.DistributionKind != DistributionKind.Installed)
        {
            return OperationResult.Fail(
                "Esta edição não instala atualizações automaticamente.",
                $"Tentativa de instalar pacote para {package.DistributionKind}.",
                errorCode: "InstallerNotAllowed");
        }

        var validation = ValidatePreparedPackage(package);
        if (!validation.Success)
            return validation;

        return StartInstaller(package.FilePath);
    }

    public OperationResult StartInstaller(string installerPath)
    {
        try
        {
            if (installerPath.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) || !File.Exists(installerPath))
            {
                return OperationResult.Fail(
                    "O instalador da atualização não está pronto para execução.",
                    "Instalador ausente ou parcial.",
                    errorCode: "InstallerNotReady");
            }

            var started = _processLauncher.StartInstaller(installerPath, InstallerArguments);
            if (!started)
            {
                return OperationResult.Fail(
                    "Não foi possível iniciar o instalador. O ListForge continuará aberto.",
                    "Process.Start não confirmou a inicialização do instalador.",
                    errorCode: "InstallerStartFailed");
            }

            AppLogger.Info("Update", "Instalador da atualização iniciado com confirmação do processo.");
            return OperationResult.Ok("Instalador iniciado.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Update", "Falha ao iniciar instalador de atualização.", ex);
            return OperationResult.Fail(
                "Não foi possível iniciar o instalador. O ListForge continuará aberto.",
                "Exceção ao iniciar instalador.",
                ex,
                "InstallerStartFailed");
        }
    }

    public OperationResult OpenReleasePage(UpdateReleaseInfo release)
    {
        var url = IsHttps(release.HtmlUrl) ? release.HtmlUrl : GitHubUpdateService.GitHubReleasesUrl;
        return OpenUrl(url, "página da versão");
    }

    public OperationResult OpenDownloadFolder(PreparedUpdatePackage package)
    {
        var validation = ValidatePreparedPackage(package);
        if (!validation.Success)
            return validation;

        try
        {
            var folder = Path.GetDirectoryName(package.FilePath);
            return !string.IsNullOrWhiteSpace(folder) && _processLauncher.OpenFolder(folder)
                ? OperationResult.Ok("Pasta da atualização aberta.")
                : OperationResult.Fail(
                    "Não foi possível abrir a pasta da atualização.",
                    "Process.Start não confirmou a abertura da pasta.",
                    errorCode: "UpdateFolderOpenFailed");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                "Não foi possível abrir a pasta da atualização.",
                "Exceção ao abrir a pasta do pacote.",
                ex,
                "UpdateFolderOpenFailed");
        }
    }

    public async Task<OperationResult<PreparedUpdateInstaller>> DownloadAndValidateInstallerAsync(
        UpdateReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var adapter = progress == null
            ? null
            : new Progress<UpdateDownloadProgressInfo>(value => progress.Report(value.Percentage));
        var result = await DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(DistributionKind.Installed),
            adapter,
            cancellationToken).ConfigureAwait(false);

        return result.Success && result.Value != null
            ? OperationResult<PreparedUpdateInstaller>.Ok(
                new PreparedUpdateInstaller(result.Value.FilePath, release),
                result.UserMessage,
                result.TechnicalMessage)
            : OperationResult<PreparedUpdateInstaller>.Fail(
                result.UserMessage,
                result.TechnicalMessage,
                result.Exception,
                result.ErrorCode);
    }

    internal static string? ParseChecksumFile(string content, string assetName)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var hash = parts[0].Trim();
            var path = parts[^1].Trim().Replace('\\', '/');
            if (hash.Length == 64
                && hash.All(Uri.IsHexDigit)
                && string.Equals(Path.GetFileName(path), assetName, StringComparison.OrdinalIgnoreCase))
            {
                return hash.ToUpperInvariant();
            }
        }

        return null;
    }

    internal static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private async Task<OperationResult<string>> ResolveExpectedHashAsync(
        UpdateReleaseInfo release,
        UpdateAssetInfo asset,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(asset.Sha256))
            return OperationResult<string>.Ok(asset.Sha256);

        if (release.ChecksumsAsset == null || !IsHttps(release.ChecksumsAsset.DownloadUrl))
        {
            return OperationResult<string>.Fail(
                "A versão não possui informações de integridade suficientes.",
                $"SHA-256 e checksum ausentes para {asset.Name}.",
                errorCode: "ChecksumMissing");
        }

        var content = await _httpClient
            .GetStringAsync(release.ChecksumsAsset.DownloadUrl, cancellationToken)
            .ConfigureAwait(false);
        var hash = ParseChecksumFile(content, asset.Name);
        return string.IsNullOrWhiteSpace(hash)
            ? OperationResult<string>.Fail(
                "O arquivo de verificação não contém o hash esperado.",
                $"SHA256SUMS.txt não contém {asset.Name}.",
                errorCode: "ChecksumEntryMissing")
            : OperationResult<string>.Ok(hash);
    }

    private async Task DownloadFileAsync(
        UpdateAssetInfo asset,
        string partialPath,
        IProgress<UpdateDownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var headerLength = response.Content.Headers.ContentLength.GetValueOrDefault();
        var totalBytes = asset.SizeBytes > 0 ? asset.SizeBytes : headerLength;
        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var local = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;
            progress?.Report(new UpdateDownloadProgressInfo(totalRead, totalBytes));
        }
    }

    private OperationResult OpenUrl(string url, string description)
    {
        try
        {
            return IsHttps(url) && _processLauncher.OpenUrl(url)
                ? OperationResult.Ok($"{description} aberta.")
                : OperationResult.Fail(
                    $"Não foi possível abrir a {description}.",
                    $"URL inválida ou processo não iniciado para {description}.",
                    errorCode: "ReleasePageOpenFailed");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                $"Não foi possível abrir a {description}.",
                $"Exceção ao abrir {description}.",
                ex,
                "ReleasePageOpenFailed");
        }
    }

    private void CleanupOldPartialFiles()
    {
        try
        {
            if (!Directory.Exists(_updatesRoot) || !IsPathInsideUpdatesRoot(_updatesRoot))
                return;

            var threshold = DateTime.UtcNow - PartialFileRetention;
            foreach (var path in Directory.EnumerateFiles(_updatesRoot, "*.partial", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(path) < threshold && IsPathInsideUpdatesRoot(path))
                    DeleteIfExists(path);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning("Update", "Não foi possível concluir a limpeza de downloads parciais antigos.", ex);
        }
    }

    private bool IsPathInsideUpdatesRoot(string path)
    {
        var root = _updatesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        return string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar), _updatesRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExpectedAssetName(Version version, DistributionKind distributionKind)
    {
        var versionText = GitHubUpdateService.ToThreePartVersion(version);
        return distributionKind switch
        {
            DistributionKind.Installed => $"ListForge-Setup-{versionText}.exe",
            DistributionKind.PortableOneFile => $"ListForge-v{versionText}.exe",
            DistributionKind.TrialPortableOneFile => $"ListForge-Trial-v{versionText}.exe",
            _ => null,
        };
    }

    private static string ResolveDefaultUpdatesRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
        return Path.Combine(root, ConfigManager.AppName, "updates");
    }

    private static bool IsHttps(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup failures are logged by the caller when they affect the operation.
        }
    }

    private static void EnsureDefaultHeaders(HttpClient client)
    {
        if (!client.DefaultRequestHeaders.UserAgent.Any())
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ConfigManager.AppName}/update-download");
    }

    public void Dispose() =>
        _httpClient.Dispose();
}
