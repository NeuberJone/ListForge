using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;

namespace ListForge.Services;

public sealed class SupportPackageService
{
    private const int MaxLogFiles = 5;

    public string Generate(string outputDirectory, AboutInfo aboutInfo)
    {
        Directory.CreateDirectory(outputDirectory);

        var packagePath = CreatePackagePath(outputDirectory);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        AddTextEntry(archive, "support-info.txt", BuildSupportInfo(aboutInfo));
        AddTextEntry(archive, "config-summary.txt", BuildConfigSummary(ConfigManager.LoadConfig()));
        AddTextEntry(archive, "sizes-summary.txt", BuildSizeSummary(ConfigManager.LoadSizeConfig()));
        AddRecentLogs(archive);

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

    private static string BuildSupportInfo(AboutInfo info)
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
            "",
            "Privacidade: este pacote não inclui conteúdo completo de listas, saída organizada, JSON de listas reais ou estado interno do Trial.",
            "Antes de enviar, revise o arquivo se houver informações sensíveis nos logs.",
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

    private static void AddRecentLogs(ZipArchive archive)
    {
        if (!Directory.Exists(ConfigManager.LogDir))
            return;

        var logs = Directory
            .GetFiles(ConfigManager.LogDir, "listforge-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .Take(MaxLogFiles);

        foreach (var log in logs)
        {
            archive.CreateEntryFromFile(log.FullName, $"logs/{log.Name}");
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
