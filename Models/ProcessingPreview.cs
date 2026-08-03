using ListForge.Core;
using ListForge.Services;

namespace ListForge.Models;

public enum ProcessingIssueSeverity
{
    Warning,
    Invalid,
}

public sealed record ProcessingIssue(
    int LineNumber,
    string Content,
    ProcessingIssueSeverity Severity,
    string Message,
    string SuggestedAction);

public sealed record SizeImpactSummary(string Size, int Count);

public sealed record PieceTypeImpactSummary(string PieceType, int Count);

public sealed record ProcessingPreviewSnapshot(
    ProcessingWorkflowRequest Request,
    string ActiveWorkProfileName,
    bool HasUnsavedWorkProfileChanges,
    bool AdvancedListEnabled,
    string OutputDirectoryDescription,
    string OutputFileDescription);

public sealed record ProcessingPreview(
    ProcessingPreviewSnapshot Snapshot,
    ProcessingWorkflowResult AnalysisResult,
    int TotalRecords,
    int ValidRecords,
    int WarningRecords,
    int InvalidRecords,
    IReadOnlyList<SizeImpactSummary> Sizes,
    IReadOnlyList<PieceTypeImpactSummary> PieceTypes,
    IReadOnlyList<ProcessingIssue> Issues,
    IReadOnlyList<string> Warnings)
{
    public bool CanProcess => AnalysisResult.Status == ProcessingWorkflowStatus.Success && AnalysisResult.Rows.Count > 0 && InvalidRecords == 0;
}
