using System;
using System.Collections.Generic;
using System.Linq;
using ListForge.Models;

namespace ListForge.Core;

public static class ListParser
{
    public sealed record ValidationIssue(int LineNumber, string Message);
    private sealed record HeaderContext(IReadOnlyList<string> Fields);

    private static readonly Dictionary<string, string> PieceHeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShortSleeve"] = PieceTypeMapper.ShortSleeve,
        ["Manga Curta"] = PieceTypeMapper.ShortSleeve,
        ["MangaCurta"] = PieceTypeMapper.ShortSleeve,
        ["Camiseta"] = PieceTypeMapper.ShortSleeve,
        ["LongSleeve"] = PieceTypeMapper.LongSleeve,
        ["Manga Longa"] = PieceTypeMapper.LongSleeve,
        ["MangaLonga"] = PieceTypeMapper.LongSleeve,
        ["Short"] = PieceTypeMapper.Short,
        ["Bermuda"] = PieceTypeMapper.Short,
        ["Pants"] = PieceTypeMapper.Pants,
        ["Calca"] = PieceTypeMapper.Pants,
        ["Calça"] = PieceTypeMapper.Pants,
        ["Tanktop"] = PieceTypeMapper.Tanktop,
        ["Regata"] = PieceTypeMapper.Tanktop,
        ["Vest"] = PieceTypeMapper.Vest,
        ["Colete"] = PieceTypeMapper.Vest,
    };

    public static string NormalizeSeparator(string value)
    {
        var raw = (value ?? "").Trim();
        // Legacy tab values remain accepted for compatibility with older saved settings.
        return raw is @"\t" or "TAB" or "tab" ? "\t" : (raw == "" ? "," : raw);
    }

    public static string SeparatorLabel(string value)
    {
        var sep = NormalizeSeparator(value);
        return sep == "\t" ? @"\t" : sep;
    }

    private static bool IsNumber(string token) =>
        !string.IsNullOrEmpty(token.Trim()) && token.Trim().All(char.IsDigit);

    private static bool IsSize(string token, SizeConfig config)
    {
        var text = token.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        try { SizeHelper.ParseQtyAndSize(text, config); return true; }
        catch { return SizeHelper.IsValidSize(text, config); }
    }

    private static string InferPieceFieldFromColumn(int columnIndex)
    {
        var pieceIndex = columnIndex - 2;
        return pieceIndex >= 0 && pieceIndex < PieceTypeMapper.JsonFields.Count
            ? PieceTypeMapper.JsonFields[pieceIndex]
            : "";
    }

    private static bool TryMapPieceHeader(string token, out string pieceField)
    {
        var key = token.Trim();
        return PieceHeaderAliases.TryGetValue(key, out pieceField!);
    }

    private static bool IsNameHeader(string token) =>
        token.Trim().Equals("Name", StringComparison.OrdinalIgnoreCase)
        || token.Trim().Equals("Nome", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumberHeader(string token) =>
        token.Trim().Equals("Number", StringComparison.OrdinalIgnoreCase)
        || token.Trim().Equals("Numero", StringComparison.OrdinalIgnoreCase)
        || token.Trim().Equals("Número", StringComparison.OrdinalIgnoreCase);

    private static HeaderContext? TryParseHeaderContext(IReadOnlyList<string> parts)
    {
        if (!parts.Any(part => TryMapPieceHeader(part, out _)))
            return null;

        var hasNameOrNumber = parts.Any(IsNameHeader) || parts.Any(IsNumberHeader);
        if (!hasNameOrNumber && parts.Count(part => TryMapPieceHeader(part, out _)) < 2)
            return null;

        var fields = parts
            .Select(part => TryMapPieceHeader(part, out var pieceField) ? pieceField : "")
            .ToList();

        return new HeaderContext(fields);
    }

    private static bool IsHeaderLine(string raw, string separator, out HeaderContext? context)
    {
        var parts = raw.Split(separator)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        context = TryParseHeaderContext(parts);
        return context != null;
    }

    public static ParsedRow? ParseLine(string line, string inputSeparator, SizeConfig sizeConfig)
        => ParseLine(line, inputSeparator, sizeConfig, null);

    private static ParsedRow? ParseLine(string line, string inputSeparator, SizeConfig sizeConfig, HeaderContext? headerContext)
    {
        var raw = line.Trim();
        if (string.IsNullOrEmpty(raw)) return null;

        var sep = NormalizeSeparator(inputSeparator);
        var parts = raw.Split(sep).Select(p => p.Trim()).ToList();

        var name = "";
        var number = "";
        var tams = new List<string>();
        var pieceFields = new List<string>();
        var extras = new List<string>();

        for (var i = 0; i < parts.Count; i++)
        {
            var token = parts[i];
            if (string.IsNullOrEmpty(token)) continue;

            if (IsSize(token, sizeConfig))
            {
                tams.Add(token);
                pieceFields.Add(headerContext != null && i < headerContext.Fields.Count
                    ? headerContext.Fields[i]
                    : InferPieceFieldFromColumn(i));
                continue;
            }
            if (IsNumber(token) && string.IsNullOrEmpty(number)) { number = token; continue; }
            if (string.IsNullOrEmpty(name)) { name = token; continue; }
            extras.Add(token);
        }

        if (tams.Count == 0)
            throw new ArgumentException($"Sem TAM reconhecido: {raw}");
        if (tams.Count > 6)
            throw new ArgumentException($"Mais de 6 TAMs na linha: {raw}");

        return new ParsedRow(
            name,
            number,
            tams,
            extras.Count >= 1 ? extras[0] : "",
            extras.Count >= 2 ? extras[1] : "",
            pieceFields.Any(field => !string.IsNullOrEmpty(field)) ? pieceFields : null);
    }

    public static List<ParsedRow> ProcessText(string text, string inputSeparator, SizeConfig sizeConfig)
    {
        var parsed = new List<ParsedRow>();
        var lines = text.Split('\n');
        var sep = NormalizeSeparator(inputSeparator);
        HeaderContext? headerContext = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (IsHeaderLine(line, sep, out var parsedHeader))
            {
                headerContext = parsedHeader;
                continue;
            }

            try
            {
                var row = ParseLine(line, inputSeparator, sizeConfig, headerContext);
                if (row != null)
                {
                    parsed.Add(row with
                    {
                        SourceId = $"source-{i + 1}-{parsed.Count + 1}",
                        SourceLineNumber = i + 1,
                    });
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Linha {i + 1}: {ex.Message}");
            }
        }
        return parsed;
    }

    public static List<ValidationIssue> ValidateText(string text, string inputSeparator, SizeConfig sizeConfig)
    {
        var issues = new List<ValidationIssue>();
        var lines = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var sep = NormalizeSeparator(inputSeparator);

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;

            if (IsHeaderLine(raw, sep, out _))
                continue;

            var parts = raw.Split(sep)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            var sizeCount = parts.Count(part => IsSize(part, sizeConfig));
            if (sizeCount > 6)
            {
                issues.Add(new ValidationIssue(i + 1, "mais de 6 tamanhos"));
                continue;
            }

            if (sizeCount == 0)
            {
                var message = parts.Count >= 3
                    ? "tamanho não reconhecido"
                    : "sem tamanho";
                issues.Add(new ValidationIssue(i + 1, message));
            }
        }

        return issues;
    }

    public static string CleanTextBySeparator(string text, string separator)
    {
        var sep = NormalizeSeparator(separator);
        if (string.IsNullOrEmpty(sep)) throw new ArgumentException("Separador inválido.");

        var cleaned = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) { cleaned.Add(""); continue; }
            var stripped = line.TrimStart();
            var parts = stripped.Split(sep).Select(p => p.Trim());
            cleaned.Add(string.Join(sep, parts));
        }
        return string.Join("\n", cleaned);
    }
}
