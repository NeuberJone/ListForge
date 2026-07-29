using System.Collections.Generic;
using System.Linq;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public sealed class JsonPieceMappingService
{
    public const int MaxPiecePositions = 6;

    public IReadOnlyList<ListParser.ValidationIssue> Validate(
        IReadOnlyList<ParsedRow> rows,
        SizeConfig sizeConfig,
        JsonPieceMappingOptions options)
    {
        if (!options.UseCustomOrder)
            return [];

        var order = options.NormalizedOrder;
        if (order.Count == 0)
            return [new ListParser.ValidationIssue(1, "ordem personalizada sem tipos configurados")];

        if (order.Distinct().Count() != order.Count)
            return [new ListParser.ValidationIssue(1, "A ordem personalizada não pode repetir o mesmo tipo de peça.")];

        foreach (var piece in order)
        {
            if (!PieceTypeMapper.IsKnownKey(piece))
                return [new ListParser.ValidationIssue(1, "ordem personalizada possui tipo de peça inválido")];
        }

        var maxApparelSizes = MaxApparelSizeCount(rows, sizeConfig);
        if (maxApparelSizes > order.Count)
            return [new ListParser.ValidationIssue(1, "A ordem personalizada dos tipos de peça possui menos posições do que os tamanhos encontrados na lista.")];

        return [];
    }

    public int MaxApparelSizeCount(IReadOnlyList<ParsedRow> rows, SizeConfig sizeConfig)
    {
        var max = 0;
        foreach (var row in rows)
        {
            var count = 0;
            var sequentialCount = 0;
            for (var i = 0; i < row.Tams.Count; i++)
            {
                if (!IsApparelSize(row.Tams[i], sizeConfig))
                    continue;

                sequentialCount++;
                var pieceIndex = row.PieceFields != null && i < row.PieceFields.Count
                    ? PieceFieldIndex(row.PieceFields[i])
                    : -1;
                count = System.Math.Max(count, pieceIndex >= 0 ? pieceIndex + 1 : sequentialCount);
            }

            if (count > max) max = count;
        }
        return max;
    }

    public int EstimateRequiredSlots(string inputText, string separator, SizeConfig sizeConfig)
    {
        if (string.IsNullOrWhiteSpace(inputText))
            return 1;

        var max = 0;
        var sep = ListParser.NormalizeSeparator(separator);
        var lines = inputText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var count = 0;
            var sequentialCount = 0;
            var parts = line.Split(sep).Select(part => part.Trim()).ToList();
            for (var i = 0; i < parts.Count; i++)
            {
                if (!IsApparelSize(parts[i], sizeConfig))
                    continue;

                sequentialCount++;
                var columnPosition = i >= 2 ? i - 1 : sequentialCount;
                count = System.Math.Max(count, columnPosition);
            }

            if (count > max) max = count;
        }

        return ClampSlotCount(max == 0 ? 1 : max);
    }

    public static int ClampSlotCount(int value) =>
        System.Math.Clamp(value, 1, MaxPiecePositions);

    private static bool IsApparelSize(string token, SizeConfig sizeConfig)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var (_, size) = SizeHelper.ParseQtyAndSize(token, sizeConfig);
            return SizeHelper.SizeGroupOf(size, sizeConfig) != SizeHelper.GroupSock;
        }
        catch
        {
            return false;
        }
    }

    private static int PieceFieldIndex(string? pieceField)
    {
        var normalized = PieceTypeMapper.NormalizeKey(pieceField);
        for (var i = 0; i < PieceTypeMapper.JsonFields.Count; i++)
        {
            if (PieceTypeMapper.JsonFields[i] == normalized)
                return i;
        }

        return -1;
    }
}
