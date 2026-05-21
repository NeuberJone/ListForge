using System.Collections.Generic;

namespace ListForge.Models;

public sealed class SizeGroupConfig
{
    public string Label { get; set; } = "";
    public List<string> BaseSizes { get; set; } = [];
    public List<string> Prefixes { get; set; } = [];
    public List<string> Suffixes { get; set; } = [];
}

public sealed class SizeConfig
{
    public Dictionary<string, SizeGroupConfig> Groups { get; set; } = [];

    public static SizeConfig Default() => new()
    {
        Groups = new Dictionary<string, SizeGroupConfig>
        {
            ["male"] = new()
            {
                Label = "Masculino",
                BaseSizes = ["PP", "P", "M", "G", "GG", "XG", "XGG", "XXGG", "XLGG"],
                Prefixes = [],
                Suffixes = [],
            },
            ["female"] = new()
            {
                Label = "Feminino",
                BaseSizes = ["PP", "P", "M", "G", "GG", "XG", "XGG", "XXGG"],
                Prefixes = ["BL"],
                Suffixes = [],
            },
            ["child"] = new()
            {
                Label = "Infantil",
                BaseSizes = ["2", "4", "6", "8", "10", "12", "14", "16"],
                Prefixes = [],
                Suffixes = ["A"],
            },
        }
    };
}
