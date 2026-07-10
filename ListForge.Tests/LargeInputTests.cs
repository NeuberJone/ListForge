using System.Linq;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Tests;

public class LargeInputTests
{
    private const int LargeLineCount = 1000;
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void ValidateText_AcceptsOneThousandValidLines()
    {
        var input = BuildSimpleInput(LargeLineCount);

        var issues = ListProcessor.ValidateText(input, ",", Config);

        Assert.Empty(issues);
    }

    [Fact]
    public void ProcessText_ProcessesOneThousandSimpleLines()
    {
        var input = BuildSimpleInput(LargeLineCount);

        var rows = ListProcessor.ProcessText(input, ",", Config);

        Assert.Equal(LargeLineCount, rows.Count);
        Assert.Equal("PESSOA0001", rows[0].Name);
        Assert.Equal("PESSOA1000", rows[^1].Name);
    }

    [Fact]
    public void BuildOutputAndJson_UseExpectedCountsForOneThousandLines()
    {
        var rows = ListProcessor.ProcessText(BuildSimpleInput(LargeLineCount), ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);
        var preview = ListProcessor.BuildJsonPreview(orders);

        Assert.Equal(LargeLineCount, CountOutputLines(output));
        Assert.Equal(LargeLineCount, orders.Count);
        Assert.Contains("\"orders\"", preview);
        Assert.Equal("PESSOA0001", orders[0]["Name"]);
        Assert.Equal("PESSOA1000", orders[^1]["Name"]);
    }

    [Fact]
    public void SortRows_AscendingOrdersOneThousandRows()
    {
        var rows = ListProcessor.ProcessText(BuildSimpleInput(LargeLineCount, descending: true), ",", Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Ascending);

        Assert.Equal(LargeLineCount, sorted.Count);
        Assert.Equal("PESSOA0001", sorted[0].Name);
        Assert.Equal("PESSOA1000", sorted[^1].Name);
    }

    [Fact]
    public void SortRows_DescendingOrdersOneThousandRows()
    {
        var rows = ListProcessor.ProcessText(BuildSimpleInput(LargeLineCount), ",", Config);

        var sorted = ListProcessor.SortRows(rows, ListSortMode.Descending);

        Assert.Equal(LargeLineCount, sorted.Count);
        Assert.Equal("PESSOA1000", sorted[0].Name);
        Assert.Equal("PESSOA0001", sorted[^1].Name);
    }

    [Fact]
    public void QuantityExpansion_ProducesExpectedOutputAndJsonCounts()
    {
        var rows = ListProcessor.ProcessText(BuildQuantityInput(LargeLineCount), ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);

        Assert.Equal(LargeLineCount * 2, CountOutputLines(output));
        Assert.Equal(LargeLineCount, orders.Count);
        Assert.All(orders, order => Assert.Equal("2-G", order["ShortSleeve"]));
    }

    [Fact]
    public void SockSize_RemainsInLargeTextOutputAndStaysOutOfJson()
    {
        var rows = ListProcessor.ProcessText(BuildSockInput(LargeLineCount), ",", Config);

        var output = ListProcessor.BuildOutput(rows, Config);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, Config);
        var preview = ListProcessor.BuildJsonPreview(orders);

        Assert.Equal(LargeLineCount, CountOutputLines(output));
        Assert.Contains("JUVENIL", output);
        Assert.DoesNotContain("JUVENIL", preview);
        Assert.DoesNotContain(orders, order => order.ContainsKey("Socks"));
        Assert.All(orders, order => Assert.Equal("1-M", order["ShortSleeve"]));
    }

    [Fact]
    public void ValidateText_ReturnsSpecificMiddleLineForLargeInputError()
    {
        const int errorLine = 501;
        var input = BuildSimpleInput(LargeLineCount, invalidLine: errorLine);

        var issues = ListProcessor.ValidateText(input, ",", Config);

        var issue = Assert.Single(issues);
        Assert.Equal(errorLine, issue.LineNumber);
        Assert.Equal("tamanho não reconhecido", issue.Message);
    }

    private static string BuildSimpleInput(int count, bool descending = false, int? invalidLine = null)
    {
        var range = Enumerable.Range(1, count);
        if (descending)
            range = range.Reverse();

        return string.Join("\n", range.Select(i =>
            invalidLine == i
                ? $"{Name(i)},{i},ZZ"
                : $"{Name(i)},{i},M"));
    }

    private static string BuildQuantityInput(int count) =>
        string.Join("\n", Enumerable.Range(1, count).Select(i => $"{Name(i)},{i},2-G"));

    private static string BuildSockInput(int count) =>
        string.Join("\n", Enumerable.Range(1, count).Select(i => $"{Name(i)},{i},M,JUVENIL"));

    private static string Name(int index) =>
        $"PESSOA{index:0000}";

    private static int CountOutputLines(string output) =>
        string.IsNullOrEmpty(output) ? 0 : output.Split('\n').Length;
}
