using System.Diagnostics;
using System.Text;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;

namespace ListForge.Services;

public interface IProcessingHistoryService
{
    IReadOnlyList<ProcessingHistoryEntry> Load();
    OperationResult<ProcessingHistoryEntry> Add(ProcessingHistoryEntry entry);
    OperationResult Clear();
    OperationResult OpenOutputFolder(ProcessingHistoryEntry entry);
}

public sealed class ProcessingHistoryService : IProcessingHistoryService
{
    public const int MaxEntries = 100;
    public const int SchemaVersion = 1;

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly string _path;
    private readonly Func<DateTimeOffset> _clock;

    public ProcessingHistoryService()
        : this(Path.Combine(ConfigManager.AppDir, "processing-history.json"), () => DateTimeOffset.UtcNow)
    {
    }

    public ProcessingHistoryService(string path, Func<DateTimeOffset>? clock = null)
    {
        _path = path;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<ProcessingHistoryEntry> Load()
    {
        var document = LoadDocument();
        return NormalizeEntries(document.Entries);
    }

    public OperationResult<ProcessingHistoryEntry> Add(ProcessingHistoryEntry entry)
    {
        try
        {
            var stored = new ProcessingHistoryEntry
            {
                Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                ProcessedAt = entry.ProcessedAt == default ? _clock() : entry.ProcessedAt.ToUniversalTime(),
                SourceDisplayName = SanitizeSourceDisplayName(entry.SourceDisplayName, entry.SourceType),
                SourceType = SanitizeSourceType(entry.SourceType),
                ProcessedLineCount = Math.Max(0, entry.ProcessedLineCount),
                OutputPath = entry.OutputPath ?? "",
            };

            if (string.IsNullOrWhiteSpace(stored.OutputPath))
                return OperationResult<ProcessingHistoryEntry>.Fail(
                    "O processamento foi concluído, mas não foi possível atualizar o histórico.",
                    "Histórico sem caminho de saída.",
                    errorCode: "HistoryMissingOutputPath");

            var entries = LoadDocument().Entries;
            entries.Insert(0, stored);
            entries = NormalizeEntries(entries).Take(MaxEntries).ToList();
            SaveDocument(new ProcessingHistoryDocument { SchemaVersion = SchemaVersion, Entries = entries });
            AppLogger.Info("ProcessingHistory", "Registro de histórico adicionado.");
            return OperationResult<ProcessingHistoryEntry>.Ok(stored);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessingHistory", "Falha ao atualizar histórico de processamentos.", ex, _path);
            return OperationResult<ProcessingHistoryEntry>.Fail(
                "O processamento foi concluído, mas não foi possível atualizar o histórico.",
                "Falha ao gravar histórico.",
                ex,
                "ProcessingHistoryWriteFailed");
        }
    }

    public OperationResult Clear()
    {
        try
        {
            SaveDocument(new ProcessingHistoryDocument { SchemaVersion = SchemaVersion, Entries = [] });
            AppLogger.Info("ProcessingHistory", "Histórico de processamentos limpo.");
            return OperationResult.Ok("Histórico limpo.", "Histórico limpo.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessingHistory", "Falha ao limpar histórico de processamentos.", ex, _path);
            return OperationResult.Fail(
                "Não foi possível limpar o histórico.",
                "Falha ao limpar histórico.",
                ex,
                "ProcessingHistoryClearFailed");
        }
    }

    public OperationResult OpenOutputFolder(ProcessingHistoryEntry entry)
    {
        try
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.OutputPath) || !File.Exists(entry.OutputPath))
                return OperationResult.Fail(
                    "O arquivo de saída não foi encontrado.",
                    "Arquivo de saída do histórico não encontrado.",
                    errorCode: "HistoryOutputMissing");

