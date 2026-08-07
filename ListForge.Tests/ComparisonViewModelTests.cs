using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class ComparisonViewModelTests
{
    [Fact]
    public void MainViewModel_EnablesComparisonOnlyAfterSuccessfulProcessing()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G\nBIA,11,M" };

        Assert.False(vm.CanCompareInputOutput);
        Assert.Contains("Gere uma saída", vm.ComparisonActionTooltip);

        vm.QuickProcessCommand.Execute(null);

        Assert.True(vm.CanCompareInputOutput);
        Assert.True(vm.CompareInputOutputCommand.CanExecute(null));
    }

    [Fact]
    public async Task MainViewModel_CompareCommandRequestsComparisonWindow()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G\nBIA,11,M" };
        vm.QuickProcessCommand.Execute(null);
        var requested = new TaskCompletionSource<ComparisonViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.RequestComparison += comparison => requested.TrySetResult(comparison);

        vm.CompareInputOutputCommand.Execute(null);

        var comparisonViewModel = await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, comparisonViewModel.Summary.InputRecords);
        Assert.Equal(2, comparisonViewModel.Summary.OutputRecords);
        Assert.False(comparisonViewModel.Summary.HasCriticalDifferences);
    }

    [Fact]
    public void MainViewModel_InputChangeInvalidatesComparisonUntilNextProcessing()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G" };
        vm.QuickProcessCommand.Execute(null);
        var processedInput = vm.InputText;

        vm.InputText = "ANA,10,M";
        Assert.False(vm.CanCompareInputOutput);
        Assert.Contains("foi alterada", vm.ComparisonActionTooltip);

        vm.InputText = processedInput;
        Assert.False(vm.CanCompareInputOutput);

        vm.QuickProcessCommand.Execute(null);
        Assert.True(vm.CanCompareInputOutput);
    }

    [Fact]
    public void MainViewModel_PendingOutputEditDisablesComparisonAndDiscardRestoresIt()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G" };
        vm.QuickProcessCommand.Execute(null);
        vm.AllowOutputEditing = true;

        vm.OutputText = "ANA,10,M";
        Assert.False(vm.CanCompareInputOutput);

        vm.DiscardOutputEditsCommand.Execute(null);
        Assert.True(vm.CanCompareInputOutput);
    }

    [Fact]
    public void MainViewModel_AppliedOutputEditCreatesCurrentManualComparisonState()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G" };
        vm.QuickProcessCommand.Execute(null);
        vm.AllowOutputEditing = true;
        vm.OutputText = "ANA,10,M";

        vm.ApplyOutputEditsCommand.Execute(null);

        Assert.True(vm.CanCompareInputOutput);
        Assert.Equal("ANA,10,M", vm.OutputText);
    }

    [Fact]
    public void MainViewModel_AppliedJsonEditUpdatesCurrentComparisonOutput()
    {
        using var env = ComparisonTestEnvironment.Create();
        var vm = new MainViewModel { InputText = "ANA,10,G" };
        vm.QuickProcessCommand.Execute(null);
        vm.AllowOutputEditing = true;
        vm.JsonText = vm.JsonText.Replace("1-G", "1-M", StringComparison.Ordinal);

        vm.ApplyOutputEditsCommand.Execute(null);

        Assert.True(vm.CanCompareInputOutput);
        Assert.Equal("ANA,10,M", vm.OutputText);
    }

    [Fact]
    public void FiltersAndDifferenceNavigationUseCachedComparisonItems()
    {
        var service = new ListComparisonService();
        var request = new ProcessingWorkflowRequest(
            "ANA,10,G\nBIA,11,M",
            ",",
            SizeConfig.Default(),
            "original",
            ListSortMode.Original,
            JsonPieceMappingOptions.Disabled,
            ConsumeTrialCredit: false);
        var workflow = new ProcessingWorkflowService(new CompleteLicenseService());
        var originalResult = workflow.Execute(request);
        var snapshot = service.CreateSnapshot(request, originalResult, "Padrão", false);
        var editedResult = workflow.Execute(request with { InputText = "ANA,10,P\nCARLA,12,G" });
        var comparison = service.Compare(service.UpdateOutput(snapshot, editedResult, true));
        var vm = new ComparisonViewModel(comparison, 13);

        vm.SelectedFilter = vm.Filters.Single(filter => filter.Category == ComparisonCategory.Changed);
        Assert.Single(vm.FilteredItems);

        vm.NextDifferenceCommand.Execute(null);
        Assert.NotNull(vm.SelectedItem);
        Assert.True(vm.SelectedItem.RequiresReview);
        Assert.Null(vm.SelectedFilter?.Category);
    }

    [Fact]
    public void ClipboardReportIncludesPrivacyNoticeAndReviewDetails()
    {
        var service = new ListComparisonService();
        var request = new ProcessingWorkflowRequest(
            "ANA,10,G",
            ",",
            SizeConfig.Default(),
            "original",
            ListSortMode.Original,
            JsonPieceMappingOptions.Disabled,
            ConsumeTrialCredit: false);
        var workflow = new ProcessingWorkflowService(new CompleteLicenseService());
        var snapshot = service.CreateSnapshot(request, workflow.Execute(request), "Padrão", false);
        var edited = workflow.Execute(request with { InputText = "ANA,10,M" });
        var viewModel = new ComparisonViewModel(service.Compare(service.UpdateOutput(snapshot, edited, true)), 13);

        var report = viewModel.BuildClipboardReport();

        Assert.Contains(viewModel.PrivacyNotice, report);
        Assert.Contains("Manga Curta", report);
        Assert.Contains("Diferenças para revisão", report);
    }

    private sealed class ComparisonTestEnvironment : IDisposable
    {
        private readonly string _root;

        private ComparisonTestEnvironment(string root)
        {
            _root = root;
        }

        public static ComparisonTestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-ComparisonViewModelTests", Guid.NewGuid().ToString("N"));
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            TrialManager.SetTrialModeForTesting(false);
            return new ComparisonTestEnvironment(root);
        }

        public void Dispose()
        {
            TrialManager.SetTrialModeForTesting(null);
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

    private sealed class CompleteLicenseService : ILicenseService
    {
        public string Edition => "Completo";
        public bool IsTrial => false;
        public int ProcessingLimit => int.MaxValue;
        public int RemainingProcessings => int.MaxValue;
        public bool CanProcess => true;
        public string ProcessingStatusSuffix => "";
        public void ConsumeSuccessfulProcessing() { }
    }
}
