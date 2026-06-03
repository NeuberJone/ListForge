using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;

namespace ListForge.Services;

public sealed record SupportPackageOptions(
    bool IncludeLogs = true,
    int MaxLogFiles = 5,
    long MaxTotalLogSizeBytes = 1_048_576);

public sealed class SupportPackageService
{
    private static readonly SupportPackageOptions DefaultOptions = new();

    public OperationResult<string> Generate(string outputDirectory, AboutInfo aboutInfo)
    {
        return Generate(outputDirectory, aboutInfo, DefaultOptions);
    }

    public OperationResult<string> Generate(string outputDirectory, AboutInfo aboutInfo, SupportPackageOptions options)
    {
        try
        {
            return OperationResult<string>.Ok(GeneratePackage(outputDirectory, aboutInfo, NormalizeOptions(options)));
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                $"Falha ao gerar pacote de suporte.\n\n{ex.Message}",
                "Falha ao gerar pacote de suporte.",
                ex,
                "SupportPackageFailed");
        }
    }

    private static SupportPackageOptions NormalizeOptions(SupportPackageOptions? options)
    {
        options ??= DefaultOptions;

        return new SupportPackageOptions(
            options.IncludeLogs,
            Math.Clamp(options.MaxLogFiles, 0, 5),
            Math.Clamp(options.MaxTotalLogSizeBytes, 0, 5 * 1_048_576));
    }

    private static string GeneratePackage(string outputDirectory, AboutInfo aboutInfo, SupportPackageOptions options)
    {
        Directory.CreateDirectory(outputDirectory);

        var packagePath = CreatePackagePath(outputDirectory);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        AddTextEntry(archive, "support-info.txt", BuildSupportInfo(aboutInfo, options));
        AddTextEntry(archive, "config-summary.txt", BuildConfigSummary(ConfigManager.LoadConfig()));
        AddTextEntry(archive, "sizes-summary.txt", BuildSizeSummary(ConfigManager.LoadSizeConfig()));

        if (options.IncludeLogs)
            AddRecentLogs(archive, options);

        AppLogger.Info("SupportPackage", "Pacote de suporte gerado.");
        return packagePath;
    }

    private static string CreatePackagePath(string outputDirectory)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var packagePath = Path.Combine(outputDirectory, $"support-package-{timestamp}.zip");

        if (!File.Exists(packagePath))
            return packagePath;

        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(outputDirectory, $"support-package-{timestamp}-{index}.zip");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string BuildSupportInfo(AboutInfo info, SupportPackageOptions options)
    {
        var lines = new[]
        {
            "ListForge",
            $"Versão: {info.Version}",
            $"Edição: {info.Edition}",
            $"Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Sistema operacional: {RuntimeInformation.OSDescription}",
            $"Arquitetura: {RuntimeInformation.OSArchitecture}",
            $"Pasta de configuração: {info.ConfigPath}",
            $"Pasta de logs: {info.LogsPath}",
            $"Logs recentes incluídos: {(options.IncludeLogs ? "sim" : "não")}",
            $"Limite de arquivos de log: {options.MaxLogFiles}",
            $"Limite total de logs: {options.MaxTotalLogSizeBytes} bytes",
            "",
            "Privacidade: este pacote não inclui conteúdo completo de listas, saída organizada, JSON de listas reais, arquivos do usuário, estado interno do Trial, tokens, senhas, chaves, build/dist ou repositório Git.",
            "Os logs podem conter caminhos de arquivos. Revise o pacote antes de enviar.",
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildConfigSummary(AppConfig config)
    {
        var safeSummary = new
        {
            config.ShowJsonTab,
            config.ShowGenerateJsonButton,
            config.ShowCopyJsonButton,
            config.UseDefaultOutputDir,
            config.UseDefaultListName,
            config.DefaultCaseMode,
            config.DefaultInputSeparator,
            config.ThemeName,
            config.EditorFontSize,
        };

        return JsonConvert.SerializeObject(safeSummary, Formatting.Indented);
    }

    private static string BuildSizeSummary(SizeConfig sizes) =>
        JsonConvert.SerializeObject(SizeHelper.Normalize(sizes), Formatting.Indented);

    private static void AddRecentLogs(ZipArchive archive, SupportPackageOptions options)
    {
        if (!Directory.Exists(ConfigManager.LogDir) || options.MaxLogFiles <= 0 || options.MaxTotalLogSizeBytes <= 0)
            return;

        var remainingBytes = options.MaxTotalLogSizeBytes;
        var logs = Directory
            .GetFiles(ConfigManager.LogDir, "listforge-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .Take(options.MaxLogFiles);

        foreach (var log in logs)
        {
            if (log.Length > remainingBytes)
                continue;

            archive.CreateEntryFromFile(log.FullName, $"logs/{log.Name}");
            remainingBytes -= log.Length;
        }
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
