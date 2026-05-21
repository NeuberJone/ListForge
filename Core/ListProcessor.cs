using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ListForge.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ListForge.Core;

public static class ListProcessor
{
    private static readonly string[] GroupRenderOrder = ["male", "female", "child"];

    // ---------------------------------------------------------------
    // Separator helpers
    // ---------------------------------------------------------------
    public static string NormalizeSeparator(string value)
    {
        var raw = (value ?? "").Trim();
        return raw is @"\t" or "TAB" or "tab" ? "\t" : (raw == "" ? "," : raw);
    }

    public static string SeparatorLabel(string value)
    {
        var sep = NormalizeSeparator(value);
        return sep == "\t" ? @"\t" : sep;
    }

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

    public static string SanitizeBaseFilename(string name)
    {
        var text = (name ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            text = DateTime.Now.ToString("lista-yyyyMMdd-HHmmss");

        foreach (var ch in @"\/:*?""<>|")
            text = text.Replace(ch.ToString(), "_");

        text = Regex.Replace(text, @"\s+", " ").Trim(' ', '.');
        return string.IsNullOrEmpty(text) ? DateTime.Now.ToString("lista-yyyyMMdd-HHmmss") : text;
    }

    public static string VersionedPath(string directory, string baseName, string suffix)
    {
        var safeBase = SanitizeBaseFilename(baseName);
        var path = Path.Combine(directory, $"{safeBase}{suffix}");
        if (!File.Exists(path)) return path;

        var idx = 2;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{safeBase}_v{idx}{suffix}");
            if (!File.Exists(candidate)) return candidate;
            idx++;
        }
    }

    // ---------------------------------------------------------------
    // JSON import extraction
    // ---------------------------------------------------------------
    private static readonly string[] JsonImportFieldOrder =
        ["Name", "Number", "ShortSleeve", "LongSleeve", "Short", "Pants", "Tanktop", "Vest", "Nickname", "BloodType"];

    private static readonly HashSet<string> JsonImportMandatory = ["Name", "Number"];

    private static string NormalizeJsonImportValue(object? value) =>
        value == null ? "" : value.ToString()!.Replace("\r", "").Replace("\n", " ").Trim();

    private static List<string> DecideEffectiveFields(List<Dictionary<string, object?>> orders)
    {
        var present = new HashSet<string>();
        foreach (var entry in orders)
            foreach (var key in JsonImportFieldOrder)
            {
                if (JsonImportMandatory.Contains(key)) continue;
                if (!string.IsNullOrEmpty(NormalizeJsonImportValue(entry.GetValueOrDefault(key))))
                    present.Add(key);
            }

        return JsonImportFieldOrder
            .Where(k => JsonImportMandatory.Contains(k) || present.Contains(k))
            .ToList();
    }

    public static string ExtractListTextFromJsonData(object data, string outputSeparator = ",")
    {
        List<Dictionary<string, object?>> orders;

        if (data is JArray arr)
        {
            orders = arr.Select(t => t.ToObject<Dictionary<string, object?>>()!).ToList();
        }
        else if (data is JObject obj)
        {
            var ordersToken = obj["orders"] as JArray
                ?? throw new ArgumentException("Campo 'orders' inválido (não é lista).");
            orders = ordersToken.Select(t => t.ToObject<Dictionary<string, object?>>()!).ToList();
        }
        else
        {
            throw new ArgumentException("A resposta precisa ser um objeto com 'orders' ou uma lista de pedidos.");
        }

        var fields = DecideEffectiveFields(orders);
        var lines = new List<string>();

        foreach (var entry in orders)
        {
            var rowValues = fields.Select(f => NormalizeJsonImportValue(entry.GetValueOrDefault(f))).ToList();
            var expandedRows = new List<string>();

            bool expanded = false;
            for (var i = 0; i < rowValues.Count; i++)
            {
                var m = Regex.Match(rowValues[i], @"^(\d+)-(.+)$");
                if (m.Success)
                {
                    var qty = int.Parse(m.Groups[1].Value);
                    var baseValue = m.Groups[2].Value.Trim();
                    for (var q = 0; q < qty; q++)
                    {
                        var newRow = new List<string>(rowValues) { [i] = baseValue };
                        expandedRows.Add(string.Join(outputSeparator, newRow));
                    }
                    expanded = true;
                    break;
                }
            }

            if (!expanded)
                expandedRows.Add(string.Join(outputSeparator, rowValues));

            lines.AddRange(expandedRows);
        }

        return string.Join("\n", lines);
    }

