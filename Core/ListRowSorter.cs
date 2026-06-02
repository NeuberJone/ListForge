using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ListForge.Models;

namespace ListForge.Core;

public static class ListRowSorter
{
    public static List<ParsedRow> SortRows(IEnumerable<ParsedRow> rows, ListSortMode mode)
    {
        var indexedRows = rows
            .Select((row, index) => new IndexedRow(row, index))
            .ToList();

        if (mode == ListSortMode.Original)
            return indexedRows.Select(item => item.Row).ToList();

        return indexedRows
            .OrderBy(item => item, new IndexedRowComparer(mode))
            .Select(item => item.Row)
            .ToList();
    }

    private sealed record IndexedRow(ParsedRow Row, int Index);

    private sealed class IndexedRowComparer(ListSortMode mode) : IComparer<IndexedRow>
    {
        public int Compare(IndexedRow? x, IndexedRow? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var direction = mode == ListSortMode.Descending ? -1 : 1;
            var nameCompare = string.Compare(
                x.Row.Name,
                y.Row.Name,
                CultureInfo.CurrentCulture,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            if (nameCompare != 0) return nameCompare * direction;

            var numberCompare = CompareNumbers(x.Row.Number, y.Row.Number);
            if (numberCompare != 0) return numberCompare * direction;

            return x.Index.CompareTo(y.Index);
        }

        private static int CompareNumbers(string left, string right)
        {
            if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftNumber) &&
                int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightNumber))
                return leftNumber.CompareTo(rightNumber);

            return string.Compare(
                left ?? "",
                right ?? "",
                CultureInfo.CurrentCulture,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        }
    }
}
