using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ListForge.Models;

namespace ListForge.Core;

public static class SizeHelper
{
    public const string GroupMale = "male";
    public const string GroupFemale = "female";
    public const string GroupChild = "child";

    public static readonly Dictionary<string, string> GroupLabels = new()
    {
        [GroupMale] = "Masculino",
        [GroupFemale] = "Feminino",
        [GroupChild] = "Infantil",
    };

    private static readonly Regex QtySizeRe =
        new(@"^\s*(\d+)\s*-\s*([A-Za-z0-9]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---------------------------------------------------------------
    // Token helpers
    // ---------------------------------------------------------------
    public static string NormalizeToken(string value) =>
        Regex.Replace((value ?? "").Trim().ToUpperInvariant(), @"\s+", "");

    private static List<string> DedupeKeepOrder(IEnumerable<string> values)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var v in values)
        {
            var n = NormalizeToken(v);
            if (string.IsNullOrEmpty(n) || !seen.Add(n)) continue;
            result.Add(n);
        }
        return result;
    }

    // ---------------------------------------------------------------
    // Config normalization
    // ---------------------------------------------------------------
    public static SizeConfig Normalize(SizeConfig raw)
    {
        var def = SizeConfig.Default();
        var result = new SizeConfig();

        foreach (var groupKey in new[] { GroupMale, GroupFemale, GroupChild })
        {
            raw.Groups.TryGetValue(groupKey, out var rawGroup);
            var defGroup = def.Groups[groupKey];

            var merged = new SizeGroupConfig
            {
                Label = string.IsNullOrWhiteSpace(rawGroup?.Label) ? defGroup.Label : rawGroup.Label.Trim(),
                BaseSizes = DedupeKeepOrder(rawGroup?.BaseSizes ?? defGroup.BaseSizes),
                Prefixes = DedupeKeepOrder(rawGroup?.Prefixes ?? defGroup.Prefixes),
                Suffixes = DedupeKeepOrder(rawGroup?.Suffixes ?? defGroup.Suffixes),
            };
            result.Groups[groupKey] = merged;
        }
        return result;
    }

    // ---------------------------------------------------------------
    // Size building
    // ---------------------------------------------------------------
    public static List<string> BuildGroupSizes(SizeGroupConfig group)
    {
        var bases = DedupeKeepOrder(group.BaseSizes);
        var prefixes = DedupeKeepOrder(group.Prefixes);
        var suffixes = DedupeKeepOrder(group.Suffixes);

        var prefixOpts = prefixes.Count > 0 ? prefixes : new List<string> { "" };
        var suffixOpts = suffixes.Count > 0 ? suffixes : new List<string> { "" };

        var sizes = new List<string>();
        var seen = new HashSet<string>();

        foreach (var prefix in prefixOpts)
            foreach (var base_ in bases)
                foreach (var suffix in suffixOpts)
                {
                    var size = $"{prefix}{base_}{suffix}";
                    if (!string.IsNullOrEmpty(size) && seen.Add(size))
                        sizes.Add(size);
                }

        return sizes;
    }

    public static Dictionary<string, string> BuildSizeIndex(SizeConfig config)
    {
        var index = new Dictionary<string, string>();
        var cfg = Normalize(config);
        foreach (var (groupKey, group) in cfg.Groups)
            foreach (var size in BuildGroupSizes(group))
                index[size] = groupKey;
        return index;
    }

    public static bool IsValidSize(string token, SizeConfig config)
    {
        var text = NormalizeToken(token);
        return !string.IsNullOrEmpty(text) && BuildSizeIndex(config).ContainsKey(text);
    }

    // ---------------------------------------------------------------
    // Parsing
    // ---------------------------------------------------------------
    public static (int qty, string size) ParseQtyAndSize(string token, SizeConfig config)
    {
        var text = (token ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Tamanho vazio (não permitido).");

        var match = QtySizeRe.Match(text);
        if (match.Success)
        {
            var qty = int.Parse(match.Groups[1].Value);
            var size = NormalizeToken(match.Groups[2].Value);
            if (qty <= 0) throw new ArgumentException("Quantidade inválida (<= 0).");
            if (!IsValidSize(size, config)) throw new ArgumentException($"Tamanho inválido: {size}");
            return (qty, size);
        }

        var normalized = NormalizeToken(text);
        if (!IsValidSize(normalized, config))
            throw new ArgumentException($"Tamanho inválido: {normalized}");
        return (1, normalized);
    }

    public static string SizeGroupOf(string size, SizeConfig config)
    {
        var normalized = NormalizeToken(size);
        var index = BuildSizeIndex(config);
        if (!index.TryGetValue(normalized, out var group))
            throw new ArgumentException($"Tamanho inválido: {normalized}");
        return group;
    }

    public static string GenderFromSize(string size, SizeConfig config)
    {
        var group = SizeGroupOf(size, config);
        return group switch
        {
            GroupChild => "C",
            GroupFemale => "FE",
            _ => "MA",
        };
    }

    public static string FormatSizeToken(string token, SizeConfig config)
    {
        var (qty, size) = ParseQtyAndSize(token, config);
        return qty == 1 ? size : $"{qty}-{size}";
    }

    // ---------------------------------------------------------------
    // CSV helpers (for UI settings)
    // ---------------------------------------------------------------
    public static List<string> ParseCsvTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return DedupeKeepOrder(text.Split(',').Select(p => p.Trim()));
    }

    public static string TokensToCsv(IEnumerable<string> values) =>
        string.Join(", ", DedupeKeepOrder(values));

    // ---------------------------------------------------------------
    // Summary text
    // ---------------------------------------------------------------
    public static string BuildSizeSummary(SizeConfig cfg)
    {
        var male = BuildGroupSizes(cfg.Groups[GroupMale]);
        var female = BuildGroupSizes(cfg.Groups[GroupFemale]);
        var child = BuildGroupSizes(cfg.Groups[GroupChild]);
        var total = male.Union(female).Union(child).Distinct().Count();

        var maleStr = male.Count > 0 ? string.Join(", ", male) : "(nenhum)";
        var femaleStr = female.Count > 0 ? string.Join(", ", female) : "(nenhum)";
        var childStr = child.Count > 0 ? string.Join(", ", child) : "(nenhum)";

        return $"Tamanhos válidos atuais:\n• Masculino: {maleStr}\n• Feminino: {femaleStr}\n• Infantil: {childStr}\n• Total: {total}";
    }

    public static SizeConfig UpdateGroupConfig(
        SizeConfig config,
        string groupKey,
        List<string> baseSizes,
        List<string> prefixes,
        List<string> suffixes,
        string? label = null)
    {
        var cfg = Normalize(config);
        if (!cfg.Groups.ContainsKey(groupKey))
            throw new ArgumentException($"Grupo de tamanhos inválido: {groupKey}");

        var current = cfg.Groups[groupKey];
        cfg.Groups[groupKey] = new SizeGroupConfig
        {
            Label = string.IsNullOrWhiteSpace(label) ? current.Label : label.Trim(),
            BaseSizes = DedupeKeepOrder(baseSizes),
            Prefixes = DedupeKeepOrder(prefixes),
            Suffixes = DedupeKeepOrder(suffixes),
        };
        return cfg;
    }
}
