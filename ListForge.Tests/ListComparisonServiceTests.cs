using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class ListComparisonServiceTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void IdenticalInputAndOutput_AreMatchingWithoutCriticalDifferences()
    {
        var comparison = CompareProcessed("ANA,10,G\nBIA,11,M");

        Assert.Equal(2, comparison.Summary.InputRecords);
        Assert.Equal(2, comparison.Summary.OutputRecords);
        Assert.Equal(2, comparison.Summary.Matching);
        Assert.Equal(0, comparison.Summary.Problems);
        Assert.Contains("Nenhum registro", comparison.Summary.StatusMessage);
    }

    [Fact]
    public void SortedOutput_IsReorderedAndDoesNotCreateMissingOrAddedRecords()
    {
        var comparison = CompareProcessed(
            "CARLA,12,G\nANA,7,M\nBRUNO,3,P",
            sortMode: ListSortMode.Ascending);

        Assert.Equal(3, comparison.Summary.Reordered);
        Assert.Equal(0, comparison.Summary.PossiblyMissing);
        Assert.Equal(0, comparison.Summary.Added);
        Assert.Equal([1, 2, 3], comparison.Items.Select(item => item.InputLineNumber));
        Assert.Equal([3, 1, 2], comparison.Items.Select(item => item.OutputLineNumber));
    }

    [Fact]
    public void ConfiguredCapitalization_IsReportedAsExpectedTransformation()
    {
        var comparison = CompareProcessed("joão da silva,10,g", caseMode: "upper");

        var item = Assert.Single(comparison.Items);
        Assert.Equal(ComparisonCategory.Transformed, item.Category);
        var difference = Assert.Single(item.FieldDifferences);
        Assert.Equal("Nome", difference.FieldName);
        Assert.Equal("joão da silva", difference.InputValue);
        Assert.Equal("JOÃO DA SILVA", difference.OutputValue);
        Assert.Equal("capitalização configurada", difference.Reason);
        Assert.True(difference.IsExpected);
    }

    [Fact]
    public void QuantityExpansion_PreservesAllOccurrences()
    {
        var comparison = CompareProcessed("ANA,10,3-G");

        Assert.Equal(3, comparison.Summary.InputRecords);
        Assert.Equal(3, comparison.Summary.OutputRecords);
        Assert.Equal(3, comparison.Summary.Matching);
        Assert.Equal(0, comparison.Summary.Problems);
    }

    [Fact]
    public void ManualRemovalAndAddition_AreReportedWithoutReusingMatches()
    {
        var snapshot = CreateProcessedSnapshot("ANA,10,G\nBIA,11,M");
        var edited = Process("ANA,10,G\nCARLA,12,P");
        var service = new ListComparisonService();

        var comparison = service.Compare(service.UpdateOutput(snapshot, edited, outputWasManuallyEdited: true));

        Assert.Equal(1, comparison.Summary.Matching);
        Assert.Equal(1, comparison.Summary.PossiblyMissing);
        Assert.Equal(1, comparison.Summary.Added);
        Assert.Contains(comparison.Items, item => item.Category == ComparisonCategory.PossiblyMissing && item.InputDisplay.Contains("BIA"));
        Assert.Contains(comparison.Items, item => item.Category == ComparisonCategory.Added && item.OutputDisplay.Contains("CARLA"));
    }

    [Fact]
    public void ManualFieldChanges_AreDetailedAndNotReportedAsAutomaticTransformations()
    {
        var snapshot = CreateProcessedSnapshot("ANA,10,G,JUVENIL,NINA,O+");
        var edited = Process("ANA MARIA,21,M,ADULTO,NINA,A+");
        var service = new ListComparisonService();

        var comparison = service.Compare(service.UpdateOutput(snapshot, edited, outputWasManuallyEdited: true));

        var item = Assert.Single(comparison.Items);
        Assert.Equal(ComparisonCategory.Changed, item.Category);
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Nome");
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Número");
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Manga Curta");
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Meião");
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Tipo sanguíneo");
        Assert.All(item.FieldDifferences, difference => Assert.False(difference.IsExpected));
    }

    [Fact]
    public void ManualPieceTypeChange_IsReportedByField()
    {
        var snapshot = CreateProcessedSnapshot("Name,Number,ShortSleeve\nANA,10,G");
        var edited = Process("Name,Number,LongSleeve\nANA,10,G");
        var service = new ListComparisonService();

        var item = Assert.Single(service.Compare(service.UpdateOutput(snapshot, edited, true)).Items);

        Assert.Equal(ComparisonCategory.Changed, item.Category);
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Manga Curta" && difference.OutputValue == "");
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Manga Longa" && difference.InputValue == "");
    }

    [Fact]
    public void LegitimateDuplicates_AreCountedAndMatchedByOccurrence()
    {
        var comparison = CompareProcessed("JOÃO,10,M\nJOÃO,10,M");

        Assert.Equal(2, comparison.Summary.Matching);
        Assert.Equal(1, comparison.Summary.InputDuplicates);
        Assert.Equal(1, comparison.Summary.OutputDuplicates);
        Assert.Equal(0, comparison.Summary.Problems);
    }

    [Fact]
    public void MissingDuplicate_IsReportedAsSinglePossibleAbsence()
    {
        var snapshot = CreateProcessedSnapshot("JOÃO,10,M\nJOÃO,10,M");
        var edited = Process("JOÃO,10,M");
        var service = new ListComparisonService();

        var comparison = service.Compare(service.UpdateOutput(snapshot, edited, true));

        Assert.Equal(1, comparison.Summary.Matching);
        Assert.Equal(1, comparison.Summary.PossiblyMissing);
        Assert.Equal(0, comparison.Summary.Added);
    }

    [Fact]
    public void AdditionalDuplicate_IsReportedAsSingleAddedOccurrence()
    {
        var snapshot = CreateProcessedSnapshot("JOÃO,10,M");
        var edited = Process("JOÃO,10,M\nJOÃO,10,M");
        var service = new ListComparisonService();

        var comparison = service.Compare(service.UpdateOutput(snapshot, edited, true));

        Assert.Equal(1, comparison.Summary.Matching);
        Assert.Equal(0, comparison.Summary.PossiblyMissing);
        Assert.Equal(1, comparison.Summary.Added);
        Assert.Equal(1, comparison.Summary.OutputDuplicates);
    }

    [Fact]
    public void NormalizedMatching_DoesNotHideManualTextChanges()
    {
        var snapshot = CreateProcessedSnapshot("JOÃO  SILVA,10,G");
        var edited = Process("joao silva,10,G");
        var service = new ListComparisonService();

        var item = Assert.Single(service.Compare(service.UpdateOutput(snapshot, edited, true)).Items);

        Assert.Equal(ComparisonCategory.Changed, item.Category);
        Assert.Equal("JOÃO  SILVA,10,G", item.InputDisplay);
        Assert.Equal("joao silva,10,G", item.OutputDisplay);
        Assert.Contains(item.FieldDifferences, difference => difference.FieldName == "Nome" && !difference.IsExpected);
    }

    [Fact]
    public void AmbiguousCandidates_AreNotPairedArbitrarily()
    {
        var snapshot = CreateProcessedSnapshot("JOÃO,10,G\nJOÃO,10,M");
        var edited = Process("JOÃO,10,P\nJOÃO,10,GG");
        var service = new ListComparisonService();

        var comparison = service.Compare(service.UpdateOutput(snapshot, edited, true));

        Assert.Equal(2, comparison.Summary.Uncertain);
        Assert.Equal(0, comparison.Summary.Changed);
        Assert.Equal(0, comparison.Summary.Added);
        Assert.All(comparison.Items, item => Assert.Equal(ComparisonCategory.Uncertain, item.Category));
    }

    [Fact]
    public void AdvancedPieceMappingAndSock_AreIncludedInSemanticComparison()
    {
        var mapping = new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.Pants]);
        var comparison = CompareProcessed("ANA,10,G,M,JUVENIL", mapping: mapping, advancedListEnabled: true);

        Assert.Equal(0, comparison.Summary.Problems);
        Assert.Contains(comparison.Items, item => item.InputDisplay.Contains("JUVENIL"));
    }

    [Fact]
    public void SnapshotCapturesProcessingSettingsUsingIndependentCopies()
    {
        var mapping = new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.Pants]);
        var request = Request(
            "ANA,10,G,M",
            ListSortMode.Descending,
            "upper",
            mapping,
            consumeTrialCredit: false);
        var result = new ProcessingWorkflowService(new FakeCompleteLicenseService()).Execute(request);
        var snapshot = new ListComparisonService().CreateSnapshot(request, result, "Equipe", advancedListEnabled: true);

        Assert.Equal("upper", snapshot.CaseMode);
        Assert.Equal(ListSortMode.Descending, snapshot.SortMode);
        Assert.True(snapshot.AdvancedListEnabled);
        Assert.Equal("Equipe", snapshot.ActiveWorkProfileName);
        Assert.Equal(mapping.NormalizedOrder, snapshot.JsonPieceMapping.NormalizedOrder);
        Assert.NotSame(request.SizeConfig, snapshot.SizeConfig);
        Assert.NotSame(mapping.NormalizedOrder, snapshot.JsonPieceMapping.NormalizedOrder);
    }

    [Fact]
    public void ComparisonDoesNotConsumeAdditionalTrialCredit()
    {
        var license = new FakeLicenseService(limit: 2);
        var workflow = new ProcessingWorkflowService(license);
        var request = Request("ANA,10,G", consumeTrialCredit: true);
        var result = workflow.Execute(request);
        Assert.Equal(1, license.ConsumedCredits);

        var service = new ListComparisonService();
        var snapshot = service.CreateSnapshot(request, result, "Padrão", false);
        _ = service.Compare(snapshot);

        Assert.Equal(1, license.ConsumedCredits);
    }

    [Fact]
    public void ComparisonDoesNotPersistSnapshotOrListContent()
    {
        using var env = ComparisonPersistenceEnvironment.Create();

        var comparison = CompareProcessed("ANA,10,G");

        Assert.Equal(0, comparison.Summary.Problems);
        Assert.Empty(Directory.Exists(ConfigManager.AppDir)
            ? Directory.GetFiles(ConfigManager.AppDir, "*comparison*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public void LargeInput_UsesAllThousandRecordsWithoutFalseDifferences()
    {
        var input = string.Join('\n', Enumerable.Range(1, 1000).Select(index => $"PESSOA {index:D4},{index},G"));

        var comparison = CompareProcessed(input, sortMode: ListSortMode.Descending);

        Assert.Equal(1000, comparison.Summary.InputRecords);
        Assert.Equal(1000, comparison.Summary.OutputRecords);
        Assert.Equal(1000, comparison.Summary.Reordered);
        Assert.Equal(0, comparison.Summary.Problems);
    }

    private static ListComparisonResult CompareProcessed(
        string input,
        ListSortMode sortMode = ListSortMode.Original,
        string caseMode = "original",
        JsonPieceMappingOptions? mapping = null,
        bool advancedListEnabled = false)
    {
        var request = Request(input, sortMode, caseMode, mapping, consumeTrialCredit: false);
        var result = new ProcessingWorkflowService(new FakeCompleteLicenseService()).Execute(request);
        var service = new ListComparisonService();
        return service.Compare(service.CreateSnapshot(request, result, "Padrão", advancedListEnabled));
    }

    private static ComparisonSnapshot CreateProcessedSnapshot(string input)
    {
        var request = Request(input, consumeTrialCredit: false);
        var result = new ProcessingWorkflowService(new FakeCompleteLicenseService()).Execute(request);
        return new ListComparisonService().CreateSnapshot(request, result, "Padrão", false);
    }

    private static ProcessingWorkflowResult Process(string input)
    {
        var request = Request(input, consumeTrialCredit: false);
        return new ProcessingWorkflowService(new FakeCompleteLicenseService()).Execute(request);
    }

    private static ProcessingWorkflowRequest Request(
        string input,
        ListSortMode sortMode = ListSortMode.Original,
        string caseMode = "original",
        JsonPieceMappingOptions? mapping = null,
        bool consumeTrialCredit = false) => new(
            input,
            ",",
            Config,
            caseMode,
            sortMode,
            mapping ?? JsonPieceMappingOptions.Disabled,
            consumeTrialCredit);

    private sealed class FakeCompleteLicenseService : ILicenseService
    {
        public string Edition => "Completo";
        public bool IsTrial => false;
        public int ProcessingLimit => int.MaxValue;
        public int RemainingProcessings => int.MaxValue;
        public bool CanProcess => true;
        public string ProcessingStatusSuffix => "";
        public void ConsumeSuccessfulProcessing() { }
    }

    private sealed class FakeLicenseService(int limit) : ILicenseService
    {
        public int ConsumedCredits { get; private set; }
        public string Edition => "Trial";
        public bool IsTrial => true;
        public int ProcessingLimit => limit;
        public int RemainingProcessings => Math.Max(0, limit - ConsumedCredits);
        public bool CanProcess => RemainingProcessings > 0;
        public string ProcessingStatusSuffix => $" | Trial: {RemainingProcessings}/{ProcessingLimit}";

        public void ConsumeSuccessfulProcessing()
        {
            if (!CanProcess)
                throw new InvalidOperationException("Sem créditos Trial.");
            ConsumedCredits++;
        }
    }

    private sealed class ComparisonPersistenceEnvironment : IDisposable
    {
        private readonly string _root;

        private ComparisonPersistenceEnvironment(string root)
        {
            _root = root;
        }

        public static ComparisonPersistenceEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-ComparisonPersistenceTests", Guid.NewGuid().ToString("N"));
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            return new ComparisonPersistenceEnvironment(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Temporary test directories can be left for the OS to clean up.
            }
        }
    }
}