            var argument = $"/select,\"{entry.OutputPath}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
            return OperationResult.Ok("Pasta aberta.", "Pasta da saída aberta.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessingHistory", "Falha ao abrir pasta da saída.", ex, entry?.OutputPath);
            return OperationResult.Fail(
                "Não foi possível abrir a pasta da saída.",
                "Falha ao abrir pasta da saída.",
                ex,
                "HistoryOpenOutputFailed");
        }
    }

    public static ProcessingHistorySource BuildSafeSource(string? inputPath, string? currentFileLabel, string? explicitType = null)
    {
        if (string.Equals(explicitType, ProcessingHistorySourceTypes.Link, StringComparison.OrdinalIgnoreCase))
            return new ProcessingHistorySource("Lista extraída de link", ProcessingHistorySourceTypes.Link);
        if (string.Equals(explicitType, ProcessingHistorySourceTypes.EditedJson, StringComparison.OrdinalIgnoreCase))
            return new ProcessingHistorySource("JSON editado", ProcessingHistorySourceTypes.EditedJson);
        if (string.Equals(explicitType, ProcessingHistorySourceTypes.EditedOutput, StringComparison.OrdinalIgnoreCase))
            return new ProcessingHistorySource("Lista de saída editada", ProcessingHistorySourceTypes.EditedOutput);

        if (!string.IsNullOrWhiteSpace(inputPath))
            return new ProcessingHistorySource(Path.GetFileName(inputPath), ProcessingHistorySourceTypes.File);

        var label = currentFileLabel ?? "";
        if (label.Contains("extraída do link", StringComparison.OrdinalIgnoreCase))
            return new ProcessingHistorySource("Lista extraída de link", ProcessingHistorySourceTypes.Link);
        if (label.StartsWith("Importado de:", StringComparison.OrdinalIgnoreCase))
            return new ProcessingHistorySource(SanitizeSourceDisplayName(label["Importado de:".Length..], ProcessingHistorySourceTypes.ImportedFile), ProcessingHistorySourceTypes.ImportedFile);

        return new ProcessingHistorySource("Texto colado", ProcessingHistorySourceTypes.PastedText);
    }

    private ProcessingHistoryDocument LoadDocument()
    {
        if (!File.Exists(_path))
            return new ProcessingHistoryDocument { SchemaVersion = SchemaVersion };

        try
        {
            var json = File.ReadAllText(_path, Encoding.UTF8);
            var document = JsonConvert.DeserializeObject<ProcessingHistoryDocument>(json);
            return document ?? new ProcessingHistoryDocument { SchemaVersion = SchemaVersion };
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessingHistory", "Falha ao ler histórico. Iniciando vazio.", ex, _path);
            PreserveInvalidFile();
            return new ProcessingHistoryDocument { SchemaVersion = SchemaVersion };
        }
    }

    private void SaveDocument(ProcessingHistoryDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        document.SchemaVersion = SchemaVersion;
        document.Entries = NormalizeEntries(document.Entries).Take(MaxEntries).ToList();

        var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        var json = JsonConvert.SerializeObject(document, Formatting.Indented);
        File.WriteAllText(tempPath, json, Utf8NoBom);
        File.Move(tempPath, _path, overwrite: true);
    }

    private void PreserveInvalidFile()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            var invalidPath = $"{_path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Move(_path, invalidPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessingHistory", "Falha ao preservar arquivo de histórico inválido.", ex, _path);
        }
    }

    private static List<ProcessingHistoryEntry> NormalizeEntries(IEnumerable<ProcessingHistoryEntry>? entries) =>
        (entries ?? [])
            .Where(entry => entry != null)
            .Select(entry => new ProcessingHistoryEntry
            {
                Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                ProcessedAt = entry.ProcessedAt == default ? DateTimeOffset.MinValue : entry.ProcessedAt.ToUniversalTime(),
                SourceDisplayName = SanitizeSourceDisplayName(entry.SourceDisplayName, entry.SourceType),
                SourceType = SanitizeSourceType(entry.SourceType),
                ProcessedLineCount = Math.Max(0, entry.ProcessedLineCount),
                OutputPath = entry.OutputPath ?? "",
            })
            .OrderByDescending(entry => entry.ProcessedAt)
            .ToList();

    private static string SanitizeSourceType(string? sourceType)
    {
        var value = (sourceType ?? "").Trim();
        return value switch
        {
            ProcessingHistorySourceTypes.File => ProcessingHistorySourceTypes.File,
            ProcessingHistorySourceTypes.ImportedFile => ProcessingHistorySourceTypes.ImportedFile,
            ProcessingHistorySourceTypes.PastedText => ProcessingHistorySourceTypes.PastedText,
            ProcessingHistorySourceTypes.Link => ProcessingHistorySourceTypes.Link,
            ProcessingHistorySourceTypes.EditedJson => ProcessingHistorySourceTypes.EditedJson,
            ProcessingHistorySourceTypes.EditedOutput => ProcessingHistorySourceTypes.EditedOutput,
            _ => ProcessingHistorySourceTypes.Unknown,
        };
    }

    private static string SanitizeSourceDisplayName(string? sourceDisplayName, string? sourceType)
    {
        var fallback = SanitizeSourceType(sourceType) switch
        {
            ProcessingHistorySourceTypes.Link => "Lista extraída de link",
            ProcessingHistorySourceTypes.EditedJson => "JSON editado",
            ProcessingHistorySourceTypes.EditedOutput => "Lista de saída editada",
            ProcessingHistorySourceTypes.Unknown => "Arquivo sem nome",
            _ => "Texto colado",
        };

        var text = (sourceDisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        text = text.Replace('\r', ' ').Replace('\n', ' ');
        if (SanitizeSourceType(sourceType) is ProcessingHistorySourceTypes.File or ProcessingHistorySourceTypes.ImportedFile)
        {
            var fileName = Path.GetFileName(text);
            return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out _)
            ? fallback
            : text;
    }
}
