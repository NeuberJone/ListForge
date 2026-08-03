using System.Globalization;

namespace ListForge.Models;

public static class ProcessingHistorySourceTypes
{
    public const string File = "File";
    public const string ImportedFile = "ImportedFile";
    public const string PastedText = "PastedText";
    public const string Link = "Link";
    public const string EditedJson = "EditedJson";
    public const string EditedOutput = "EditedOutput";
    public const string Unknown = "Unknown";
}

public sealed class ProcessingHistoryEntry
{
    public string Id { get; init; } = "";
    public DateTimeOffset ProcessedAt { get; init; }
    public string SourceDisplayName { get; init; } = "";
    public string SourceType { get; init; } = ProcessingHistorySourceTypes.Unknown;
    public int ProcessedLineCount { get; init; }
    public string OutputPath { get; init; } = "";

    public string ProcessedAtDisplay => ProcessedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
}

public sealed class ProcessingHistoryDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<ProcessingHistoryEntry> Entries { get; set; } = [];
}

public sealed record ProcessingHistorySource(string DisplayName, string SourceType);
