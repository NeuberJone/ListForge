using System;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Tests;

public class SizeHelperTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Theory]
    [InlineData("PP")]
    [InlineData("P")]
    [InlineData("G")]
    [InlineData("XGG")]
    public void IsValidSize_RecognizesDefaultMaleSizes(string size)
    {
        Assert.True(SizeHelper.IsValidSize(size, Config));
    }

    [Theory]
    [InlineData("BLP")]
    [InlineData("BLG")]
    public void IsValidSize_RecognizesFemalePrefixedSizes(string size)
    {
        Assert.True(SizeHelper.IsValidSize(size, Config));
    }

    [Theory]
    [InlineData("8A")]
    [InlineData("14A")]
    public void IsValidSize_RecognizesChildSuffixedSizes(string size)
    {
        Assert.True(SizeHelper.IsValidSize(size, Config));
    }

    [Theory]
    [InlineData("JUVENIL")]
    [InlineData("ADULTO")]
    [InlineData("INFANTIL")]
    public void IsValidSize_RecognizesSockSizes(string size)
    {
        Assert.True(SizeHelper.IsValidSize(size, Config));
    }

    [Theory]
    [InlineData("2-G", 2, "G")]
    [InlineData("3-M", 3, "M")]
    [InlineData("1-BLG", 1, "BLG")]
    public void ParseQtyAndSize_ParsesQuantityAndSize(string token, int expectedQty, string expectedSize)
    {
        var (qty, size) = SizeHelper.ParseQtyAndSize(token, Config);

        Assert.Equal(expectedQty, qty);
        Assert.Equal(expectedSize, size);
    }

    [Theory]
    [InlineData("0-G", "Quantidade")]
    [InlineData("2-INVALIDO", "Tamanho")]
    [InlineData("", "vazio")]
    public void ParseQtyAndSize_InvalidTokenThrowsUsefulError(string token, string expectedMessage)
    {
        var ex = Assert.Throws<ArgumentException>(() => SizeHelper.ParseQtyAndSize(token, Config));

        Assert.Contains(expectedMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("G", "MA")]
    [InlineData("BLG", "FE")]
    [InlineData("8A", "C")]
    public void GenderFromSize_ReturnsExpectedGenderCode(string size, string expectedGender)
    {
        Assert.Equal(expectedGender, SizeHelper.GenderFromSize(size, Config));
    }
}
