using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ListForge.Models;

namespace ListForge.Core;

internal sealed record RowFragment(ParsedRow Row, string Group, IReadOnlyList<string> Socks);

public static class ListOutputBuilder
{
    internal static readonly string[] GroupRenderOrder = ["male", "female", "child"];

    internal static List<RowFragment> ExplodeRowFragments(ParsedRow row, SizeConfig config)
    {
        var groupedColumns = GroupRenderOrder.ToDictionary(g => g, _ => new List<List<string>>());
        var exploded = new List<RowFragment>();
        var socks = new List<string>();

        foreach (var token in row.Tams)
        {
            var (qty, size) = SizeHelper.ParseQtyAndSize(token, config);
            var group = SizeHelper.SizeGroupOf(size, config);

            if (group == SizeHelper.GroupSock)
            {
                for (var i = 0; i < qty; i++)
                    socks.Add(size);
                continue;
            }

            groupedColumns[group].Add(Enumerable.Repeat(size, qty).ToList());
        }

        foreach (var group in GroupRenderOrder)
        {
            var columns = groupedColumns[group];
            if (columns.Count == 0) continue;

            var rowCount = columns.Max(c => c.Count);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var sizes = columns
                    .Select(c => rowIndex < c.Count ? c[rowIndex] : "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (sizes.Count > 0)
                    exploded.Add(new RowFragment(new ParsedRow(row.Name, row.Number, sizes, row.S2, row.S3), group, []));
            }
        }

        if (exploded.Count == 0 && socks.Count > 0)
            exploded.Add(new RowFragment(new ParsedRow(row.Name, row.Number, [], row.S2, row.S3), "", socks));
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
            widths[fragment.Group] = System.Math.Max(widths[fragment.Group], fragment.Row.Tams.Count);
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
        var activeGroups = GroupRenderOrder.Where(g => widths[g] > 0).ToList();
        var apparelWidth = activeGroups.Sum(g => widths[g]);
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
                        var groupSizes = row.Tams.Select(s => SizeHelper.FormatSizeToken(s, sizeConfig)).ToList();
                        groupSizes.AddRange(Enumerable.Repeat("", widths[group] - groupSizes.Count));
                        apparelCols.AddRange(groupSizes);
                    }
                    else
                    {
                        apparelCols.AddRange(Enumerable.Repeat("", widths[group]));
                    }
                }

                if (!hasS2 && !hasS3)
                    apparelCols.AddRange(Enumerable.Repeat("", System.Math.Max(0, apparelWidth - apparelCols.Count)));
                cols.AddRange(apparelCols);

                if (sockWidth > 0)
                {
                    var sockSizes = fragment.Socks.Select(s => SizeHelper.FormatSizeToken(s, sizeConfig)).ToList();
                    sockSizes.AddRange(Enumerable.Repeat("", sockWidth - sockSizes.Count));
                    cols.AddRange(sockSizes);
                }

                if (hasS2) cols.Add(ListProcessor.ApplyCaseMode(row.S2, caseMode));
                if (hasS3) cols.Add(ListProcessor.ApplyCaseMode(row.S3, caseMode));

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
