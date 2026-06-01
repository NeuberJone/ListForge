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
        string caseMode = "original")
    {
        var orders = new List<Dictionary<string, string>>();

        var normalized = rows
            .SelectMany(r => ListOutputBuilder.ExplodeRowFragments(r, sizeConfig))
            .ToList();

        foreach (var fragment in normalized)
        {
            var row = fragment.Row;
            if (row.Tams.Count == 0 && fragment.Socks.Count > 0)
            {
                orders.Add(new Dictionary<string, string>
                {
                    ["Name"] = ListProcessor.ApplyCaseMode(row.Name, caseMode),
                    ["Nickname"] = ListProcessor.ApplyCaseMode(row.S2, caseMode),
                    ["Number"] = row.Number,
                    ["BloodType"] = ListProcessor.ApplyCaseMode(row.S3, caseMode),
                    ["Gender"] = "",
                    ["ShortSleeve"] = "",
                    ["LongSleeve"] = "",
                    ["Short"] = "",
                    ["Pants"] = "",
                    ["Tanktop"] = "",
                    ["Vest"] = "",
                });
                continue;
            }

            foreach (var tam in row.Tams)
            {
                var (qty, size) = SizeHelper.ParseQtyAndSize(tam, sizeConfig);
                var gender = SizeHelper.GenderFromSize(size, sizeConfig);

                for (var i = 0; i < qty; i++)
                {
                    orders.Add(new Dictionary<string, string>
                    {
                        ["Name"] = ListProcessor.ApplyCaseMode(row.Name, caseMode),
                        ["Nickname"] = ListProcessor.ApplyCaseMode(row.S2, caseMode),
                        ["Number"] = row.Number,
                        ["BloodType"] = ListProcessor.ApplyCaseMode(row.S3, caseMode),
                        ["Gender"] = gender,
                        ["ShortSleeve"] = size,
                        ["LongSleeve"] = "",
                        ["Short"] = "",
                        ["Pants"] = "",
                        ["Tanktop"] = "",
                        ["Vest"] = "",
                    });
                }
            }
        }

        return orders;
    }

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
