using System.Collections.Generic;
using System.Linq;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json.Linq;

namespace ListForge.Tests;

public class JsonExportFormatTests
{
    private static SizeConfig Config => SizeConfig.Default();

    private static JObject BuildPreview(string input) =>
        JObject.Parse(ListProcessor.BuildJsonPreview(
            ListProcessor.BuildOrdersFromOrderlist(ListProcessor.ProcessText(input, ",", Config), Config)));

    [Fact]
    public void MaleAndFemalePieces_GetOwnGenderSuffix()
    {
        var root = BuildPreview("MARIO,1,M\nFABI,2,BLG");

        Assert.Equal("1-M-MA", (string?)root["orders"]![0]!["ShortSleeve"]);
        Assert.Equal("1-BLG-FE", (string?)root["orders"]![1]!["ShortSleeve"]);
    }

    [Fact]
    public void ChildPiece_UsesRegistryChildCode()
    {
        var root = BuildPreview("KID,3,8A");

        Assert.Equal("1-8A-C", (string?)root["orders"]![0]!["ShortSleeve"]);
    }

    [Fact]
    public void GenderField_AlwaysExportsAsMaleForSisboltCompatibility()
    {
        var root = BuildPreview("MARIO,1,M\nFABI,2,BLG\nKID,3,8A");

        Assert.All(root["orders"]!, order => Assert.Equal("MA", (string?)order["Gender"]));
    }

