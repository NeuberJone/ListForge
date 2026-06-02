using ListForge.Core;

namespace ListForge.Tests;

public class OperationResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = OperationResult.Ok("Pronto.", "Operação concluída.");

        Assert.True(result.Success);
        Assert.Equal("Pronto.", result.UserMessage);
        Assert.Equal("Operação concluída.", result.TechnicalMessage);
        Assert.Null(result.Exception);
        Assert.Equal("", result.ErrorCode);
    }

    [Fact]
    public void Fail_CapturesUserMessageTechnicalMessageExceptionAndCode()
    {
        var ex = new InvalidOperationException("erro interno");

        var result = OperationResult.Fail(
            "Mensagem amigável.",
            "Detalhe técnico.",
            ex,
            "ErrorCode");

        Assert.False(result.Success);
        Assert.Equal("Mensagem amigável.", result.UserMessage);
        Assert.Equal("Detalhe técnico.", result.TechnicalMessage);
        Assert.Same(ex, result.Exception);
        Assert.Equal("ErrorCode", result.ErrorCode);
    }

    [Fact]
    public void GenericOk_CarriesValue()
    {
        var result = OperationResult<int>.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFail_DoesNotExposeValue()
    {
        var result = OperationResult<string>.Fail("Falha.", errorCode: "Failure");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("Falha.", result.UserMessage);
        Assert.Equal("Failure", result.ErrorCode);
    }
}
