using System.Collections.Generic;
using System.Linq;

namespace ListForge.Core;

public sealed record PieceTypeOption(string Key, string Label)
{
    public override string ToString() => Label;
}

public static class PieceTypeMapper
{
    public const string ShortSleeve = "ShortSleeve";
    public const string LongSleeve = "LongSleeve";
    public const string Short = "Short";
    public const string Pants = "Pants";
    public const string Tanktop = "Tanktop";
    public const string Vest = "Vest";

    public static IReadOnlyList<PieceTypeOption> AvailableOptions { get; } =
    [
        new(ShortSleeve, "Manga Curta"),
        new(LongSleeve, "Manga Longa"),
        new(Short, "Short"),
        new(Pants, "Calça"),
        new(Tanktop, "Regata"),
        new(Vest, "Colete"),
    ];

    public static IReadOnlyList<string> JsonFields { get; } =
    [
        ShortSleeve,
        LongSleeve,
        Short,
        Pants,
        Tanktop,
        Vest,
    ];

    public static string NormalizeKey(string? value)
    {
        var text = (value ?? "").Trim();
        var byKey = AvailableOptions.FirstOrDefault(option =>
            string.Equals(option.Key, text, System.StringComparison.OrdinalIgnoreCase));
        if (byKey != null) return byKey.Key;

        var byLabel = AvailableOptions.FirstOrDefault(option =>
            string.Equals(option.Label, text, System.StringComparison.OrdinalIgnoreCase));
        return byLabel?.Key ?? text;
    }

    public static bool IsKnownKey(string? value) =>
        AvailableOptions.Any(option => option.Key == NormalizeKey(value));

    public static string LabelFromKey(string? value)
    {
        var key = NormalizeKey(value);
        return AvailableOptions.FirstOrDefault(option => option.Key == key)?.Label ?? "";
    }
}
