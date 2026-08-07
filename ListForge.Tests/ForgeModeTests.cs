using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class ForgeModeTests
{
    [Fact]
    public void AppConfig_DisablesForgeModeByDefault()
    {
        var config = new AppConfig();

        Assert.False(config.ForgeModeEnabled);
        Assert.True(config.ForgeAnvilEnabled);
        Assert.True(config.ForgeHeatEnabled);
        Assert.True(config.ForgeSparksEnabled);
        Assert.True(config.ForgeImpactEnabled);
    }

    [Fact]
    public void ProcessingWorkflow_ResultIsUnchangedWhenForgePreferencesExist()
    {
        const string input = "ANA,10,G\nBRUNO,7,M";
        var service = new ProcessingWorkflowService();
        var sizeConfig = SizeConfig.Default();

        var baseline = service.Execute(new ProcessingWorkflowRequest(
            input,
            ",",
            sizeConfig,
            "original",
            ListSortMode.Original));
        var withForgeConfig = new AppConfig
        {
            ForgeModeEnabled = true,
            ForgeAnvilEnabled = true,
            ForgeHeatEnabled = true,
            ForgeSparksEnabled = true,
            ForgeImpactEnabled = true,
        };
        _ = withForgeConfig;
        var repeated = service.Execute(new ProcessingWorkflowRequest(
            input,
            ",",
            sizeConfig,
            "original",
            ListSortMode.Original));

        Assert.Equal(ProcessingWorkflowStatus.Success, baseline.Status);
        Assert.Equal(baseline.OutputText, repeated.OutputText);
        Assert.Equal(baseline.JsonPreview, repeated.JsonPreview);
        Assert.Equal(baseline.Rows.Count, repeated.Rows.Count);
    }

    [Fact]
    public void MainViewModel_UpdatesProcessingLabelsWithForgeMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "ListForge-ForgeModeTests", Guid.NewGuid().ToString("N"));
        ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
        TrialManager.SetTrialModeForTesting(false);

        try
        {
            var vm = new MainViewModel();
            var notifications = new List<string?>();
            vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            Assert.Equal("Processar", vm.ProcessButtonText);
            Assert.Equal("Processar rápido", vm.QuickProcessButtonText);

            vm.ForgeModeEnabled = true;

            Assert.Equal("Forjar", vm.ProcessButtonText);
            Assert.Equal("Forja expressa", vm.QuickProcessButtonText);
            Assert.Contains(nameof(vm.ProcessButtonText), notifications);
            Assert.Contains(nameof(vm.QuickProcessButtonText), notifications);
        }
        finally
        {
            TrialManager.SetTrialModeForTesting(null);
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Temporary test directories can be left for the OS to clean up.
            }
        }
    }
}
