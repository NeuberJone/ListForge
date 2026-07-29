using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class AdvancedListToggleTests
{
    [Fact]
    public void AdvancedListIsDisabledWhenConfigIsMissing()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();

        var vm = new MainViewModel();

        Assert.False(vm.AdvancedListEnabled);
        Assert.False(vm.ShowAdvancedEditorOptions);
        Assert.False(vm.ShowAdvancedJsonOptions);
    }

    [Fact]
    public void AdvancedListLoadsFromSavedConfig()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();
        ConfigManager.SaveConfig(new AppConfig
        {
            UseAdvancedJsonPieceMapping = true,
        });

        var vm = new MainViewModel();

        Assert.True(vm.AdvancedListEnabled);
        Assert.True(vm.ShowAdvancedEditorOptions);
        Assert.True(vm.ShowAdvancedJsonOptions);
    }

    [Fact]
    public void AdvancedJsonOptionsRequireAdvancedListAndJsonSection()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();
        var vm = new MainViewModel();

        vm.ShowJsonSection = true;
        Assert.False(vm.ShowAdvancedJsonOptions);

        vm.AdvancedListEnabled = true;
        Assert.True(vm.ShowAdvancedJsonOptions);

        vm.ShowJsonSection = false;
        Assert.False(vm.ShowAdvancedJsonOptions);
    }

    [Fact]
    public void AdvancedListTogglePersistsConfig()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();
        var vm = new MainViewModel();

        vm.AdvancedListEnabled = true;

        var cfg = ConfigManager.LoadConfig();
        Assert.True(cfg.UseAdvancedJsonPieceMapping);
        Assert.True(cfg.ShowJsonTab);
        Assert.True(cfg.ShowGenerateJsonButton);
        Assert.True(cfg.ShowCopyJsonButton);
    }

    [Fact]
    public void TogglingAdvancedListDoesNotAlterListData()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();
        var vm = new MainViewModel
        {
            InputText = "ANA,10,P",
            OutputText = "ANA,10,P",
            JsonText = "{\"orders\":[]}",
        };

        vm.AdvancedListEnabled = true;
        vm.AdvancedListEnabled = false;

        Assert.Equal("ANA,10,P", vm.InputText);
        Assert.Equal("ANA,10,P", vm.OutputText);
        Assert.Equal("{\"orders\":[]}", vm.JsonText);
    }

    [Fact]
    public void TogglingAdvancedListDoesNotConsumeTrialCredit()
    {
        using var env = AdvancedListToggleTestEnvironment.Create(isTrial: true, limit: 2);
        var vm = new MainViewModel();

        Assert.Equal(2, TrialManager.RemainingProcessings);

        vm.AdvancedListEnabled = true;
        vm.AdvancedListEnabled = false;

        Assert.Equal(2, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void OutputEditingStartsDisabledAndIsSessionOnly()
    {
        using var env = AdvancedListToggleTestEnvironment.Create();
        File.WriteAllText(ConfigManager.ConfigPath, """
        {
          "AllowOutputEditing": true
        }
        """);

        var vm = new MainViewModel();

        Assert.False(vm.AllowOutputEditing);
        Assert.True(vm.IsGeneratedOutputReadOnly);

        vm.AllowOutputEditing = true;
        Assert.True(vm.AllowOutputEditing);

        Assert.Null(typeof(AppConfig).GetProperty("AllowOutputEditing"));
        vm.SaveSettingsCommand.Execute(null);
        var cfgText = File.ReadAllText(ConfigManager.ConfigPath);
        Assert.DoesNotContain("AllowOutputEditing", cfgText);

        var nextVm = new MainViewModel();
        Assert.False(nextVm.AllowOutputEditing);
    }

    private sealed class AdvancedListToggleTestEnvironment : IDisposable
    {
        private readonly string _root;

        private AdvancedListToggleTestEnvironment(string root)
        {
            _root = root;
        }

        public static AdvancedListToggleTestEnvironment Create(bool isTrial = false, int limit = 10)
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-AdvancedListToggleTests", Guid.NewGuid().ToString("N"));
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            TrialManager.SetTrialModeForTesting(isTrial, limit);
            return new AdvancedListToggleTestEnvironment(root);
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
}
