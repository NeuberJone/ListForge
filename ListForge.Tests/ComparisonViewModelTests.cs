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
