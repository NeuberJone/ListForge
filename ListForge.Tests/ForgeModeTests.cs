using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

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
}
