using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ListForge.Models;

namespace ListForge.Core;

internal sealed record RowFragment(ParsedRow Row, string Group, IReadOnlyList<string> Socks, IReadOnlyList<string> PieceFields);
internal sealed record SizeColumn(string PieceField, IReadOnlyList<string> Sizes);

public static class ListOutputBuilder
{
    internal static readonly string[] GroupRenderOrder = ["male", "female", "child"];

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

    internal static List<RowFragment> ExplodeRowFragments(ParsedRow row, SizeConfig config)
    {
        var groupedColumns = GroupRenderOrder.ToDictionary(g => g, _ => new List<SizeColumn>());
        var exploded = new List<RowFragment>();
        var socks = new List<string>();

        for (var tokenIndex = 0; tokenIndex < row.Tams.Count; tokenIndex++)
        {
            var token = row.Tams[tokenIndex];
            var (qty, size) = SizeHelper.ParseQtyAndSize(token, config);
            var group = SizeHelper.SizeGroupOf(size, config);

            if (group == SizeHelper.GroupSock)
            {
                for (var i = 0; i < qty; i++)
                    socks.Add(size);
                continue;
            }

            var pieceField = row.PieceFields != null && tokenIndex < row.PieceFields.Count
                ? row.PieceFields[tokenIndex]
                : "";
            groupedColumns[group].Add(new SizeColumn(pieceField, Enumerable.Repeat(size, qty).ToList()));
        }

        foreach (var group in GroupRenderOrder)
        {
            var columns = groupedColumns[group];
            if (columns.Count == 0) continue;

            var rowCount = columns.Max(c => c.Sizes.Count);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var sizes = new List<string>();
                var pieceFields = new List<string>();

                foreach (var column in columns)
                {
                    if (rowIndex >= column.Sizes.Count)
                        continue;

                    var size = column.Sizes[rowIndex];
                    if (string.IsNullOrEmpty(size))
                        continue;

                    sizes.Add(size);
                    pieceFields.Add(column.PieceField);
                }

                if (sizes.Count > 0)
                    exploded.Add(new RowFragment(
                        new ParsedRow(
                            row.Name,
                            row.Number,
                            sizes,
                            row.S2,
                            row.S3,
                            SourceId: row.SourceId,
                            SourceLineNumber: row.SourceLineNumber),
                        group,
                        [],
                        pieceFields));
            }
        }

        if (exploded.Count == 0 && socks.Count > 0)
            exploded.Add(new RowFragment(
                new ParsedRow(
                    row.Name,
                    row.Number,
                    [],
                    row.S2,
                    row.S3,
                    SourceId: row.SourceId,
                    SourceLineNumber: row.SourceLineNumber),
                "",
                socks,
                []));
        else if (socks.Count > 0)
            exploded[0] = exploded[0] with { Socks = socks };

        return exploded;
    }

    private static Dictionary<string, int> GroupColumnWidths(List<RowFragment> normalized)
    {
        var widths = GroupRenderOrder.ToDictionary(g => g, _ => 0);
        foreach (var fragment in normalized)
        {
            if (!widths.ContainsKey(fragment.Group)) continue;
            var maxPieceIndex = fragment.PieceFields
                .Select(PieceFieldIndex)
                .DefaultIfEmpty(-1)
                .Max();
            var width = System.Math.Max(fragment.Row.Tams.Count, maxPieceIndex + 1);
            widths[fragment.Group] = System.Math.Max(widths[fragment.Group], width);
        }
        return widths;
    }

    public static string BuildOutput(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original",
        string outputSeparator = ",")
    {
        if (rows.Count == 0) return "";

        var hasS2 = rows.Any(r => !string.IsNullOrEmpty(r.S2));
        var hasS3 = rows.Any(r => !string.IsNullOrEmpty(r.S3));
        var normalizedRows = rows
            .Select(row =>
            {
                var fragments = ExplodeRowFragments(row, sizeConfig);
                var rowGroups = GroupRenderOrder
                    .Where(g => fragments.Any(f => f.Group == g))
                    .ToList();
                return (Fragments: fragments, RowGroups: rowGroups);
            })
            .Where(row => row.Fragments.Count > 0)
            .ToList();
        var allFragments = normalizedRows.SelectMany(row => row.Fragments).ToList();
        if (allFragments.Count == 0) return "";

        var widths = GroupColumnWidths(allFragments);
        var sockWidth = allFragments.Max(f => f.Socks.Count);
        var outLines = new List<string>();

        foreach (var normalizedRow in normalizedRows)
        {
            foreach (var fragment in normalizedRow.Fragments)
            {
                var row = fragment.Row;
                var cols = new List<string>
                {
                    ListProcessor.ApplyCaseMode(row.Name, caseMode),
                    row.Number,
                };

                var apparelCols = new List<string>();

                foreach (var group in normalizedRow.RowGroups)
                {
                    if (group == fragment.Group)
                    {
                        var groupSizes = Enumerable.Repeat("", widths[group]).ToList();
                        var nextFreeColumn = 0;

                        for (var sizeIndex = 0; sizeIndex < row.Tams.Count; sizeIndex++)
                        {
                            var formatted = SizeHelper.FormatSizeToken(row.Tams[sizeIndex], sizeConfig);
                            var pieceIndex = sizeIndex < fragment.PieceFields.Count
                                ? PieceFieldIndex(fragment.PieceFields[sizeIndex])
                                : -1;
                            var targetIndex = pieceIndex >= 0 && pieceIndex < groupSizes.Count
                                ? pieceIndex
                                : nextFreeColumn;

                            while (targetIndex < groupSizes.Count && !string.IsNullOrEmpty(groupSizes[targetIndex]))
                                targetIndex++;
                            if (targetIndex >= groupSizes.Count)
                                groupSizes.Add("");

                            groupSizes[targetIndex] = formatted;
                            nextFreeColumn = targetIndex + 1;
                        }

                        groupSizes.AddRange(Enumerable.Repeat("", System.Math.Max(0, widths[group] - groupSizes.Count)));
                        apparelCols.AddRange(groupSizes);
                    }
                    else
                    {
                        apparelCols.AddRange(Enumerable.Repeat("", widths[group]));
                    }
                }

                cols.AddRange(apparelCols);

                if (sockWidth > 0)
                {
                    var sockSizes = fragment.Socks.Select(s => SizeHelper.FormatSizeToken(s, sizeConfig)).ToList();
                    sockSizes.AddRange(Enumerable.Repeat("", sockWidth - sockSizes.Count));
                    cols.AddRange(sockSizes);
                }

                if (hasS2) cols.Add(ListProcessor.ApplyCaseMode(row.S2, caseMode));
                if (hasS3) cols.Add(ListProcessor.ApplyCaseMode(row.S3, caseMode));

                if (!hasS2 && !hasS3)
                {
                    while (cols.Count > 2 && string.IsNullOrEmpty(cols[^1]))
                        cols.RemoveAt(cols.Count - 1);
                }

                outLines.Add(string.Join(outputSeparator, cols));
            }
        }

        return string.Join("\n", outLines);
    }

    public static string ExportOutputText(string text, string outputDir, string baseName)
    {
        Directory.CreateDirectory(outputDir);
        var path = FileNameHelper.VersionedPath(outputDir, baseName, ".txt");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }
}
