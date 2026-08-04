using System.Globalization;
using ListForge.Core;
using ListForge.Models;

namespace ListForge.Services;

public sealed class ListComparisonService
{
    private const int MinimumSimilarityScore = 3;

    public ComparisonSnapshot CreateSnapshot(
        ProcessingWorkflowRequest request,
        ProcessingWorkflowResult result,
        string activeWorkProfileName,
        bool advancedListEnabled,
        bool outputWasManuallyEdited = false)
    {
        if (result.Status != ProcessingWorkflowStatus.Success)
            throw new ArgumentException("A comparação exige um processamento concluído com sucesso.", nameof(result));

        var inputRows = ListParser.ProcessText(request.InputText, request.Separator, request.SizeConfig);
        return new ComparisonSnapshot(
            request.InputText,
            result.OutputText,
            CloneRows(inputRows),
            CloneRows(result.Rows),
            CloneSizeConfig(request.SizeConfig),
            request.CaseMode,
            request.SortMode,
            new JsonPieceMappingOptions(
                request.JsonPieceMapping?.UseCustomOrder == true,
                request.JsonPieceMapping?.NormalizedOrder.ToList() ?? []),
            advancedListEnabled,
            outputWasManuallyEdited,
            activeWorkProfileName,
            DateTimeOffset.UtcNow);
    }

    public ComparisonSnapshot UpdateOutput(
        ComparisonSnapshot snapshot,
        ProcessingWorkflowResult result,
        bool outputWasManuallyEdited)
    {
        if (result.Status != ProcessingWorkflowStatus.Success)
            throw new ArgumentException("A comparação exige uma saída válida.", nameof(result));

        return snapshot with
        {
            OutputText = result.OutputText,
            OutputRows = CloneRows(result.Rows),
            OutputWasManuallyEdited = outputWasManuallyEdited,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public ListComparisonResult Compare(ComparisonSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var inputRecords = BuildSemanticRecords(snapshot.InputRows, snapshot, isOutput: false);
        var outputRecords = BuildSemanticRecords(snapshot.OutputRows, snapshot, isOutput: true);
        var items = MatchRecords(inputRecords, outputRecords, snapshot);
        var summary = BuildSummary(items, inputRecords, outputRecords);

        AppLogger.Info(
            "ListComparison",
            $"Comparação concluída: entrada={summary.InputRecords}; saída={summary.OutputRecords}; problemas={summary.Problems}.");

        return new ListComparisonResult(snapshot, items, summary);
    }

    private static IReadOnlyList<ParsedRow> CloneRows(IEnumerable<ParsedRow> rows) =>
        rows.Select(row => row with
        {
            Tams = row.Tams.ToArray(),
            PieceFields = row.PieceFields?.ToArray(),
        }).ToList();

    private static SizeConfig CloneSizeConfig(SizeConfig config) => new()
    {
        Groups = config.Groups.ToDictionary(
            entry => entry.Key,
            entry => new SizeGroupConfig
            {
                Label = entry.Value.Label,
                BaseSizes = entry.Value.BaseSizes.ToList(),
                Prefixes = entry.Value.Prefixes.ToList(),
                Suffixes = entry.Value.Suffixes.ToList(),
            },
            StringComparer.OrdinalIgnoreCase),
    };

    private static List<SemanticRecord> BuildSemanticRecords(
        IReadOnlyList<ParsedRow> rows,
        ComparisonSnapshot snapshot,
        bool isOutput)
    {
        var records = new List<SemanticRecord>();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var fragments = ListOutputBuilder.ExplodeRowFragments(row, snapshot.SizeConfig);

            for (var fragmentIndex = 0; fragmentIndex < fragments.Count; fragmentIndex++)
            {
                var fragment = fragments[fragmentIndex];
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nome"] = isOutput
                        ? ListProcessor.ApplyCaseMode(fragment.Row.Name, snapshot.CaseMode)
                        : fragment.Row.Name,
                    ["Número"] = fragment.Row.Number,
                    ["Apelido"] = isOutput
                        ? ListProcessor.ApplyCaseMode(fragment.Row.S2, snapshot.CaseMode)
                        : fragment.Row.S2,
                    ["Tipo sanguíneo"] = isOutput
                        ? ListProcessor.ApplyCaseMode(fragment.Row.S3, snapshot.CaseMode)
                        : fragment.Row.S3,
                };

                if (!string.IsNullOrWhiteSpace(fragment.Group))
                    fields["Gênero/categoria"] = GroupLabel(fragment.Group, snapshot.SizeConfig);

                for (var sizeIndex = 0; sizeIndex < fragment.Row.Tams.Count; sizeIndex++)
                {
                    var (_, size) = SizeHelper.ParseQtyAndSize(fragment.Row.Tams[sizeIndex], snapshot.SizeConfig);
                    var pieceField = ResolvePieceField(fragment.PieceFields, sizeIndex, snapshot.JsonPieceMapping);
                    var label = PieceTypeMapper.LabelFromKey(pieceField);
                    AddFieldValue(fields, string.IsNullOrWhiteSpace(label) ? $"Peça {sizeIndex + 1}" : label, size);
                }

                if (fragment.Socks.Count > 0)
                    fields["Meião"] = string.Join(" | ", fragment.Socks);

                var position = records.Count + 1;
                var sourceLine = row.SourceLineNumber > 0 ? row.SourceLineNumber : rowIndex + 1;
                var originKey = string.IsNullOrWhiteSpace(row.SourceId)
                    ? ""
                    : $"{row.SourceId}:{fragmentIndex}";
                var display = isOutput
                    ? LineAt(snapshot.OutputText, position)
                    : LineAt(snapshot.InputText, sourceLine);

                records.Add(new SemanticRecord(
                    originKey,
                    sourceLine,
                    position,
                    display,
                    fields,
                    BuildNormalizedKey(fields)));
            }
        }

        return records;
    }

