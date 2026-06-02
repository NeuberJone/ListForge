using ListForge.Core;

namespace ListForge.Tests;

public class TextSearchHelperTests
{
    [Fact]
    public void FindMatches_UsesCaseInsensitiveSearchByDefault()
    {
        var matches = TextSearchHelper.FindMatches("Ana ana ANA", "ana", matchCase: false);

        Assert.Equal([(0, 3), (4, 3), (8, 3)], matches);
    }

    [Fact]
    public void FindMatches_RespectsCaseSensitiveSearch()
    {
        var matches = TextSearchHelper.FindMatches("Ana ana ANA", "ana", matchCase: true);

        Assert.Equal([(4, 3)], matches);
    }

    [Fact]
    public void ReplaceAt_ReplacesOnlyRequestedRange()
    {
        var text = TextSearchHelper.ReplaceAt("ABC DEF", 4, 3, "XYZ");

        Assert.Equal("ABC XYZ", text);
    }

    [Fact]
    public void ReplaceAll_UsesRequestedCaseMode()
    {
        var text = TextSearchHelper.ReplaceAll("Ana ana ANA", "ana", "Bia", matchCase: false);

        Assert.Equal("Bia Bia Bia", text);
    }
}
