using System.Collections.Generic;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public enum ProcessingWorkflowStatus
{
    EmptyInput,
    ValidationFailed,
    TrialLimitReached,
    NoRows,
    Success,
}

public sealed record ProcessingWorkflowRequest(
    string InputText,
    string Separator,
    SizeConfig SizeConfig,
    string CaseMode,
    ListSortMode SortMode);

public sealed record ProcessingWorkflowResult(
    ProcessingWorkflowStatus Status,
    IReadOnlyList<ListParser.ValidationIssue> ValidationIssues,
    List<ParsedRow> Rows,
    string OutputText,
    List<Dictionary<string, string>> Orders,
    string JsonPreview)
{
    public static ProcessingWorkflowResult Empty(ProcessingWorkflowStatus status) =>
        new(status, [], [], "", [], "");

    public static ProcessingWorkflowResult ValidationFailed(IReadOnlyList<ListParser.ValidationIssue> issues) =>
        new(ProcessingWorkflowStatus.ValidationFailed, issues, [], "", [], "");
}

public sealed class ProcessingWorkflowService
{
    private readonly ILicenseService _licenseService;

    public ProcessingWorkflowService()
        : this(new LocalTrialLicenseService())
    {
    }

    public ProcessingWorkflowService(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public ProcessingWorkflowResult Execute(ProcessingWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputText))
            return ProcessingWorkflowResult.Empty(ProcessingWorkflowStatus.EmptyInput);

        var validationIssues = ListProcessor.ValidateText(request.InputText, request.Separator, request.SizeConfig);
        if (validationIssues.Count > 0)
            return ProcessingWorkflowResult.ValidationFailed(validationIssues);

        if (!_licenseService.CanProcess)
            return ProcessingWorkflowResult.Empty(ProcessingWorkflowStatus.TrialLimitReached);

        var rows = ListProcessor.ProcessText(request.InputText, request.Separator, request.SizeConfig);
        if (rows.Count == 0)
            return ProcessingWorkflowResult.Empty(ProcessingWorkflowStatus.NoRows);

        rows = ListProcessor.SortRows(rows, request.SortMode);
        var output = ListProcessor.BuildOutput(rows, request.SizeConfig, request.CaseMode);
        var orders = ListProcessor.BuildOrdersFromOrderlist(rows, request.SizeConfig, request.CaseMode);
        var preview = ListProcessor.BuildJsonPreview(orders);

        _licenseService.ConsumeSuccessfulProcessing();

        return new ProcessingWorkflowResult(
            ProcessingWorkflowStatus.Success,
            [],
            rows,
            output,
            orders,
            preview);
    }
}
