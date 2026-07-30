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

public sealed record SupportPackageSnapshot(
    string InputText,
    string OutputText,
    SettingsExportSnapshot Settings);

public sealed class SupportPackageService
{
    private static readonly SupportPackageOptions DefaultOptions = new();
    private readonly SettingsExportService _settingsExportService;

    public SupportPackageService()
        : this(new SettingsExportService())
    {
    }

    public SupportPackageService(SettingsExportService settingsExportService)
    {
        _settingsExportService = settingsExportService;
    }

    public OperationResult<string> Generate(string outputDirectory, AboutInfo aboutInfo)
    {
        return Generate(outputDirectory, aboutInfo, DefaultOptions);
    }

    public OperationResult<string> Generate(string outputDirectory, AboutInfo aboutInfo, SupportPackageOptions options)
    {
        var snapshot = new SupportPackageSnapshot(
            "",
            "",
            new SettingsExportSnapshot(ConfigManager.LoadConfig(), ConfigManager.LoadSizeConfig(), aboutInfo.Version));
        return Generate(outputDirectory, aboutInfo, options, snapshot);
    }

    public OperationResult<string> Generate(
        string outputDirectory,
        AboutInfo aboutInfo,
        SupportPackageOptions options,
        SupportPackageSnapshot snapshot)
    {
        try
        {
            return OperationResult<string>.Ok(
                GeneratePackage(outputDirectory, aboutInfo, NormalizeOptions(options), snapshot),
                "Pacote de suporte gerado com sucesso.");
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                "Falha ao gerar pacote de suporte.",
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

    private string GeneratePackage(
        string outputDirectory,
        AboutInfo aboutInfo,
        SupportPackageOptions options,
        SupportPackageSnapshot snapshot)
    {
        Directory.CreateDirectory(outputDirectory);

        var packagePath = CreatePackagePath(outputDirectory);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        AddTextEntry(archive, "support-info.txt", BuildSupportInfo(aboutInfo, options, snapshot));
        AddTextEntry(archive, "config-summary.txt", BuildConfigSummary(snapshot.Settings.Config));
        AddTextEntry(archive, "sizes-summary.txt", BuildSizeSummary(snapshot.Settings.Sizes));
        AddTextEntry(archive, "lista-entrada.txt", snapshot.InputText ?? "");
        AddTextEntry(archive, "lista-saida.txt", snapshot.OutputText ?? "");
        AddTextEntry(archive, "configuracoes.json", _settingsExportService.BuildJson(snapshot.Settings));

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

    private static string BuildSupportInfo(AboutInfo info, SupportPackageOptions options, SupportPackageSnapshot snapshot)
    {
        var lines = new[]
        {
            "ListForge",
            $"Versao: {info.Version}",
            $"Edicao: {info.Edition}",
            $"Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Sistema operacional: {RuntimeInformation.OSDescription}",
            $"Arquitetura: {RuntimeInformation.OSArchitecture}",
            $"Pasta de configuracao: {info.ConfigPath}",
            $"Pasta de logs: {info.LogsPath}",
            $"Logs recentes incluidos: {(options.IncludeLogs ? "sim" : "nao")}",
            $"Limite de arquivos de log: {options.MaxLogFiles}",
            $"Limite total de logs: {options.MaxTotalLogSizeBytes} bytes",
            $"Entrada atual incluida: {(string.IsNullOrEmpty(snapshot.InputText) ? "vazia" : "sim")}",
            $"Saida atual incluida: {(string.IsNullOrEmpty(snapshot.OutputText) ? "vazia" : "sim")}",
            "Configuracoes exportadas incluidas: sim",
            "",
            "Privacidade: este pacote inclui a entrada atual, a saida atual e as configuracoes exportadas para diagnostico. Ele nao inclui JSON de listas reais, arquivos externos do usuario, estado interno do Trial, tokens, senhas, chaves, build/dist ou repositorio Git.",
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
