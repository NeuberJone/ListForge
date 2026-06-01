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
    public void BuildOutput_KeepsSockInTextButJsonDoesNotExportSocksField()
    {
        var rows = ListProcessor.ProcessText("JOANA,10,M,JUVENIL", ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal("JOANA,10,M,JUVENIL", output);
        Assert.DoesNotContain(orders, order => order.ContainsKey("Socks"));
        Assert.Equal("M", orders.Single()["ShortSleeve"]);
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
    public void ProcessText_InvalidInputIncludesLineNumber()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ListProcessor.ProcessText("ANA,10,G\nSEM TAM,20", ",", Config));

        Assert.Contains("Linha 2", ex.Message);
    }
}
