using System.Net;
using System.Net.Http;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ListForge.Services;

public enum ExtractedListDestination
{
    NewList,
    CurrentList,
}

public sealed record LinkListImportResult(string Text, int LineCount);

public sealed class LinkListImportService : IDisposable
{
    private readonly HttpClient _httpClient;

    public LinkListImportService()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
    {
    }

    public LinkListImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ConfigManager.AppName}/link-import");
    }

    public async Task<OperationResult<LinkListImportResult>> ExtractAsync(
        string url,
        string outputSeparator,
        SizeConfig sizeConfig,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return OperationResult<LinkListImportResult>.Fail(
                "O link precisa começar com http:// ou https://.",
                $"Link rejeitado por formato inválido: {url}",
                errorCode: "InvalidUrl");
        }

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return OperationResult<LinkListImportResult>.Fail(
                    "Não foi possível extrair a lista do link.",
                    $"Link retornou HTTP 404: {uri}",
                    errorCode: "NotFound");
            }

            if (!response.IsSuccessStatusCode)
            {
                return OperationResult<LinkListImportResult>.Fail(
                    "Não foi possível extrair a lista do link.",
                    $"Link retornou HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {uri}",
                    errorCode: "HttpError");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<JToken>(json);
            if (data == null)
            {
                return OperationResult<LinkListImportResult>.Fail(
                    "Não foi possível extrair a lista do link.",
                    "JSON vazio ou inválido.",
                    errorCode: "InvalidJson");
            }

            var separator = ListProcessor.NormalizeSeparator(outputSeparator);
            var extracted = ListProcessor.ExtractListTextFromJsonData(data, separator);
            if (string.IsNullOrWhiteSpace(extracted))
            {
                return OperationResult<LinkListImportResult>.Fail(
                    "Nenhum item válido foi encontrado no link.",
                    "Extração por link retornou texto vazio.",
                    errorCode: "EmptyResult");
            }

            var validationIssues = ListProcessor.ValidateText(extracted, separator, sizeConfig);
            if (validationIssues.Count > 0)
            {
                var issue = validationIssues[0];
                return OperationResult<LinkListImportResult>.Fail(
                    "Nenhum item válido foi encontrado no link.",
                    $"Lista extraída possui validação inválida. Linha {issue.LineNumber}: {issue.Message}",
                    errorCode: "ValidationFailed");
            }

            var rows = ListProcessor.ProcessText(extracted, separator, sizeConfig);
            if (rows.Count == 0)
            {
                return OperationResult<LinkListImportResult>.Fail(
                    "Nenhum item válido foi encontrado no link.",
                    "Lista extraída não gerou registros.",
                    errorCode: "NoRows");
            }

            return OperationResult<LinkListImportResult>.Ok(
                new LinkListImportResult(extracted, rows.Count),
                "Lista extraída com sucesso.",
                $"Lista extraída do link com {rows.Count} registro(s).");
        }
        catch (OperationCanceledException ex)
        {
            return OperationResult<LinkListImportResult>.Fail(
                "Extração cancelada.",
                "Extração por link cancelada.",
                ex,
                "Canceled");
        }
        catch (JsonException ex)
        {
            return OperationResult<LinkListImportResult>.Fail(
                "Não foi possível extrair a lista do link.",
                "Resposta do link não é um JSON válido.",
                ex,
                "InvalidJson");
        }
        catch (Exception ex)
        {
            return OperationResult<LinkListImportResult>.Fail(
                "Não foi possível extrair a lista do link.",
                "Falha ao extrair lista do link.",
                ex,
                "LinkImportFailed");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