    [Fact]
    public void SamePersonWithDifferentGenderPieces_CreatesSeparateEntriesWithoutLossOrDuplication()
    {
        var root = BuildPreview("PESSOA,1,M,BLG");
        var orders = root["orders"]!.ToList();

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal("PESSOA", (string?)o["Name"]));
        Assert.All(orders, o => Assert.Equal("1", (string?)o["Number"]));
        Assert.Contains(orders, o => (string?)o["ShortSleeve"] == "1-M-MA" && (string?)o["LongSleeve"] == "");
        Assert.Contains(orders, o => (string?)o["LongSleeve"] == "1-BLG-FE" && (string?)o["ShortSleeve"] == "");
    }

    [Fact]
    public void MultiplePieceTypesOfSameGender_StayInSingleEntry()
    {
        var root = BuildPreview("PESSOA,1,M,G");
        var orders = root["orders"]!.ToList();

        Assert.Single(orders);
        Assert.Equal("1-M-MA", (string?)orders[0]["ShortSleeve"]);
        Assert.Equal("1-G-MA", (string?)orders[0]["LongSleeve"]);
    }

    [Fact]
    public void QuantitiesGreaterThanOne_AreKeptInPieceValueAndUnitCounts()
    {
        var root = BuildPreview("PESSOA,1,3-M");

        Assert.Equal("3-M-MA", (string?)root["orders"]![0]!["ShortSleeve"]);
        Assert.Equal(3, (int?)root["orders_genders_count"]!["male"]);
        Assert.Equal(0, (int?)root["orders_genders_count"]!["female"]);
        Assert.Equal(0, (int?)root["orders_genders_count"]!["childish"]);
    }

    [Fact]
    public void RepeatedLegitEntries_AreNotMergedOrDeduplicated()
    {
        var root = BuildPreview("ANA,10,M\nANA,10,M");
        var orders = root["orders"]!.ToList();

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal("1-M-MA", (string?)o["ShortSleeve"]));
    }

    [Fact]
    public void EmptyOptionalFields_RemainEmptyStringsInExport()
    {
        var root = BuildPreview("ANA,10,M");
        var order = root["orders"]![0]!;

        Assert.Equal("", (string?)order["Nickname"]);
        Assert.Equal("", (string?)order["BloodType"]);
        Assert.Equal("", (string?)order["LongSleeve"]);
        Assert.Equal("", (string?)order["Short"]);
        Assert.Equal("", (string?)order["Pants"]);
        Assert.Equal("", (string?)order["Tanktop"]);
        Assert.Equal("", (string?)order["Vest"]);
    }

    [Fact]
    public void AccentsPunctuationAndLeadingZeros_ArePreservedInFieldsAndUniqueChars()
    {
        var root = BuildPreview("JOÃO D'ÁVILA,007,M");
        var order = root["orders"]![0]!;

        Assert.Equal("JOÃO D'ÁVILA", (string?)order["Name"]);
        Assert.Equal("007", (string?)order["Number"]);

        var uniqueNameChars = (string?)root["unique_name_chars"] ?? "";
        Assert.Contains('Ã', uniqueNameChars);
        Assert.Contains('Á', uniqueNameChars);
        Assert.Contains('A', uniqueNameChars);
        Assert.Contains('\'', uniqueNameChars);
        Assert.DoesNotContain(' ', uniqueNameChars);

        var uniqueNumberChars = (string?)root["unique_number_chars"] ?? "";
        Assert.Equal("07", uniqueNumberChars);
    }

    [Fact]
    public void GenderSuffixAlreadyPresent_IsNotAppendedAgain()
    {
        var orders = new List<Dictionary<string, string>>
        {
            new()
            {
                ["Name"] = "ANA",
                ["Number"] = "1",
                ["Gender"] = "FE",
                ["ShortSleeve"] = "1-M-MA",
            },
        };

        var root = JObject.Parse(ListProcessor.BuildJsonPreview(orders));

        Assert.Equal("1-M-MA", (string?)root["orders"]![0]!["ShortSleeve"]);
    }

    [Fact]
    public void Metadata_DefaultsPreserveExistingBehaviorWhenNotProvided()
    {
        var orders = new List<Dictionary<string, string>> { new() { ["Name"] = "ANA", ["Number"] = "1" } };

        var root = JObject.Parse(ListProcessor.BuildJsonPreview(orders));

        Assert.Equal("", (string?)root["project"]);
        Assert.Equal("List", (string?)root["title"]);
        Assert.Equal("", (string?)root["client_name"]);
        Assert.Equal(0, (int?)root["order_number"]);
    }

    [Fact]
    public void Metadata_CanBeOverriddenByCaller()
    {
        var orders = new List<Dictionary<string, string>> { new() { ["Name"] = "ANA", ["Number"] = "1" } };

        var root = JObject.Parse(ListProcessor.BuildJsonPreview(
            orders, project: "Projeto X", title: "Lista Y", clientName: "Cliente Z", orderNumber: 1071));

        Assert.Equal("Projeto X", (string?)root["project"]);
        Assert.Equal("Lista Y", (string?)root["title"]);
        Assert.Equal("Cliente Z", (string?)root["client_name"]);
        Assert.Equal(1071, (int?)root["order_number"]);
    }

    [Fact]
    public void GendersCount_CountsPieceUnitsNotOrdersAndKeepsEntriesSeparateFromUnits()
    {
        var root = BuildPreview("A,1,2-M\nB,2,BLG\nC,3,8A");

        Assert.Equal(3, root["orders"]!.Count());
        Assert.Equal(2, (int?)root["orders_genders_count"]!["male"]);
        Assert.Equal(1, (int?)root["orders_genders_count"]!["female"]);
        Assert.Equal(1, (int?)root["orders_genders_count"]!["childish"]);
    }

    [Fact]
    public void Import_AcceptsOldUnsuffixedAndNewSuffixedPieceFormats()
    {
        var oldFormat = JObject.Parse("""{"orders":[{"Name":"ANA","Number":"10","ShortSleeve":"2-G"}]}""");
        var newFormat = JObject.Parse("""{"orders":[{"Name":"ANA","Number":"10","ShortSleeve":"2-G-MA"}]}""");

        var oldText = ListProcessor.ExtractListTextFromJsonData(oldFormat);
        var newText = ListProcessor.ExtractListTextFromJsonData(newFormat);

        Assert.Equal(oldText, newText);
        Assert.Equal("ANA,10,G\nANA,10,G", oldText);
    }

    [Fact]
    public void Import_StripsGenderSuffixAndRecomputesRealGenderDownstream()
    {
        var data = JObject.Parse(
            """{"orders":[{"Name":"ANA","Number":"10","ShortSleeve":"1-BLG-FE"},{"Name":"KID","Number":"11","ShortSleeve":"3-8A-C"}]}""");

        var text = ListProcessor.ExtractListTextFromJsonData(data, includeHeader: true);
        var rows = ListProcessor.ProcessText(text, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("1-BLG", orders[0]["ShortSleeve"]);
        Assert.Equal("FE", orders[0]["Gender"]);
        Assert.Equal(["8A", "8A", "8A"], rows.Skip(1).Select(r => r.Tams[0]));
        Assert.All(orders.Skip(1), o => Assert.Equal("C", o["Gender"]));
    }
}
