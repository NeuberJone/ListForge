using System.Net.Http;
using System.Security.Cryptography;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public sealed class UpdateInstallerService : IDisposable
{
    public const string InstallerArguments = "/CLOSEAPPLICATIONS /NORESTART";

    private readonly HttpClient _httpClient;
    private readonly IUpdateProcessLauncher _processLauncher;
    private readonly string _updatesRoot;

    public UpdateInstallerService()
        : this(new HttpClient { Timeout = TimeSpan.FromMinutes(5) }, new UpdateProcessLauncher())
    {
    }

    public UpdateInstallerService(
        HttpClient httpClient,
        IUpdateProcessLauncher processLauncher,
        string? updatesRoot = null)
    {
        _httpClient = httpClient;
        _processLauncher = processLauncher;
        _updatesRoot = string.IsNullOrWhiteSpace(updatesRoot)
            ? ResolveDefaultUpdatesRoot()
            : updatesRoot;
        EnsureDefaultHeaders(_httpClient);
    }

    public async Task<OperationResult<PreparedUpdateInstaller>> DownloadAndValidateInstallerAsync(
        UpdateReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versionText = GitHubUpdateService.ToThreePartVersion(release.Version);
        var expectedName = $"ListForge-Setup-{versionText}.exe";
        if (!string.Equals(release.InstallerAsset.Name, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<PreparedUpdateInstaller>.Fail(
                "A Release encontrada não possui o instalador esperado para esta versão.",
                $"Asset recebido: {release.InstallerAsset.Name}; esperado: {expectedName}",
                errorCode: "InstallerAssetMismatch");
        }

        if (!IsHttps(release.InstallerAsset.DownloadUrl))
        {
            return OperationResult<PreparedUpdateInstaller>.Fail(
                "O instalador da Release possui um endereço inválido.",
                $"URL do instalador não é HTTPS: {release.InstallerAsset.DownloadUrl}",
                errorCode: "InstallerUrlNotHttps");
        }

        try
        {
            var expectedHashResult = await ResolveExpectedHashAsync(release, cancellationToken).ConfigureAwait(false);
            if (!expectedHashResult.Success || string.IsNullOrWhiteSpace(expectedHashResult.Value))
            {
                return OperationResult<PreparedUpdateInstaller>.Fail(
                    expectedHashResult.UserMessage,
                    expectedHashResult.TechnicalMessage,
                    expectedHashResult.Exception,
                    expectedHashResult.ErrorCode);
            }

            var expectedHash = expectedHashResult.Value;
            var versionDir = Path.Combine(_updatesRoot, $"v{versionText}");
            Directory.CreateDirectory(versionDir);

            var installerPath = Path.Combine(versionDir, expectedName);
            var partialPath = installerPath + ".partial";
            DeleteIfExists(partialPath);

            if (File.Exists(installerPath))
            {
                var existingHash = ComputeSha256(installerPath);
                if (string.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(100);
                    return OperationResult<PreparedUpdateInstaller>.Ok(
                        new PreparedUpdateInstaller(installerPath, release),
                        "Instalador já baixado e validado.");
                }

                DeleteIfExists(installerPath);
            }

            await DownloadFileAsync(release.InstallerAsset, partialPath, progress, cancellationToken).ConfigureAwait(false);

            var downloaded = new FileInfo(partialPath);
            if (release.InstallerAsset.SizeBytes > 0 && downloaded.Length != release.InstallerAsset.SizeBytes)
            {
                DeleteIfExists(partialPath);
                return OperationResult<PreparedUpdateInstaller>.Fail(
                    "O download da atualização foi concluído com tamanho inesperado.",
                    $"Tamanho esperado: {release.InstallerAsset.SizeBytes}; obtido: {downloaded.Length}",
                    errorCode: "InstallerSizeMismatch");
            }

            var actualHash = ComputeSha256(partialPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                DeleteIfExists(partialPath);
                return OperationResult<PreparedUpdateInstaller>.Fail(
                    "A validação de integridade da atualização falhou. O instalador não será executado.",
                    $"SHA-256 esperado: {expectedHash}; obtido: {actualHash}",
                    errorCode: "InstallerHashMismatch");
            }

            File.Move(partialPath, installerPath);
            progress?.Report(100);
            return OperationResult<PreparedUpdateInstaller>.Ok(
                new PreparedUpdateInstaller(installerPath, release),
                "Atualização baixada e validada.");
        }
        catch (OperationCanceledException ex)
        {
            DeleteIfExists(Path.Combine(_updatesRoot, $"v{versionText}", expectedName + ".partial"));
            return OperationResult<PreparedUpdateInstaller>.Fail(
                "Download da atualização cancelado.",
                "Download da atualização cancelado.",
                ex,
                "DownloadCanceled");
        }
        catch (Exception ex)
        {
            return OperationResult<PreparedUpdateInstaller>.Fail(
                "Não foi possível baixar a atualização agora.",
                "Falha ao baixar ou validar instalador.",
                ex,
                "UpdateDownloadFailed");
        }
    }

    public OperationResult StartInstaller(string installerPath)
    {
        try
        {
            if (installerPath.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) || !File.Exists(installerPath))
            {
                return OperationResult.Fail(
                    "O instalador da atualização não está pronto para execução.",
                    $"Instalador ausente ou parcial: {installerPath}",
                    errorCode: "InstallerNotReady");
            }

            var started = _processLauncher.StartInstaller(installerPath, InstallerArguments);
            if (!started)
            {
                return OperationResult.Fail(
                    $"Não foi possível iniciar o instalador.\n\nArquivo salvo em:\n{installerPath}",
                    "Process.Start retornou null para o instalador.",
                    errorCode: "InstallerStartFailed");
            }

            AppLogger.Info("Update", $"Instalador iniciado para atualização: {installerPath}");
            return OperationResult.Ok("Instalador iniciado.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Update", "Falha ao iniciar instalador de atualização.", ex, installerPath);
            return OperationResult.Fail(
                $"Não foi possível iniciar o instalador.\n\nArquivo salvo em:\n{installerPath}",
                "Exceção ao iniciar instalador.",
                ex,
                "InstallerStartFailed");
        }
    }

    public OperationResult OpenReleasePage(UpdateReleaseInfo release)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(release.HtmlUrl) || !IsHttps(release.HtmlUrl))
            {
                return OperationResult.Fail(
                    "A página da Release não está disponível.",
                    $"URL da Release inválida: {release.HtmlUrl}",
                    errorCode: "ReleasePageInvalid");
            }

            return _processLauncher.OpenUrl(release.HtmlUrl)
                ? OperationResult.Ok("Página da Release aberta.")
                : OperationResult.Fail(
                    "Não foi possível abrir a página da Release.",
                    "Process.Start retornou null ao abrir URL.",
                    errorCode: "ReleasePageOpenFailed");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Update", "Falha ao abrir página da Release.", ex);
            return OperationResult.Fail(
                "Não foi possível abrir a página da Release.",
                "Exceção ao abrir página da Release.",
                ex,
                "ReleasePageOpenFailed");
        }
    }

    internal static string? ParseChecksumFile(string content, string installerName)
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
                && string.Equals(Path.GetFileName(path), installerName, StringComparison.OrdinalIgnoreCase))
            {
                return hash.ToUpperInvariant();
            }
        }

        return null;
    }

    internal static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private async Task<OperationResult<string>> ResolveExpectedHashAsync(
        UpdateReleaseInfo release,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(release.InstallerAsset.Sha256))
            return OperationResult<string>.Ok(release.InstallerAsset.Sha256);

        if (release.ChecksumsAsset == null)
        {
            return OperationResult<string>.Fail(
                "A Release não possui informações de integridade para validar o instalador.",
                "Nenhum digest SHA-256 e nenhum SHA256SUMS.txt foram encontrados.",
                errorCode: "ChecksumMissing");
        }

        if (!IsHttps(release.ChecksumsAsset.DownloadUrl))
        {
            return OperationResult<string>.Fail(
                "O arquivo de verificação da Release possui um endereço inválido.",
                $"URL do checksum não é HTTPS: {release.ChecksumsAsset.DownloadUrl}",
                errorCode: "ChecksumsUrlNotHttps");
        }

        var content = await _httpClient.GetStringAsync(release.ChecksumsAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        var hash = ParseChecksumFile(content, release.InstallerAsset.Name);
        return string.IsNullOrWhiteSpace(hash)
            ? OperationResult<string>.Fail(
                "O arquivo de verificação não contém o hash do instalador esperado.",
                $"SHA256SUMS.txt não contém {release.InstallerAsset.Name}.",
                errorCode: "ChecksumEntryMissing")
            : OperationResult<string>.Ok(hash);
    }

    private async Task DownloadFileAsync(
        UpdateAssetInfo asset,
        string partialPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = asset.SizeBytes > 0
            ? asset.SizeBytes
            : response.Content.Headers.ContentLength.GetValueOrDefault();

        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var local = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;
            if (totalBytes > 0)
                progress?.Report(Math.Clamp(totalRead * 100d / totalBytes, 0, 99));
        }
    }

    private static string ResolveDefaultUpdatesRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? Path.GetTempPath()
            : localAppData;

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
            // A failed cleanup should not hide the original operation result.
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
