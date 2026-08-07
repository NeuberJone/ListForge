using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ListForge.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ListForge.Core;

public static class JsonOrderBuilder
{
    public static List<Dictionary<string, string>> BuildOrdersFromOrderlist(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode = "original",
        JsonPieceMappingOptions? pieceMappingOptions = null)
    {
        if (pieceMappingOptions?.UseCustomOrder == true)
            return BuildOrdersWithCustomPieceOrder(rows, sizeConfig, caseMode, pieceMappingOptions);

        var orders = new List<Dictionary<string, string>>();
        foreach (var row in rows)
            orders.AddRange(BuildCustomPieceOrdersForRow(row, sizeConfig, caseMode, PieceTypeMapper.JsonFields, true));

        return orders;
    }

    private static List<Dictionary<string, string>> BuildOrdersWithCustomPieceOrder(
        List<ParsedRow> rows,
        SizeConfig sizeConfig,
        string caseMode,
        JsonPieceMappingOptions pieceMappingOptions)
    {
        var orders = new List<Dictionary<string, string>>();
        var pieceOrder = pieceMappingOptions.NormalizedOrder;

        foreach (var row in rows)
            orders.AddRange(BuildCustomPieceOrdersForRow(row, sizeConfig, caseMode, pieceOrder, false));

        return orders;
    }

    private static IEnumerable<Dictionary<string, string>> BuildCustomPieceOrdersForRow(
        ParsedRow row,
        SizeConfig sizeConfig,
        string caseMode,
        IReadOnlyList<string> pieceOrder,
        bool useRowPieceFields)
    {
        var apparelIndex = 0;
        var groupOrder = new List<string>();
        var groupedPieces = new Dictionary<string, List<MappedPieceSize>>();

        for (var tamIndex = 0; tamIndex < row.Tams.Count; tamIndex++)
        {
            var tam = row.Tams[tamIndex];
            var (qty, size) = SizeHelper.ParseQtyAndSize(tam, sizeConfig);
            var group = SizeHelper.SizeGroupOf(size, sizeConfig);
            if (group == SizeHelper.GroupSock)
                continue;

            var sourcePieceField = useRowPieceFields && row.PieceFields != null && tamIndex < row.PieceFields.Count
                ? row.PieceFields[tamIndex]
                : "";
            var sourcePieceIndex = !useRowPieceFields && row.PieceFields != null && tamIndex < row.PieceFields.Count
                ? PieceFieldIndex(row.PieceFields[tamIndex])
                : -1;
            var pieceField = !string.IsNullOrWhiteSpace(sourcePieceField) && PieceTypeMapper.JsonFields.Contains(sourcePieceField)
                ? sourcePieceField
                : sourcePieceIndex >= 0 && sourcePieceIndex < pieceOrder.Count
                ? pieceOrder[sourcePieceIndex]
                : apparelIndex < pieceOrder.Count
                ? pieceOrder[apparelIndex]
                : PieceTypeMapper.ShortSleeve;
            apparelIndex++;

            if (!groupedPieces.TryGetValue(group, out var pieces))
            {
                pieces = [];
                groupedPieces[group] = pieces;
                groupOrder.Add(group);
            }

            pieces.Add(new MappedPieceSize(pieceField, size, qty));
        }

        if (groupedPieces.Count == 0 && row.Tams.Count > 0)
            return [CreateOrder(row, caseMode)];

        var orders = new List<Dictionary<string, string>>();
        foreach (var group in groupOrder)
        {
            var pieces = groupedPieces[group];
            var gender = SizeHelper.GenderFromSize(pieces[0].Size, sizeConfig);
            var order = CreateOrder(row, caseMode, gender);

            foreach (var piece in pieces)
            {
                var targetPieceField = ResolveAvailablePieceField(order, piece.PieceField, pieceOrder);
                if (PieceTypeMapper.JsonFields.Contains(targetPieceField))
                    order[targetPieceField] = SizeHelper.FormatSizeForJson(piece.Quantity, piece.Size);
            }

            orders.Add(order);
        }

        return orders;
    }

    private static string ResolveAvailablePieceField(
        IReadOnlyDictionary<string, string> order,
        string preferredField,
        IReadOnlyList<string> pieceOrder)
    {
        bool IsAvailable(string field) =>
            PieceTypeMapper.JsonFields.Contains(field)
            && (!order.TryGetValue(field, out var value) || string.IsNullOrEmpty(value));

        if (IsAvailable(preferredField))
            return preferredField;

        return pieceOrder
            .Concat(PieceTypeMapper.JsonFields)
            .Select(PieceTypeMapper.NormalizeKey)
            .Distinct()
            .FirstOrDefault(IsAvailable)
            ?? preferredField;
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

    private static Dictionary<string, string> CreateOrder(
        ParsedRow row,
        SizeConfig sizeConfig,
        string caseMode,
        string size = "",
        string pieceField = "")
    {
        var order = CreateOrder(
            row,
            caseMode,
            string.IsNullOrEmpty(size) ? "" : SizeHelper.GenderFromSize(size, sizeConfig));

        if (!string.IsNullOrEmpty(size) && PieceTypeMapper.JsonFields.Contains(pieceField))
            order[pieceField] = size;

        return order;
    }

    private static Dictionary<string, string> CreateOrder(
        ParsedRow row,
        string caseMode,
        string gender = "")
    {
        var order = new Dictionary<string, string>
        {
            ["Name"] = ListProcessor.ApplyCaseMode(row.Name, caseMode),
            ["Nickname"] = ListProcessor.ApplyCaseMode(row.S2, caseMode),
            ["Number"] = row.Number,
            ["BloodType"] = ListProcessor.ApplyCaseMode(row.S3, caseMode),
            ["Gender"] = gender,
            ["ShortSleeve"] = "",
            ["LongSleeve"] = "",
            ["Short"] = "",
            ["Pants"] = "",
            ["Tanktop"] = "",
            ["Vest"] = "",
        };

        return order;
    }

    private sealed record MappedPieceSize(string PieceField, string Size, int Quantity);

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

    public static string ExportJson(List<Dictionary<string, string>> orders, string outputDir, string baseName)
    {
        Directory.CreateDirectory(outputDir);
        var path = FileNameHelper.VersionedPath(outputDir, baseName, ".json");
        File.WriteAllText(path, WrapOrders(orders).ToString(Formatting.Indented), new UTF8Encoding(false));
        return path;
    }
}
