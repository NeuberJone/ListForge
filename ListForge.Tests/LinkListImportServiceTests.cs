using System.Net;
using System.Text;
using ListForge.Models;
using ListForge.Services;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class LinkListImportServiceTests
{
    private static readonly SizeConfig Config = SizeConfig.Default();

    [Fact]
    public async Task ExtractAsync_ValidJson_PreservesSockField()
    {
        using var service = ServiceWithJson("""
        {
          "orders": [
            { "Name": "ANA", "Number": "10", "ShortSleeve": "G", "Socks": "ADULTO" }
          ]
        }
        """);

        var result = await service.ExtractAsync("https://example.com/list.json", ",", Config);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.LineCount);
        Assert.Contains("ADULTO", result.Value.Text);
    }

    [Fact]
    public async Task ExtractAsync_InvalidUrl_ReturnsFriendlyError()
    {
        using var service = ServiceWithJson("{}");

        var result = await service.ExtractAsync("ftp://example.com/list.json", ",", Config);

        Assert.False(result.Success);
        Assert.Equal("InvalidUrl", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_HttpError_DoesNotReturnText()
    {
        using var service = new LinkListImportService(new HttpClient(new StaticHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden))));

        var result = await service.ExtractAsync("https://example.com/list.json", ",", Config);

        Assert.False(result.Success);
        Assert.Equal("HttpError", result.ErrorCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ExtractAsync_EmptyOrders_ReturnsEmptyResult()
    {
        using var service = ServiceWithJson("""{ "orders": [] }""");

        var result = await service.ExtractAsync("https://example.com/list.json", ",", Config);

        Assert.False(result.Success);
        Assert.Equal("EmptyResult", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_InvalidRows_ReturnsValidationFailure()
    {
        using var service = ServiceWithJson("""
        {
          "orders": [
                { "Name": "ANA", "Number": "99", "Nickname": "SEM TAMANHO" }
          ]
        }
        """);

        var result = await service.ExtractAsync("https://example.com/list.json", ",", Config);

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ErrorCode);
    }

    [Fact]
    public void CombineInputText_AppendsAtEndAndPreservesDuplicates()
    {
        var combined = MainViewModel.CombineInputText(
            "ANA,10,G\nANA,10,G",
            "ANA,10,G\nBRUNO,7,M");

        Assert.Equal("ANA,10,G\nANA,10,G\nANA,10,G\nBRUNO,7,M", combined);
    }

    [Fact]
    public void ProcessingWorkflow_CanPreviewLinkImportWithoutConsumingTrialCredit()
    {
        var license = new CountingLicenseService();
        var service = new ProcessingWorkflowService(license);

        var result = service.Execute(new ProcessingWorkflowRequest(
            "ANA,99,G,ADULTO",
            ",",
            Config,
            "original",
            ListForge.Core.ListSortMode.Original,
            ConsumeTrialCredit: false));

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Equal(0, license.Consumed);
        Assert.Contains("ADULTO", result.OutputText);
    }

    private static LinkListImportService ServiceWithJson(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return new LinkListImportService(new HttpClient(new StaticHandler(response)));
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StaticHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }

    private sealed class CountingLicenseService : ILicenseService
    {
        public int Consumed { get; private set; }
        public string Edition => "Trial";
        public bool IsTrial => true;
        public int ProcessingLimit => 1;
        public int RemainingProcessings => 1 - Consumed;
        public bool CanProcess => RemainingProcessings > 0;
        public string ProcessingStatusSuffix => "";

        public void ConsumeSuccessfulProcessing()
        {
            Consumed++;
        }
    }
}
