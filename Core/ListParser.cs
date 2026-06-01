using System;
using System.Collections.Generic;
using System.Linq;
using ListForge.Models;

namespace ListForge.Core;

public static class ListParser
{
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
}
