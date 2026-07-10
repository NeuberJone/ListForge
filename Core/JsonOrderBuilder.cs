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
            orders.AddRange(BuildCustomPieceOrdersForRow(row, sizeConfig, caseMode, PieceTypeMapper.JsonFields));

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
            orders.AddRange(BuildCustomPieceOrdersForRow(row, sizeConfig, caseMode, pieceOrder));

        return orders;
    }

    private static IEnumerable<Dictionary<string, string>> BuildCustomPieceOrdersForRow(
        ParsedRow row,
        SizeConfig sizeConfig,
        string caseMode,
        IReadOnlyList<string> pieceOrder)
    {
        var apparelIndex = 0;
        var groupOrder = new List<string>();
        var groupedPieces = new Dictionary<string, List<MappedPieceSize>>();

        foreach (var tam in row.Tams)
        {
            var (qty, size) = SizeHelper.ParseQtyAndSize(tam, sizeConfig);
            var group = SizeHelper.SizeGroupOf(size, sizeConfig);
            if (group == SizeHelper.GroupSock)
                continue;

            var pieceField = apparelIndex < pieceOrder.Count
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
            var rowCount = pieces.Max(piece => piece.Quantity);
            var gender = SizeHelper.GenderFromSize(pieces[0].Size, sizeConfig);

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var order = CreateOrder(row, caseMode, gender);
                foreach (var piece in pieces)
                {
                    if (rowIndex < piece.Quantity && PieceTypeMapper.JsonFields.Contains(piece.PieceField))
                        order[piece.PieceField] = piece.Size;
                }
                orders.Add(order);
            }
        }

        return orders;
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
