using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class ProcessingPreviewServiceTests
{
    private static SizeConfig Config => SizeConfig.Default();

    [Fact]
    public void Analyze_ValidInputBuildsImpactWithoutConsumingCredit()
    {
        var license = new FakeLicenseService(isTrial: true, limit: 2);
        var service = CreateService(license);

        var preview = service.Analyze(CreateSnapshot("ANA,10,G\nBIA,11,M"));

        Assert.True(preview.CanProcess);
        Assert.Equal(2, preview.TotalRecords);
        Assert.Equal(2, preview.ValidRecords);
        Assert.Equal(0, preview.WarningRecords);
        Assert.Equal(0, preview.InvalidRecords);
        Assert.Equal(2, preview.Sizes.Sum(item => item.Count));
        Assert.Contains(preview.PieceTypes, item => item.PieceType == "Manga Curta" && item.Count == 2);
        Assert.Equal(0, license.ConsumedCredits);
    }

    [Fact]
    public void Analyze_InvalidInputReportsExpectedLine()
    {
        var service = CreateService(new FakeLicenseService(isTrial: false, limit: 0));

        var preview = service.Analyze(CreateSnapshot("ANA,10,G\nBIA,11,XYZ\nCARLA,12,P"));

        Assert.False(preview.CanProcess);
        Assert.Equal(3, preview.TotalRecords);
        Assert.Equal(0, preview.ValidRecords);
        Assert.Equal(1, preview.InvalidRecords);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(2, issue.LineNumber);
        Assert.Equal(ProcessingIssueSeverity.Invalid, issue.Severity);
        Assert.Contains("tamanho", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_BlankNameIsWarningButDoesNotBlockProcessing()
    {
        var service = CreateService(new FakeLicenseService(isTrial: false, limit: 0));

        var preview = service.Analyze(CreateSnapshot(",,P\nJOAO,7,M"));

        Assert.True(preview.CanProcess);
        Assert.Equal(2, preview.TotalRecords);
        Assert.Equal(1, preview.ValidRecords);
        Assert.Equal(1, preview.WarningRecords);
        Assert.Equal(0, preview.InvalidRecords);
        Assert.Contains(preview.Issues, issue => issue.LineNumber == 1 && issue.Severity == ProcessingIssueSeverity.Warning);
    }

    [Fact]
    public void Analyze_AdvancedListSummarizesMappedPieceTypes()
    {
        var mapping = new JsonPieceMappingOptions(true, [PieceTypeMapper.Tanktop, PieceTypeMapper.Short]);
        var service = CreateService(new FakeLicenseService(isTrial: false, limit: 0));

        var preview = service.Analyze(CreateSnapshot("ANA,10,P,M", advancedListEnabled: true, mapping: mapping));

        Assert.True(preview.CanProcess);
        Assert.Contains(preview.PieceTypes, item => item.PieceType == "Regata" && item.Count == 1);
        Assert.Contains(preview.PieceTypes, item => item.PieceType == "Short" && item.Count == 1);
        Assert.Contains(preview.Warnings, warning => warning.Contains("Lista avançada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_KeepsSockInImpactButJsonStillOmitsSock()
    {
        var service = CreateService(new FakeLicenseService(isTrial: false, limit: 0));

        var preview = service.Analyze(CreateSnapshot("ANA,10,JUVENIL"));

        Assert.True(preview.CanProcess);
        Assert.Contains(preview.PieceTypes, item => item.PieceType == "Meião" && item.Count == 1);
        Assert.DoesNotContain("JUVENIL", preview.AnalysisResult.JsonPreview);
    }

    [Fact]
    public void ExecuteConfirmed_UsesPreviewSnapshotAndConsumesCreditAfterSuccess()
    {
        var license = new FakeLicenseService(isTrial: true, limit: 1);
        var service = CreateService(license);
        var preview = service.Analyze(CreateSnapshot("CARLA,12,G\nANA,7,M", sortMode: ListSortMode.Ascending));

        Assert.Equal(0, license.ConsumedCredits);

        var result = service.ExecuteConfirmed(preview);

        Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
        Assert.Equal("ANA,7,M\nCARLA,12,G", result.OutputText);
        Assert.Equal(1, license.ConsumedCredits);
        Assert.False(license.CanProcess);
    }

    private static ProcessingPreviewService CreateService(ILicenseService licenseService) =>
        new(new ProcessingWorkflowService(licenseService));

    private static ProcessingPreviewSnapshot CreateSnapshot(
        string input,
        bool advancedListEnabled = false,
        JsonPieceMappingOptions? mapping = null,
        ListSortMode sortMode = ListSortMode.Original)
    {
        var request = new ProcessingWorkflowRequest(
            input,
            ",",
            Config,
            "original",
            sortMode,
            mapping ?? JsonPieceMappingOptions.Disabled,
            ConsumeTrialCredit: false);

        return new ProcessingPreviewSnapshot(
            request,
            "Padrão",
            HasUnsavedWorkProfileChanges: false,
            advancedListEnabled,
            "pasta da lista atual",
            "lista");
    }

    private sealed class FakeLicenseService : ILicenseService
    {
        private readonly bool _isTrial;
        private readonly int _limit;

        public FakeLicenseService(bool isTrial, int limit)
        {
            _isTrial = isTrial;
            _limit = limit;
        }

        public int ConsumedCredits { get; private set; }
        public string Edition => _isTrial ? "Trial" : "Completo";
        public bool IsTrial => _isTrial;
        public int ProcessingLimit => _isTrial ? _limit : int.MaxValue;
        public int RemainingProcessings => _isTrial ? Math.Max(0, _limit - ConsumedCredits) : int.MaxValue;
        public bool CanProcess => !_isTrial || RemainingProcessings > 0;
        public string ProcessingStatusSuffix => _isTrial ? $" | Trial: {RemainingProcessings}/{ProcessingLimit}" : "";

        public void ConsumeSuccessfulProcessing()
        {
            if (!CanProcess)
                throw new InvalidOperationException("Sem créditos Trial.");

            if (_isTrial)
                ConsumedCredits++;
        }
    }
}
