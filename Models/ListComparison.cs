using ListForge.Core;

namespace ListForge.Models;

public enum ComparisonCategory
{
    Matching,
    Reordered,
    Transformed,
    Changed,
    PossiblyMissing,
    Added,
    Uncertain,
}

public sealed record ComparisonFieldDifference(
    string FieldName,
    string InputValue,
    string OutputValue,
    string Reason,
    bool IsExpected);

public sealed record ComparisonItem(
    ComparisonCategory Category,
    int? InputLineNumber,
    int? OutputLineNumber,
    string InputDisplay,
    string OutputDisplay,
    IReadOnlyList<ComparisonFieldDifference> FieldDifferences,
    string Summary,
    string Details)
{
    public string CategoryLabel => Category switch
    {
        ComparisonCategory.Matching => "Correspondente",
        ComparisonCategory.Reordered => "Apenas reorganizado",
        ComparisonCategory.Transformed => "Transformado pelas regras",
        ComparisonCategory.Changed => "Alterado",
        ComparisonCategory.PossiblyMissing => "Possivelmente ausente",
        ComparisonCategory.Added => "Adicionado",
        _ => "Correspondência incerta",
    };

    public string Indicator => Category switch
    {
        ComparisonCategory.Matching => "OK",
        ComparisonCategory.Reordered => "ORDEM",
        ComparisonCategory.Transformed => "REGRA",
        ComparisonCategory.Changed => "AVISO",
        ComparisonCategory.PossiblyMissing => "REVISAR",
        ComparisonCategory.Added => "NOVO",
        _ => "INCERTO",
    };

    public string InputLineDisplay => InputLineNumber?.ToString() ?? "-";
    public string OutputLineDisplay => OutputLineNumber?.ToString() ?? "-";

    public bool RequiresReview => Category is ComparisonCategory.Changed
        or ComparisonCategory.PossiblyMissing
        or ComparisonCategory.Added
        or ComparisonCategory.Uncertain;
}

public sealed record ComparisonSummary(
    int InputRecords,
    int OutputRecords,
    int Matching,
    int Reordered,
    int Transformed,
    int Changed,
    int PossiblyMissing,
    int Added,
    int Uncertain,
    int InputDuplicates,
    int OutputDuplicates)
{
    public int Problems => Changed + PossiblyMissing + Added + Uncertain;
    public bool HasCriticalDifferences => Problems > 0;

    public string StatusMessage => HasCriticalDifferences
        ? "Foram encontradas diferenças que precisam ser revisadas."
        : "Nenhum registro foi perdido ou adicionado durante a organização.";
}

public sealed record ComparisonSnapshot(
    string InputText,
    string OutputText,
    IReadOnlyList<ParsedRow> InputRows,
    IReadOnlyList<ParsedRow> OutputRows,
    SizeConfig SizeConfig,
    string CaseMode,
    ListSortMode SortMode,
    JsonPieceMappingOptions JsonPieceMapping,
    bool AdvancedListEnabled,
    bool OutputWasManuallyEdited,
    string ActiveWorkProfileName,
    DateTimeOffset CreatedAtUtc);

public sealed record ListComparisonResult(
    ComparisonSnapshot Snapshot,
    IReadOnlyList<ComparisonItem> Items,
    ComparisonSummary Summary);

public sealed record ComparisonFilterOption(
    ComparisonCategory? Category,
    string Label,
    int Count)
{
    public string DisplayName => $"{Label} ({Count})";
    public override string ToString() => DisplayName;
}
