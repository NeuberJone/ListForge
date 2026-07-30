using ListForge.Config;
using ListForge.Core;
using ListForge.ViewModels;

namespace ListForge.Tests;

public class CurrentFileSessionTests
{
    [Fact]
    public void MainViewModel_DoesNotRestoreLastOpenedFileFromLegacyConfig()
    {
        using var env = CurrentFileSessionTestEnvironment.Create();
        var previousFile = Path.Combine(env.Root, "lista-antiga.csv");
        File.WriteAllText(previousFile, "ANA,10,G");
        File.WriteAllText(ConfigManager.ConfigPath, $$"""
        {
          "ThemeName": "SISBolt",
          "EditorFontSize": 18,
          "LastOpenedFile": "{{previousFile.Replace("\\", "\\\\")}}"
        }
        """);

        var beforeTrialCredits = TrialManager.RemainingProcessings;

        var vm = new MainViewModel();

        Assert.Equal("Arquivo atual: (nova lista)", vm.CurrentFileLabel);
        Assert.Equal("SISBolt", vm.ThemeName);
        Assert.Equal(18, vm.EditorFontSize);
        Assert.True(File.Exists(previousFile));
        Assert.Equal(beforeTrialCredits, TrialManager.RemainingProcessings);

        vm.SaveSettingsCommand.Execute(null);
        var savedConfig = File.ReadAllText(ConfigManager.ConfigPath);
        Assert.DoesNotContain("LastOpenedFile", savedConfig);
        Assert.DoesNotContain("lista-antiga.csv", savedConfig);
    }

    private sealed class CurrentFileSessionTestEnvironment : IDisposable
    {
        private CurrentFileSessionTestEnvironment(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static CurrentFileSessionTestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"listforge-current-file-test-{Guid.NewGuid():N}");
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(ConfigManager.AppDir);
            Directory.CreateDirectory(ConfigManager.InternalStateDir);
            return new CurrentFileSessionTestEnvironment(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
