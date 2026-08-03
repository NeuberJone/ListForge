using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public sealed class ProcessingPreviewService
{
    private readonly ProcessingWorkflowService _processingWorkflowService;

    public ProcessingPreviewService(ProcessingWorkflowService processingWorkflowService)
    {
        _processingWorkflowService = processingWorkflowService;
    }

    public ProcessingPreview Analyze(ProcessingPreviewSnapshot snapshot)
    {
        AppLogger.Info("ProcessingPreview", "Iniciando análise da lista.");

        var analysisRequest = snapshot.Request with { ConsumeTrialCredit = false };
        var result = _processingWorkflowService.Execute(analysisRequest);
        var totalRecords = CountInputRecords(snapshot.Request.InputText, snapshot.Request.Separator);
        var issues = BuildIssues(snapshot.Request.InputText, snapshot.Request.Separator, result);
        var warningRecords = issues.Count(issue => issue.Severity == ProcessingIssueSeverity.Warning);
        var invalidRecords = result.Status == ProcessingWorkflowStatus.ValidationFailed
            ? result.ValidationIssues.Count
            : issues.Count(issue => issue.Severity == ProcessingIssueSeverity.Invalid);
        var validRecords = result.Status == ProcessingWorkflowStatus.Success
            ? Math.Max(0, result.Rows.Count - warningRecords)
            : 0;
        var warnings = BuildWarnings(snapshot, result, warningRecords, invalidRecords);

        var preview = new ProcessingPreview(
            snapshot,
            result,
            totalRecords,
            validRecords,
            warningRecords,
            invalidRecords,
            BuildSizeSummary(result.Rows, snapshot.Request.SizeConfig),
            BuildPieceTypeSummary(result.Rows, snapshot.Request.SizeConfig, snapshot.Request.JsonPieceMapping ?? JsonPieceMappingOptions.Disabled),
            issues,
            warnings);

        AppLogger.Info(
            "ProcessingPreview",
            $"Análise concluída: total={preview.TotalRecords}; validos={preview.ValidRecords}; avisos={preview.WarningRecords}; invalidos={preview.InvalidRecords}.");
        return preview;
    }

    public ProcessingWorkflowResult ExecuteConfirmed(ProcessingPreview preview)
    {
        AppLogger.Info("ProcessingPreview", "Processamento confirmado a partir da prévia.");
        return _processingWorkflowService.Execute(preview.Snapshot.Request with { ConsumeTrialCredit = true });
    }

    private static int CountInputRecords(string inputText, string separator)
    {
        var sep = ListParser.NormalizeSeparator(separator);
        return (inputText ?? "")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Count(line => !LooksLikeHeaderLine(line, sep));
    }

    private static bool LooksLikeHeaderLine(string line, string separator)
    {
        var parts = line.Split(separator).Select(part => part.Trim()).ToList();
        var knownPieces = parts.Count(part => PieceTypeMapper.IsKnownKey(part));
        var hasNameOrNumber = parts.Any(part =>
            string.Equals(part, "Name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "Nome", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "Number", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "Numero", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "Número", StringComparison.OrdinalIgnoreCase));

        return knownPieces > 0 && (hasNameOrNumber || knownPieces >= 2);
    }

    private static IReadOnlyList<ProcessingIssue> BuildIssues(
        string inputText,
        string separator,
        ProcessingWorkflowResult result)
    {
        inputText ??= "";
        var lines = inputText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (result.Status == ProcessingWorkflowStatus.ValidationFailed)
        {
            return result.ValidationIssues
                .Select(issue => new ProcessingIssue(
                    issue.LineNumber,
                    LineAt(lines, issue.LineNumber),
                    ProcessingIssueSeverity.Invalid,
                    issue.Message,
                    "Revise a linha antes de processar."))
                .ToList();
        }

        if (result.Status != ProcessingWorkflowStatus.Success)
            return [];

        var warnings = new List<ProcessingIssue>();
        var nonEmptyLineNumbers = NonEmptyDataLineNumbers(inputText ?? "", separator).ToList();
        for (var i = 0; i < result.Rows.Count && i < nonEmptyLineNumbers.Count; i++)
        {
            var row = result.Rows[i];
            if (!string.IsNullOrWhiteSpace(row.Name))
                continue;

            var lineNumber = nonEmptyLineNumbers[i];
            warnings.Add(new ProcessingIssue(
                lineNumber,
                LineAt(lines, lineNumber),
                ProcessingIssueSeverity.Warning,
                "nome não informado",
                "Confira se a linha sem nome está correta para este pedido."));
        }

        return warnings;
    }

    private static IEnumerable<int> NonEmptyDataLineNumbers(string inputText, string separator)
    {
        var sep = ListParser.NormalizeSeparator(separator);
        var lines = (inputText ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || LooksLikeHeaderLine(line, sep))
                continue;

            yield return i + 1;
        }
    }

    private static string LineAt(IReadOnlyList<string> lines, int lineNumber) =>
        lineNumber >= 1 && lineNumber <= lines.Count ? lines[lineNumber - 1].Trim() : "";

    private static IReadOnlyList<SizeImpactSummary> BuildSizeSummary(
        IReadOnlyList<ParsedRow> rows,
        SizeConfig sizeConfig)
    {
        var sizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var token in row.Tams)
            {
                var (qty, size) = SizeHelper.ParseQtyAndSize(token, sizeConfig);
                sizes[size] = sizes.TryGetValue(size, out var current) ? current + qty : qty;
            }
        }

        return sizes
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new SizeImpactSummary(entry.Key, entry.Value))
            .ToList();
    }

    private static IReadOnlyList<PieceTypeImpactSummary> BuildPieceTypeSummary(
        IReadOnlyList<ParsedRow> rows,
        SizeConfig sizeConfig,
        JsonPieceMappingOptions mappingOptions)
    {
        var pieces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var apparelIndex = 0;
            for (var i = 0; i < row.Tams.Count; i++)
            {
                var (qty, size) = SizeHelper.ParseQtyAndSize(row.Tams[i], sizeConfig);
                var group = SizeHelper.SizeGroupOf(size, sizeConfig);
                var pieceKey = group == SizeHelper.GroupSock
                    ? "Meião"
                    : ResolvePieceLabel(row, i, apparelIndex++, mappingOptions);
                pieces[pieceKey] = pieces.TryGetValue(pieceKey, out var current) ? current + qty : qty;
            }
        }

        return pieces
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new PieceTypeImpactSummary(entry.Key, entry.Value))
            .ToList();
    }

    private static string ResolvePieceLabel(
        ParsedRow row,
        int sizeIndex,
        int apparelIndex,
        JsonPieceMappingOptions mappingOptions)
    {
        var rowPiece = row.PieceFields != null && sizeIndex < row.PieceFields.Count
            ? PieceTypeMapper.NormalizeKey(row.PieceFields[sizeIndex])
            : "";
        var rowPieceIndex = PieceFieldIndex(rowPiece);
        if (mappingOptions.UseCustomOrder && rowPieceIndex >= 0 && rowPieceIndex < mappingOptions.NormalizedOrder.Count)
            return PieceTypeMapper.LabelFromKey(mappingOptions.NormalizedOrder[rowPieceIndex]);

        if (!mappingOptions.UseCustomOrder && PieceTypeMapper.IsKnownKey(rowPiece))
            return PieceTypeMapper.LabelFromKey(rowPiece);

        if (mappingOptions.UseCustomOrder && apparelIndex < mappingOptions.NormalizedOrder.Count)
            return PieceTypeMapper.LabelFromKey(mappingOptions.NormalizedOrder[apparelIndex]);

        var fallback = apparelIndex < PieceTypeMapper.JsonFields.Count
            ? PieceTypeMapper.JsonFields[apparelIndex]
            : PieceTypeMapper.ShortSleeve;
        return PieceTypeMapper.LabelFromKey(fallback);
    }

    private static int PieceFieldIndex(string? pieceField)
    {
        var normalized = PieceTypeMapper.NormalizeKey(pieceField);
        for (var i = 0; i < PieceTypeMapper.JsonFields.Count; i++)
        {
            if (PieceTypeMapper.JsonFields[i] == normalized)
                return i;
        }

        return -1;
    }

    private static IReadOnlyList<string> BuildWarnings(
        ProcessingPreviewSnapshot snapshot,
        ProcessingWorkflowResult result,
        int warningRecords,
        int invalidRecords)
    {
        var warnings = new List<string>();
        if (snapshot.HasUnsavedWorkProfileChanges)
            warnings.Add("O perfil de trabalho possui alterações não salvas. A prévia usa os valores atuais da tela.");
        if (snapshot.AdvancedListEnabled)
            warnings.Add("Lista avançada ativa: a ordem de tipos de peça será considerada no JSON.");
        if (warningRecords > 0)
            warnings.Add("Há registros processáveis que merecem revisão.");
        if (invalidRecords > 0 || result.Status == ProcessingWorkflowStatus.ValidationFailed)
            warnings.Add("Há registros inválidos. Corrija a entrada antes de confirmar o processamento.");
        if (result.Status == ProcessingWorkflowStatus.EmptyInput)
            warnings.Add("Nenhuma entrada foi informada.");
        if (result.Status == ProcessingWorkflowStatus.NoRows)
            warnings.Add("Nenhum registro válido foi encontrado para processamento.");

        return warnings;
    }
}