    // ---------------------------------------------------------------
    // Line parsing
    // ---------------------------------------------------------------
    private static bool IsNumber(string token) =>
        !string.IsNullOrEmpty(token.Trim()) && token.Trim().All(char.IsDigit);

    private static bool IsSize(string token, SizeConfig config)
    {
        var text = token.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        try { SizeHelper.ParseQtyAndSize(text, config); return true; }
        catch { return SizeHelper.IsValidSize(text, config); }
    }

    public static ParsedRow? ParseLine(string line, string inputSeparator, SizeConfig sizeConfig)
    {
        var raw = line.Trim();
        if (string.IsNullOrEmpty(raw)) return null;

        var sep = NormalizeSeparator(inputSeparator);
        var parts = raw.Split(sep).Select(p => p.Trim()).ToList();

        var name = "";
        var number = "";
        var tams = new List<string>();
        var extras = new List<string>();

        foreach (var token in parts)
        {
            if (string.IsNullOrEmpty(token)) continue;

            if (IsSize(token, sizeConfig)) { tams.Add(token); continue; }
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
            extras.Count >= 2 ? extras[1] : "");
    }

    public static List<ParsedRow> ProcessText(string text, string inputSeparator, SizeConfig sizeConfig)
    {
        var parsed = new List<ParsedRow>();
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var row = ParseLine(line, inputSeparator, sizeConfig);
                if (row != null) parsed.Add(row);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Linha {i + 1}: {ex.Message}");
            }
        }

        parsed.Sort((a, b) =>
        {
            var c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Number, b.Number, StringComparison.Ordinal);
        });

        return parsed;
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

    // ---------------------------------------------------------------
    // Explode & normalize
    // ---------------------------------------------------------------
    private static List<(ParsedRow row, string group)> ExplodeRowFragments(ParsedRow row, SizeConfig config)
    {
        var groupedSingle = GroupRenderOrder.ToDictionary(g => g, _ => new List<string>());
        var exploded = new List<(ParsedRow, string)>();

        foreach (var token in row.Tams)
        {
            var (qty, size) = SizeHelper.ParseQtyAndSize(token, config);
            var group = SizeHelper.SizeGroupOf(size, config);

            if (qty > 1)
            {
                for (var i = 0; i < qty; i++)
                    exploded.Add((new ParsedRow(row.Name, row.Number, [size], row.S2, row.S3), group));
            }
            else
            {
                groupedSingle[group].Add(size);
            }
        }

        foreach (var group in GroupRenderOrder)
        {
            var sizes = groupedSingle[group];
            if (sizes.Count > 0)
                exploded.Add((new ParsedRow(row.Name, row.Number, sizes, row.S2, row.S3), group));
        }

        return exploded;
    }

    private static Dictionary<string, int> GroupColumnWidths(List<(ParsedRow row, string group)> normalized)
    {
        var widths = GroupRenderOrder.ToDictionary(g => g, _ => 0);
        foreach (var (row, group) in normalized)
            widths[group] = Math.Max(widths[group], row.Tams.Count);
        return widths;
    }

    // ---------------------------------------------------------------
    // Build output text
    // ---------------------------------------------------------------
    public static string BuildOutput(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original",
        string outputSeparator = ",")
    {
        if (rows.Count == 0) return "";

        var hasS2 = rows.Any(r => !string.IsNullOrEmpty(r.S2));
        var hasS3 = rows.Any(r => !string.IsNullOrEmpty(r.S3));
        var outLines = new List<string>();

        foreach (var sourceRow in rows)
        {
            var fragments = ExplodeRowFragments(sourceRow, sizeConfig);
            if (fragments.Count == 0) continue;

            var widths = GroupColumnWidths(fragments);
            var activeGroups = GroupRenderOrder.Where(g => widths[g] > 0).ToList();

            foreach (var (row, rowGroup) in fragments)
            {
                var cols = new List<string>
                {
                    ApplyCaseMode(row.Name, caseMode),
                    row.Number,
                };

                foreach (var group in activeGroups)
                {
                    if (group == rowGroup)
                    {
                        var groupSizes = row.Tams.Select(s => SizeHelper.FormatSizeToken(s, sizeConfig)).ToList();
                        groupSizes.AddRange(Enumerable.Repeat("", widths[group] - groupSizes.Count));
                        cols.AddRange(groupSizes);
                    }
                    else
                    {
                        cols.AddRange(Enumerable.Repeat("", widths[group]));
                    }
                }

                if (hasS2) cols.Add(ApplyCaseMode(row.S2, caseMode));
                if (hasS3) cols.Add(ApplyCaseMode(row.S3, caseMode));

                outLines.Add(string.Join(outputSeparator, cols));
            }
        }

        return string.Join("\n", outLines);
    }

    // ---------------------------------------------------------------
    // Build orders (for JSON)
    // ---------------------------------------------------------------
    public static List<Dictionary<string, string>> BuildOrdersFromOrderlist(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original")
    {
        var orders = new List<Dictionary<string, string>>();

        var normalized = rows
            .SelectMany(r => ExplodeRowFragments(r, sizeConfig))
            .ToList();

        foreach (var (row, _) in normalized)
        {
            foreach (var tam in row.Tams)
            {
                var (qty, size) = SizeHelper.ParseQtyAndSize(tam, sizeConfig);
                var gender = SizeHelper.GenderFromSize(size, sizeConfig);

                for (var i = 0; i < qty; i++)
                {
                    orders.Add(new Dictionary<string, string>
                    {
                        ["Name"] = ApplyCaseMode(row.Name, caseMode),
                        ["Nickname"] = ApplyCaseMode(row.S2, caseMode),
                        ["Number"] = row.Number,
                        ["BloodType"] = ApplyCaseMode(row.S3, caseMode),
                        ["Gender"] = gender,
                        ["ShortSleeve"] = size,
                        ["LongSleeve"] = "",
                        ["Short"] = "",
                        ["Pants"] = "",
                        ["Tanktop"] = "",
                        ["Vest"] = "",
                    });
                }
            }
        }

        return orders;
    }

    private static JObject WrapOrders(List<Dictionary<string, string>> orders) =>
        new()
        {
            ["title"] = "List",
            ["order_number"] = 0,
            ["client_name"] = "",
            ["orders"] = JArray.FromObject(orders),
            ["unique_name_chars"] = "",
            ["unique_nickname_chars"] = "",
        };

    public static string BuildJsonPreview(List<Dictionary<string, string>> orders) =>
        WrapOrders(orders).ToString(Formatting.Indented);

    // ---------------------------------------------------------------
    // Export
    // ---------------------------------------------------------------
    public static string ExportOutputText(string text, string outputDir, string baseName)
    {
        Directory.CreateDirectory(outputDir);
        var path = VersionedPath(outputDir, baseName, ".txt");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    public static string ExportJson(List<Dictionary<string, string>> orders, string outputDir, string baseName)
    {
        Directory.CreateDirectory(outputDir);
        var path = VersionedPath(outputDir, baseName, ".json");
        File.WriteAllText(path, WrapOrders(orders).ToString(Formatting.Indented), new UTF8Encoding(false));
        return path;
    }
}
