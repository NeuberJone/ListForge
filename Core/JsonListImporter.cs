using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ListForge.Core;

public static class JsonListImporter
{
    private static readonly string[] JsonImportFieldOrder =
        ["Name", "Number", "ShortSleeve", "LongSleeve", "Short", "Pants", "Tanktop", "Vest", "Socks", "Nickname", "BloodType"];

    private static readonly HashSet<string> JsonImportMandatory = ["Name", "Number"];

    private static string NormalizeJsonImportValue(object? value) =>
        value == null ? "" : value.ToString()!.Replace("\r", "").Replace("\n", " ").Trim();

    private static List<string> DecideEffectiveFields(List<Dictionary<string, object?>> orders)
    {
        var present = new HashSet<string>();
        foreach (var entry in orders)
            foreach (var key in JsonImportFieldOrder)
            {
                if (JsonImportMandatory.Contains(key)) continue;
                if (!string.IsNullOrEmpty(NormalizeJsonImportValue(entry.GetValueOrDefault(key))))
                    present.Add(key);
            }

        return JsonImportFieldOrder
            .Where(k => JsonImportMandatory.Contains(k) || present.Contains(k))
            .ToList();
    }

    public static string ExtractListTextFromJsonData(object data, string outputSeparator = ",")
    {
        List<Dictionary<string, object?>> orders;

        if (data is JArray arr)
        {
            orders = arr.Select(t => t.ToObject<Dictionary<string, object?>>()!).ToList();
        }
        else if (data is JObject obj)
        {
            var ordersToken = obj["orders"] as JArray
                ?? throw new ArgumentException("Campo 'orders' inválido (não é lista).");
            orders = ordersToken.Select(t => t.ToObject<Dictionary<string, object?>>()!).ToList();
        }
        else
        {
            throw new ArgumentException("A resposta precisa ser um objeto com 'orders' ou uma lista de pedidos.");
        }

        var fields = DecideEffectiveFields(orders);
        var lines = new List<string>();

        foreach (var entry in orders)
        {
            var rowValues = fields.Select(f => NormalizeJsonImportValue(entry.GetValueOrDefault(f))).ToList();
            var expandedRows = new List<string>();

            bool expanded = false;
            for (var i = 0; i < rowValues.Count; i++)
            {
                var m = Regex.Match(rowValues[i], @"^(\d+)-(.+)$");
                if (m.Success)
                {
                    var qty = int.Parse(m.Groups[1].Value);
                    var baseValue = m.Groups[2].Value.Trim();
                    for (var q = 0; q < qty; q++)
                    {
                        var newRow = new List<string>(rowValues) { [i] = baseValue };
                        expandedRows.Add(string.Join(outputSeparator, newRow));
                    }
                    expanded = true;
                    break;
                }
            }

            if (!expanded)
                expandedRows.Add(string.Join(outputSeparator, rowValues));

            lines.AddRange(expandedRows);
        }

        return string.Join("\n", lines);
    }
}
