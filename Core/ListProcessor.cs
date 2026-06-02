using System.Collections.Generic;
using ListForge.Models;

namespace ListForge.Core;

public static class ListProcessor
{
    // ---------------------------------------------------------------
    // Separator helpers
    // ---------------------------------------------------------------
    public static string NormalizeSeparator(string value) =>
        ListParser.NormalizeSeparator(value);

    public static string SeparatorLabel(string value) =>
        ListParser.SeparatorLabel(value);

    // ---------------------------------------------------------------
    // Text helpers
    // ---------------------------------------------------------------
    public static string ApplyCaseMode(string text, string caseMode) =>
        caseMode switch
        {
            "upper" => (text ?? "").ToUpperInvariant(),
            "lower" => (text ?? "").ToLowerInvariant(),
            _ => text ?? "",
        };

    public static string SanitizeBaseFilename(string name) =>
        FileNameHelper.SanitizeBaseFilename(name);

    public static string VersionedPath(string directory, string baseName, string suffix) =>
        FileNameHelper.VersionedPath(directory, baseName, suffix);

    // ---------------------------------------------------------------
    // JSON import extraction
    // ---------------------------------------------------------------
    public static string ExtractListTextFromJsonData(object data, string outputSeparator = ",") =>
        JsonListImporter.ExtractListTextFromJsonData(data, outputSeparator);

    // ---------------------------------------------------------------
    // Line parsing
    // ---------------------------------------------------------------
    public static ParsedRow? ParseLine(string line, string inputSeparator, SizeConfig sizeConfig) =>
        ListParser.ParseLine(line, inputSeparator, sizeConfig);

    public static List<ParsedRow> ProcessText(string text, string inputSeparator, SizeConfig sizeConfig) =>
        ListParser.ProcessText(text, inputSeparator, sizeConfig);

    public static List<ListParser.ValidationIssue> ValidateText(string text, string inputSeparator, SizeConfig sizeConfig) =>
        ListParser.ValidateText(text, inputSeparator, sizeConfig);

    public static List<ParsedRow> SortRows(IEnumerable<ParsedRow> rows, ListSortMode sortMode) =>
        ListRowSorter.SortRows(rows, sortMode);

    public static string CleanTextBySeparator(string text, string separator) =>
        ListParser.CleanTextBySeparator(text, separator);

    // ---------------------------------------------------------------
    // Build output text
    // ---------------------------------------------------------------
    public static string BuildOutput(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original",
        string outputSeparator = ",") =>
        ListOutputBuilder.BuildOutput(rows, sizeConfig, caseMode, outputSeparator);

    // ---------------------------------------------------------------
    // Build orders (for JSON)
    // ---------------------------------------------------------------
    public static List<Dictionary<string, string>> BuildOrdersFromOrderlist(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original") =>
        JsonOrderBuilder.BuildOrdersFromOrderlist(rows, sizeConfig, caseMode);

    public static string BuildJsonPreview(List<Dictionary<string, string>> orders) =>
        JsonOrderBuilder.BuildJsonPreview(orders);

    // ---------------------------------------------------------------
    // Export
    // ---------------------------------------------------------------
    public static string ExportOutputText(string text, string outputDir, string baseName) =>
        ListOutputBuilder.ExportOutputText(text, outputDir, baseName);

    public static string ExportJson(List<Dictionary<string, string>> orders, string outputDir, string baseName) =>
        JsonOrderBuilder.ExportJson(orders, outputDir, baseName);
}
