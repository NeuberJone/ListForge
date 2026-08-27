using System;
using System.Linq;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json.Linq;

namespace ListForge.Tests;

public class ListProcessorTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void ProcessText_KeepsOriginalInputOrder()
    {
        var rows = ListProcessor.ProcessText(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ",",
            Config);

        Assert.Equal(["CARLA", "ANA", "BRUNO"], rows.Select(r => r.Name));
    }

    [Fact]
    public void SortRows_OriginalKeepsInputOrder()
    {
        var rows = ListProcessor.ProcessText(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Original);

        Assert.Equal(["CARLA", "ANA", "BRUNO"], sorted.Select(r => r.Name));
    }

    [Fact]
    public void SortRows_AscendingSortsByName()
    {
        var rows = ListProcessor.ProcessText(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Ascending);

        Assert.Equal(["ANA", "BRUNO", "CARLA"], sorted.Select(r => r.Name));
    }

    [Fact]
    public void SortRows_AscendingSortsEqualNamesByNumericNumber()
    {
        var rows = ListProcessor.ProcessText(
            "ANA,10,G\nANA,2,M\nANA,1,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Ascending);

        Assert.Equal(["1", "2", "10"], sorted.Select(r => r.Number));
    }

    [Fact]
    public void SortRows_DescendingSortsByName()
    {
        var rows = ListProcessor.ProcessText(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Descending);

        Assert.Equal(["CARLA", "BRUNO", "ANA"], sorted.Select(r => r.Name));
    }

    [Fact]
    public void SortRows_DescendingSortsEqualNamesByNumericNumber()
    {
        var rows = ListProcessor.ProcessText(
            "ANA,10,G\nANA,2,M\nANA,1,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Descending);

        Assert.Equal(["10", "2", "1"], sorted.Select(r => r.Number));
    }

    [Fact]
    public void SortRows_PreservesRelativeOrderForTotalTies()
    {
        var rows = ListProcessor.ProcessText(
            "ANA,10,G\nANA,10,M\nANA,10,P",
            ",",
            Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Ascending);

        Assert.Equal(["G", "M", "P"], sorted.Select(r => r.Tams.Single()));
    }

    [Fact]
    public void BuildOutputAndJsonRespectSortedRows()
    {
        var rows = ListProcessor.ProcessText(
            "CARLA,12,G\nANA,10,M\nANA,2,P",
            ",",
            Config);
        var sorted = ListProcessor.SortRows(rows, ListSortMode.Ascending);

        var output = ListProcessor.BuildOutput(sorted, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(sorted, Config);

        Assert.Equal("ANA,2,P\nANA,10,M\nCARLA,12,G", output);
        Assert.Equal(["ANA", "ANA", "CARLA"], orders.Select(order => order["Name"]));
        Assert.Equal(["2", "10", "12"], orders.Select(order => order["Number"]));
    }

    [Fact]
    public void AboutInfoBuilder_IncludesTrialCreditsWhenTrial()
    {
        var info = new AboutInfo(
            "ListForge",
            "2.1.19",
            "Trial",
            "Não definido",
            true,
            3,
            10,
            "Neuber Jone",
            "GitHub: https://github.com/NeuberJone",
            @"C:\Users\user\AppData\Roaming\ListForge",
            @"C:\Users\user\AppData\Roaming\ListForge\logs",
            "Windows 11");

        var text = AboutInfoBuilder.BuildSupportText(info);

        Assert.Contains("Créditos Trial: 3/10", text);
        Assert.Contains("Edição: Trial", text);
    }

    [Fact]
    public void AboutInfoBuilder_UsesCompleteEditionMessageWhenNotTrial()
    {
        var info = new AboutInfo(
            "ListForge",
            "2.1.19",
            "Completo",
            "Não definido",
            false,
            0,
            0,
            "Neuber Jone",
            "GitHub: https://github.com/NeuberJone",
            @"C:\ListForge",
            @"C:\ListForge\logs",
            "Windows");

        var text = AboutInfoBuilder.BuildSupportText(info);

        Assert.Contains("Versão completa: sem limite de créditos Trial.", text);
        Assert.DoesNotContain("Créditos Trial:", text);
    }

    [Fact]
    public void BuildOutput_ExpandsQuantitySizeTokens()
    {
        var rows = ListProcessor.ProcessText("PEDRO,8,2-G", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);

        Assert.Equal("PEDRO,8,G\nPEDRO,8,G", output);
    }

    [Fact]
    public void BuildOutput_KeepsNicknameAndBloodTypeAtEnd()
    {
        var rows = ListProcessor.ProcessText("ANA,10,G,NINA,O+", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);

        Assert.Equal("ANA,10,G,NINA,O+", output);
    }

    [Fact]
    public void BuildOutputAndJson_OrganizeSameGroupSizesWhenNumberIsLast()
    {
        var input = string.Join('\n',
            "Rigby,M,8",
            "Freitas,G,G,17",
            "Breno,GG,XGG,7",
            "Diddy,G,G,19",
            "Musuel,M,G,47");
        var rows = ListProcessor.ProcessText(input, ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(
            "Rigby,8,M\nFreitas,17,G,G\nBreno,7,GG,XGG\nDiddy,19,G,G\nMusuel,47,M,G",
            output);
        Assert.Equal(5, orders.Count);
        Assert.Equal("1-G", orders[1]["ShortSleeve"]);
        Assert.Equal("1-G", orders[1]["LongSleeve"]);
        Assert.Equal("1-GG", orders[2]["ShortSleeve"]);
        Assert.Equal("1-XGG", orders[2]["LongSleeve"]);
        Assert.Equal("1-M", orders[4]["ShortSleeve"]);
        Assert.Equal("1-G", orders[4]["LongSleeve"]);
    }

    [Fact]
    public void BuildOutput_KeepsSockInTextButJsonDoesNotExportSocksField()
    {
        var rows = ListProcessor.ProcessText("JOANA,10,M,JUVENIL", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("JOANA,10,M,JUVENIL", output);
        Assert.DoesNotContain(orders, order => order.ContainsKey("Socks"));
        Assert.Equal("1-M", orders.Single()["ShortSleeve"]);
    }

    [Fact]
    public void BuildOutput_SockBetweenApparelSizesDoesNotCreateExtraPieceColumn()
    {
        var rows = ListProcessor.ProcessText("PEDRO MARIANO,G,99,ADULTO,G", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var order = Assert.Single(ListProcessor.BuildOrdersFromOrderlist(rows, Config));

        Assert.Equal("PEDRO MARIANO,99,G,G,ADULTO", output);
        Assert.Equal("1-G", order["ShortSleeve"]);
        Assert.Equal("1-G", order["LongSleeve"]);
        Assert.Equal("", order["Short"]);
    }

    [Fact]
    public void BuildOutput_SingleGroupRowsDoNotReceiveEmptyColumnsFromOtherGroups()
    {
        var input = string.Join('\n',
            "JOAO MENIN,10,10 A,JUVENIL,10A",
            "MOREIRA,23,PP,ADULTO,PP");
        var rows = ListProcessor.ProcessText(input, ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);

        Assert.Equal(
            "JOAO MENIN,10,10A,10A,JUVENIL\nMOREIRA,23,PP,PP,ADULTO",
            output);
    }

    [Fact]
    public void BuildOrders_UsesQuantitySizeFormatForImplicitQuantityInJsonOnly()
    {
        var rows = ListProcessor.ProcessText("MANEL,,PP", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("MANEL,,PP", output);
        var order = Assert.Single(orders);
        Assert.Equal("1-PP", order["ShortSleeve"]);
        Assert.DoesNotContain("1-PP", output);
    }

    [Fact]
    public void BuildOrders_PreservesExplicitQuantityInJsonSizeFields()
    {
        var rows = ListProcessor.ProcessText("JAO,10,3-G", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("JAO,10,G\nJAO,10,G\nJAO,10,G", output);
        var order = Assert.Single(orders);
        Assert.Equal("3-G", order["ShortSleeve"]);
    }

    [Fact]
    public void BuildOrders_UsesQuantitySizeFormatForMultiplePieces()
    {
        var rows = ListProcessor.ProcessText("JOAO,5,1-G,2-M,3-P", ",", Config);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        var order = Assert.Single(orders);
        Assert.Equal("1-G", order["ShortSleeve"]);
        Assert.Equal("2-M", order["LongSleeve"]);
        Assert.Equal("3-P", order["Short"]);
    }

    [Fact]
    public void BuildOrders_UsesExpectedGenderForMaleFemaleAndChildSizes()
    {
        var rows = ListProcessor.ProcessText(
            "MARIO,1,M\nFABI,2,BLG\nKID,3,8A",
            ",",
            Config);

        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(["MA", "FE", "C"], orders.Select(o => o["Gender"]));
        Assert.Equal(["MARIO", "FABI", "KID"], orders.Select(o => o["Name"]));
    }

    [Fact]
    public void ExtractListTextFromJsonData_UsesExpectedFieldOrder()
    {
        var data = JArray.Parse("""
        [
          {
            "Name": "ANA",
            "Number": "10",
            "ShortSleeve": "G",
            "Nickname": "NINA",
            "BloodType": "O+"
          }
        ]
        """);

        var text = ListProcessor.ExtractListTextFromJsonData(data);

        Assert.Equal("ANA,10,G,NINA,O+", text);
    }

    [Fact]
    public void ExtractListTextFromJsonData_ExpandsQuantitySizeJsonValues()
    {
        var data = JArray.Parse("""
        [
          {
            "Name": "ANA",
            "Number": "10",
            "ShortSleeve": "2-G",
            "Nickname": "NINA",
            "BloodType": "O+"
          }
        ]
        """);

        var text = ListProcessor.ExtractListTextFromJsonData(data);

        Assert.Equal("ANA,10,G,NINA,O+\nANA,10,G,NINA,O+", text);
    }

    [Fact]
    public void ProcessText_RepeatedPieceHeadersPreserveSecondPiece()
    {
        var lines = Enumerable.Range(1, 20)
            .Select(i => $"PLAYER {i},{i},G")
            .Concat([
                "",
                "Name,Number,LongSleeve",
                "PLAYER 21,21,M",
                "PLAYER 22,22,P",
            ]);
        var input = string.Join("\n", lines);

        var rows = ListProcessor.ProcessText(input, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(22, rows.Count);
        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("", orders[0]["LongSleeve"]);
        Assert.Equal("", orders[20]["ShortSleeve"]);
        Assert.Equal("1-M", orders[20]["LongSleeve"]);
        Assert.Equal("", orders[21]["ShortSleeve"]);
        Assert.Equal("1-P", orders[21]["LongSleeve"]);
        Assert.Equal("PLAYER 21", orders[20]["Name"]);
    }

    [Fact]
    public void ProcessText_EmptyColumnsInferPieceTypeForJson()
    {
        var input = string.Join('\n',
            ",,G",
            ",,P",
            "Fé Gramacho,,M",
            "Mateus,,10A",
            ",,,P",
            ",,,M",
            ",,,G");

        var rows = ListProcessor.ProcessText(input, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("", orders[0]["LongSleeve"]);
        Assert.Equal("1-P", orders[1]["ShortSleeve"]);
        Assert.Equal("", orders[1]["LongSleeve"]);
        Assert.Equal("1-P", orders[4]["LongSleeve"]);
        Assert.Equal("", orders[4]["ShortSleeve"]);
        Assert.Equal("1-M", orders[5]["LongSleeve"]);
        Assert.Equal("1-G", orders[6]["LongSleeve"]);
    }

    [Fact]
    public void BuildOutput_EmptyColumnsPreservePieceColumns()
    {
        var input = string.Join('\n',
            ",,G",
            ",,,P",
            ",,,M");

        var rows = ListProcessor.ProcessText(input, ",", Config);
        var output = ListProcessor.BuildOutput(rows, Config);

        Assert.Equal(",,G\n,,,P\n,,,M", output);
    }

    [Fact]
    public void ProcessText_PieceHeaderTransitionWorksOnAnyLine()
    {
        var input = """
        Name,Number,Short
        A,1,G
        B,2,M
        Name,Number,Vest
        C,3,P
        """;

        var rows = ListProcessor.ProcessText(input, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(["1-G", "1-M", ""], orders.Select(o => o["Short"]));
        Assert.Equal(["", "", "1-P"], orders.Select(o => o["Vest"]));
    }

    [Fact]
    public void ProcessText_ThreePieceSectionsKeepOrderAndDoNotDuplicateRows()
    {
        var input = """
        Name,Number,ShortSleeve
        A,1,G
        Name,Number,LongSleeve
        B,2,M
        Name,Number,Tanktop
        C,3,P
        """;

        var rows = ListProcessor.ProcessText(input, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["A", "B", "C"], orders.Select(o => o["Name"]));
        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("1-M", orders[1]["LongSleeve"]);
        Assert.Equal("1-P", orders[2]["Tanktop"]);
    }

    [Fact]
    public void ExtractListTextFromJsonData_CanIncludeHeaderToPreservePieceFields()
    {
        var data = JObject.Parse("""
        {
          "orders": [
            { "Name": "ANA", "Number": "10", "ShortSleeve": "G" },
            { "Name": "BIA", "Number": "11", "LongSleeve": "M" }
          ]
        }
        """);

        var text = ListProcessor.ExtractListTextFromJsonData(data, includeHeader: true);
        var rows = ListProcessor.ProcessText(text, ",", Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.StartsWith("Name,Number,ShortSleeve,LongSleeve", text);
        Assert.Equal("1-G", orders[0]["ShortSleeve"]);
        Assert.Equal("", orders[0]["LongSleeve"]);
        Assert.Equal("", orders[1]["ShortSleeve"]);
        Assert.Equal("1-M", orders[1]["LongSleeve"]);
    }

    [Fact]
    public void ProcessText_InvalidInputIncludesLineNumber()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ListProcessor.ProcessText("ANA,10,G\nSEM TAM,20", ",", Config));

        Assert.Contains("Linha 2", ex.Message);
    }

    [Fact]
    public void ValidateText_ReturnsLineIssuesBeforeProcessing()
    {
        var issues = ListProcessor.ValidateText(
            "ANA,10,G\nSEM TAM,20\nBIA,12,ZZ\nCARLA,1,P,M,G,GG,PP,XG,XGG",
            ",",
            Config);

        Assert.Equal(3, issues.Count);
        Assert.Equal((2, "sem tamanho"), (issues[0].LineNumber, issues[0].Message));
        Assert.Equal((3, "tamanho não reconhecido"), (issues[1].LineNumber, issues[1].Message));
        Assert.Equal((4, "mais de 6 tamanhos"), (issues[2].LineNumber, issues[2].Message));
    }
}