    private static string ResolvePieceField(
        IReadOnlyList<string> pieceFields,
        int sizeIndex,
        JsonPieceMappingOptions mapping)
    {
        var sourceField = sizeIndex < pieceFields.Count
            ? PieceTypeMapper.NormalizeKey(pieceFields[sizeIndex])
            : "";

        if (mapping.UseCustomOrder)
        {
            var sourceIndex = PieceFieldIndex(sourceField);
            if (sourceIndex >= 0 && sourceIndex < mapping.NormalizedOrder.Count)
                return mapping.NormalizedOrder[sourceIndex];
            if (sizeIndex < mapping.NormalizedOrder.Count)
                return mapping.NormalizedOrder[sizeIndex];
        }

        if (PieceTypeMapper.IsKnownKey(sourceField))
            return sourceField;

        return sizeIndex < PieceTypeMapper.JsonFields.Count
            ? PieceTypeMapper.JsonFields[sizeIndex]
            : $"Piece{sizeIndex + 1}";
    }

    private static int PieceFieldIndex(string? pieceField)
    {
        var normalized = PieceTypeMapper.NormalizeKey(pieceField);
        for (var index = 0; index < PieceTypeMapper.JsonFields.Count; index++)
        {
            if (string.Equals(PieceTypeMapper.JsonFields[index], normalized, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static string GroupLabel(string group, SizeConfig config) =>
        config.Groups.TryGetValue(group, out var groupConfig) && !string.IsNullOrWhiteSpace(groupConfig.Label)
            ? groupConfig.Label
            : group;

    private static void AddFieldValue(IDictionary<string, string> fields, string fieldName, string value)
    {
        if (!fields.TryGetValue(fieldName, out var current) || string.IsNullOrWhiteSpace(current))
        {
            fields[fieldName] = value;
            return;
        }

        fields[fieldName] = $"{current} | {value}";
    }

    private static List<ComparisonItem> MatchRecords(
        IReadOnlyList<SemanticRecord> input,
        IReadOnlyList<SemanticRecord> output,
        ComparisonSnapshot snapshot)
    {
        var items = new List<ComparisonItem>();
        var unmatchedInput = new HashSet<int>(Enumerable.Range(0, input.Count));
        var unmatchedOutput = new HashSet<int>(Enumerable.Range(0, output.Count));

        if (!snapshot.OutputWasManuallyEdited)
            MatchByOrigin(input, output, snapshot, unmatchedInput, unmatchedOutput, items);

        MatchByNormalizedOccurrence(input, output, snapshot, unmatchedInput, unmatchedOutput, items);
        MatchUniqueSimilarRecords(input, output, snapshot, unmatchedInput, unmatchedOutput, items);

        var uncertainOutputs = new HashSet<int>();
        foreach (var inputIndex in unmatchedInput.ToList())
        {
            var candidates = unmatchedOutput
                .Select(outputIndex => new { Index = outputIndex, Score = SimilarityScore(input[inputIndex], output[outputIndex]) })
                .Where(candidate => candidate.Score >= MinimumSimilarityScore)
                .OrderByDescending(candidate => candidate.Score)
                .ToList();

            if (candidates.Count == 0)
                continue;

            var bestScore = candidates[0].Score;
            var bestCandidates = candidates.Where(candidate => candidate.Score == bestScore).ToList();
            foreach (var candidate in bestCandidates)
                uncertainOutputs.Add(candidate.Index);

            var candidateLines = string.Join(", ", bestCandidates.Select(candidate => output[candidate.Index].Position));
            items.Add(new ComparisonItem(
                ComparisonCategory.Uncertain,
                input[inputIndex].SourceLineNumber,
                null,
                input[inputIndex].Display,
                "",
                [],
                $"Correspondência incerta com a(s) linha(s) {candidateLines} da saída.",
                "Existem múltiplas correspondências possíveis ou dados insuficientes para escolher com segurança."));
            unmatchedInput.Remove(inputIndex);
        }

        unmatchedOutput.ExceptWith(uncertainOutputs);

        foreach (var inputIndex in unmatchedInput.OrderBy(index => input[index].Position))
        {
            items.Add(new ComparisonItem(
                ComparisonCategory.PossiblyMissing,
                input[inputIndex].SourceLineNumber,
                null,
                input[inputIndex].Display,
                "",
                [],
                "Registro sem correspondência confiável na saída.",
                "Revise a saída antes de concluir que o registro foi removido."));
        }

        foreach (var outputIndex in unmatchedOutput.OrderBy(index => output[index].Position))
        {
            items.Add(new ComparisonItem(
                ComparisonCategory.Added,
                null,
                output[outputIndex].Position,
                "",
                output[outputIndex].Display,
                [],
                "Registro sem correspondência confiável na entrada.",
                "O registro aparece somente na saída organizada."));
        }

        return items
            .OrderBy(item => item.InputLineNumber ?? int.MaxValue)
            .ThenBy(item => item.OutputLineNumber ?? int.MaxValue)
            .ThenBy(item => item.Category)
            .ToList();
    }

    private static void MatchByOrigin(
        IReadOnlyList<SemanticRecord> input,
        IReadOnlyList<SemanticRecord> output,
        ComparisonSnapshot snapshot,
        ISet<int> unmatchedInput,
        ISet<int> unmatchedOutput,
        ICollection<ComparisonItem> items)
    {
        var outputByOrigin = output
            .Select((record, index) => new { record.OriginKey, Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.OriginKey))
            .GroupBy(item => item.OriginKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(group.Select(item => item.Index)),
                StringComparer.Ordinal);

        foreach (var inputIndex in unmatchedInput.ToList())
        {
            var origin = input[inputIndex].OriginKey;
            if (string.IsNullOrWhiteSpace(origin)
                || !outputByOrigin.TryGetValue(origin, out var queue)
                || queue.Count == 0)
                continue;

            var outputIndex = queue.Dequeue();
            if (!unmatchedOutput.Contains(outputIndex))
                continue;

            items.Add(CreatePairedItem(input[inputIndex], output[outputIndex], snapshot));
            unmatchedInput.Remove(inputIndex);
            unmatchedOutput.Remove(outputIndex);
        }
    }

    private static void MatchByNormalizedOccurrence(
        IReadOnlyList<SemanticRecord> input,
        IReadOnlyList<SemanticRecord> output,
        ComparisonSnapshot snapshot,
        ISet<int> unmatchedInput,
        ISet<int> unmatchedOutput,
        ICollection<ComparisonItem> items)
    {
        var outputByKey = unmatchedOutput
            .GroupBy(index => output[index].NormalizedKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(group.OrderBy(index => output[index].Position)),
                StringComparer.Ordinal);

        foreach (var inputIndex in unmatchedInput.OrderBy(index => input[index].Position).ToList())
        {
            if (!outputByKey.TryGetValue(input[inputIndex].NormalizedKey, out var queue) || queue.Count == 0)
                continue;

            var outputIndex = queue.Dequeue();
            items.Add(CreatePairedItem(input[inputIndex], output[outputIndex], snapshot));
            unmatchedInput.Remove(inputIndex);
            unmatchedOutput.Remove(outputIndex);
        }
    }

    private static void MatchUniqueSimilarRecords(
        IReadOnlyList<SemanticRecord> input,
        IReadOnlyList<SemanticRecord> output,
        ComparisonSnapshot snapshot,
        ISet<int> unmatchedInput,
        ISet<int> unmatchedOutput,
        ICollection<ComparisonItem> items)
    {
        var progress = true;
        while (progress)
        {
            progress = false;
            foreach (var inputIndex in unmatchedInput.ToList())
            {
                var inputCandidates = BestCandidates(input[inputIndex], output, unmatchedOutput);
                if (inputCandidates.Count != 1)
                    continue;

                var outputIndex = inputCandidates[0];
                var outputCandidates = BestCandidates(output[outputIndex], input, unmatchedInput);
                if (outputCandidates.Count != 1 || outputCandidates[0] != inputIndex)
                    continue;

                items.Add(CreatePairedItem(input[inputIndex], output[outputIndex], snapshot));
                unmatchedInput.Remove(inputIndex);
                unmatchedOutput.Remove(outputIndex);
                progress = true;
                break;
            }
        }
    }

    private static List<int> BestCandidates(
        SemanticRecord source,
        IReadOnlyList<SemanticRecord> candidates,
        IEnumerable<int> candidateIndexes)
    {
        var scored = candidateIndexes
            .Select(index => new { Index = index, Score = SimilarityScore(source, candidates[index]) })
            .Where(candidate => candidate.Score >= MinimumSimilarityScore)
            .ToList();
        if (scored.Count == 0)
            return [];

        var bestScore = scored.Max(candidate => candidate.Score);
        return scored
            .Where(candidate => candidate.Score == bestScore)
            .Select(candidate => candidate.Index)
            .ToList();
    }

    private static int SimilarityScore(SemanticRecord left, SemanticRecord right)
    {
        var score = 0;
        score += EqualField(left, right, "Nome") ? 6 : 0;
        score += EqualField(left, right, "Número") ? 5 : 0;
        score += EqualField(left, right, "Apelido") ? 2 : 0;
        score += EqualField(left, right, "Tipo sanguíneo") ? 2 : 0;
        score += EqualField(left, right, "Gênero/categoria") ? 1 : 0;

        var dataFields = left.Fields.Keys
            .Concat(right.Fields.Keys)
            .Where(field => field is not "Nome" and not "Número" and not "Apelido" and not "Tipo sanguíneo" and not "Gênero/categoria")
            .Distinct(StringComparer.OrdinalIgnoreCase);
        score += dataFields.Count(field => EqualField(left, right, field)) * 2;
        return score;
    }

    private static bool EqualField(SemanticRecord left, SemanticRecord right, string field)
    {
        var leftValue = left.Fields.GetValueOrDefault(field, "");
        var rightValue = right.Fields.GetValueOrDefault(field, "");
        return !string.IsNullOrWhiteSpace(leftValue)
            && string.Equals(NormalizeForMatching(leftValue), NormalizeForMatching(rightValue), StringComparison.Ordinal);
    }

    private static ComparisonItem CreatePairedItem(
        SemanticRecord input,
        SemanticRecord output,
        ComparisonSnapshot snapshot)
    {
        var differences = BuildFieldDifferences(input.Fields, output.Fields, snapshot).ToList();
        ComparisonCategory category;
        string summary;

        if (differences.Count == 0)
        {
            category = input.Position == output.Position
                ? ComparisonCategory.Matching
                : ComparisonCategory.Reordered;
            summary = category == ComparisonCategory.Matching
                ? "Registro preservado sem alteração."
                : $"Registro preservado e movido da posição {input.Position} para {output.Position}.";
        }
        else if (differences.All(difference => difference.IsExpected))
        {
            category = ComparisonCategory.Transformed;
            summary = string.Join("; ", differences.Select(difference => $"{difference.FieldName}: {difference.Reason}"));
        }
        else
        {
            category = ComparisonCategory.Changed;
            summary = $"{differences.Count} campo(s) diferente(s): {string.Join(", ", differences.Select(difference => difference.FieldName))}.";
        }

        return new ComparisonItem(
            category,
            input.SourceLineNumber,
            output.Position,
            input.Display,
            output.Display,
            differences,
            summary,
            snapshot.OutputWasManuallyEdited
                ? "A saída foi editada manualmente antes desta comparação."
                : "Comparação baseada no processamento atual.");
    }

    private static IEnumerable<ComparisonFieldDifference> BuildFieldDifferences(
        IReadOnlyDictionary<string, string> input,
        IReadOnlyDictionary<string, string> output,
        ComparisonSnapshot snapshot)
    {
        var fields = input.Keys
            .Concat(output.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FieldOrder)
            .ThenBy(field => field, StringComparer.CurrentCultureIgnoreCase);

        foreach (var field in fields)
        {
            var inputValue = input.GetValueOrDefault(field, "");
            var outputValue = output.GetValueOrDefault(field, "");
            if (string.Equals(inputValue, outputValue, StringComparison.Ordinal))
                continue;

            var reason = ExpectedTransformationReason(field, inputValue, outputValue, snapshot);
            yield return new ComparisonFieldDifference(
                field,
                inputValue,
                outputValue,
                reason,
                !string.IsNullOrWhiteSpace(reason));
        }
    }

    private static int FieldOrder(string field) => field switch
    {
        "Nome" => 0,
        "Número" => 1,
        "Apelido" => 2,
        "Tipo sanguíneo" => 3,
        "Gênero/categoria" => 4,
        "Meião" => 20,
        _ => 10,
    };

    private static string ExpectedTransformationReason(
        string field,
        string input,
        string output,
        ComparisonSnapshot snapshot)
    {
        if (snapshot.OutputWasManuallyEdited)
            return "";

        if (field is "Nome" or "Apelido" or "Tipo sanguíneo")
        {
            var expected = ListProcessor.ApplyCaseMode(input, snapshot.CaseMode);
            if (!string.Equals(input, expected, StringComparison.Ordinal)
                && string.Equals(expected, output, StringComparison.Ordinal))
                return "capitalização configurada";
        }

        if (!string.Equals(input, CollapseWhitespace(input), StringComparison.Ordinal)
            && string.Equals(CollapseWhitespace(input), output, StringComparison.Ordinal))
            return "remoção de espaços excedentes";

        if (field is not "Nome" and not "Apelido" and not "Tipo sanguíneo"
            && string.Equals(NormalizeForMatching(input), NormalizeForMatching(output), StringComparison.Ordinal))
            return "normalização reconhecida";

        return "";
    }

    private static ComparisonSummary BuildSummary(
        IReadOnlyList<ComparisonItem> items,
        IReadOnlyList<SemanticRecord> input,
        IReadOnlyList<SemanticRecord> output) => new(
            input.Count,
            output.Count,
            items.Count(item => item.Category == ComparisonCategory.Matching),
            items.Count(item => item.Category == ComparisonCategory.Reordered),
            items.Count(item => item.Category == ComparisonCategory.Transformed),
            items.Count(item => item.Category == ComparisonCategory.Changed),
            items.Count(item => item.Category == ComparisonCategory.PossiblyMissing),
            items.Count(item => item.Category == ComparisonCategory.Added),
            items.Count(item => item.Category == ComparisonCategory.Uncertain),
            DuplicateCount(input),
            DuplicateCount(output));

    private static int DuplicateCount(IEnumerable<SemanticRecord> records) =>
        records.GroupBy(record => record.NormalizedKey, StringComparer.Ordinal)
            .Sum(group => Math.Max(0, group.Count() - 1));

    private static string BuildNormalizedKey(IReadOnlyDictionary<string, string> fields) =>
        string.Join(
            "\u001F",
            fields.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"{NormalizeForMatching(entry.Key)}={NormalizeForMatching(entry.Value)}"));

    private static string NormalizeForMatching(string value)
    {
        var decomposed = CollapseWhitespace(value).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value?.Trim() ?? "", @"\s+", " ");

    private static string LineAt(string text, int oneBasedLine)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        return oneBasedLine >= 1 && oneBasedLine <= lines.Length
            ? lines[oneBasedLine - 1]
            : "";
    }

    private sealed record SemanticRecord(
        string OriginKey,
        int SourceLineNumber,
        int Position,
        string Display,
        IReadOnlyDictionary<string, string> Fields,
        string NormalizedKey);
}
