using System;
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
    // O SISBolt sempre devolve "Gender": "MA" na amostra observada, mesmo para peças "-FE" ou "-C".
    // Mantemos esse valor fixo apenas no objeto exportado, por compatibilidade com o formato de origem;
    // o gênero real de cada peça é expresso pelo próprio sufixo e usado nas contagens internas.
    private const string ExportGenderCompat = SizeHelper.GenderMale;

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

    // ---------------------------------------------------------------
    // JSON export (wrapped document)
    // ---------------------------------------------------------------

    /// <summary>
    /// Appends the piece's real gender/age code ("MA", "FE" or "C") to an already formatted
    /// "quantidade-tamanho" piece value, unless that value already carries one of these codes.
    /// </summary>
    private static string AppendGenderSuffix(string formattedPiece, string genderCode)
    {
        if (string.IsNullOrEmpty(formattedPiece) || string.IsNullOrEmpty(genderCode))
            return formattedPiece;

        var alreadySuffixed = SizeHelper.GenderCodes.Any(code =>
            formattedPiece.EndsWith("-" + code, StringComparison.OrdinalIgnoreCase));

        return alreadySuffixed ? formattedPiece : $"{formattedPiece}-{genderCode}";
    }

    private static int ExtractQuantity(string formattedPiece)
    {
        var dashIndex = formattedPiece.IndexOf('-');
        if (dashIndex <= 0)
            return 1;

        return int.TryParse(formattedPiece[..dashIndex], out var qty) ? qty : 1;
    }

    private static JObject BuildExportOrder(Dictionary<string, string> order)
    {
        var genderCode = order.GetValueOrDefault("Gender", "");

        var exported = new JObject
        {
            ["Name"] = order.GetValueOrDefault("Name", ""),
            ["Nickname"] = order.GetValueOrDefault("Nickname", ""),
            ["Number"] = order.GetValueOrDefault("Number", ""),
            ["BloodType"] = order.GetValueOrDefault("BloodType", ""),
            ["Gender"] = ExportGenderCompat,
        };

        foreach (var field in PieceTypeMapper.JsonFields)
        {
            var raw = order.GetValueOrDefault(field, "");
            exported[field] = string.IsNullOrEmpty(raw) ? "" : AppendGenderSuffix(raw, genderCode);
        }

        return exported;
    }

    /// <summary>
    /// Counts exported piece units (not people/orders) per real gender/age group.
    /// A single field with quantity 3 contributes three units to its group.
    /// </summary>
    private static JObject BuildGendersCount(List<Dictionary<string, string>> orders)
    {
        var male = 0;
        var female = 0;
        var childish = 0;

        foreach (var order in orders)
        {
            var genderCode = order.GetValueOrDefault("Gender", "");
            foreach (var field in PieceTypeMapper.JsonFields)
            {
                var raw = order.GetValueOrDefault(field, "");
                if (string.IsNullOrEmpty(raw))
                    continue;

                var units = ExtractQuantity(raw);
                if (string.Equals(genderCode, SizeHelper.GenderFemale, StringComparison.OrdinalIgnoreCase))
                    female += units;
                else if (string.Equals(genderCode, SizeHelper.GenderChild, StringComparison.OrdinalIgnoreCase))
                    childish += units;
                else
                    male += units;
            }
        }

        return new JObject
        {
            ["male"] = male,
            ["female"] = female,
            ["childish"] = childish,
        };
    }

    /// <summary>
    /// Builds the deduplicated, sorted character set of a text field across all orders.
    /// Accents and punctuation are preserved; only whitespace is dropped. Ordering is a
    /// deterministic ordinal (code point) sort, not locale-aware collation.
    /// </summary>
    private static string UniqueCharsFrom(List<Dictionary<string, string>> orders, string field)
    {
        var seen = new HashSet<char>();
        foreach (var order in orders)
        {
            var value = order.GetValueOrDefault(field, "");
            if (string.IsNullOrEmpty(value))
                continue;

            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch))
                    continue;
                seen.Add(ch);
            }
        }

        var sorted = seen.ToList();
        sorted.Sort();
        return new string(sorted.ToArray());
    }

    private static JObject WrapOrders(
        List<Dictionary<string, string>> orders,
        string project = "",
        string title = "List",
        string clientName = "",
        int orderNumber = 0) =>
        new()
        {
            ["project"] = project ?? "",
            ["title"] = title ?? "List",
            ["client_name"] = clientName ?? "",
            ["order_number"] = orderNumber,
            ["orders"] = new JArray(orders.Select(BuildExportOrder)),
            ["orders_genders_count"] = BuildGendersCount(orders),
            ["unique_name_chars"] = UniqueCharsFrom(orders, "Name"),
            ["unique_nickname_chars"] = UniqueCharsFrom(orders, "Nickname"),
            ["unique_number_chars"] = UniqueCharsFrom(orders, "Number"),
        };

    public static string BuildJsonPreview(
        List<Dictionary<string, string>> orders,
        string project = "",
        string title = "List",
        string clientName = "",
        int orderNumber = 0) =>
        WrapOrders(orders, project, title, clientName, orderNumber).ToString(Formatting.Indented);

    public static string ExportJson(
        List<Dictionary<string, string>> orders,
        string outputDir,
        string baseName,
        string project = "",
        string title = "List",
        string clientName = "",
        int orderNumber = 0)
    {
        Directory.CreateDirectory(outputDir);
        var path = FileNameHelper.VersionedPath(outputDir, baseName, ".json");
        var json = WrapOrders(orders, project, title, clientName, orderNumber).ToString(Formatting.Indented);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }
}
