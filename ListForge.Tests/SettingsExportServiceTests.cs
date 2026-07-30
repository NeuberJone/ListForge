using System.Text;
using ListForge.Config;
using ListForge.Models;
using ListForge.Services;
using Newtonsoft.Json.Linq;

namespace ListForge.Tests;

public class SettingsExportServiceTests
{
    [Fact]
    public void ExportToFile_WritesVersionedUtf8JsonWithoutSessionOrSensitiveFields()
    {
        using var env = SettingsExportTestEnvironment.Create();
        var service = new SettingsExportService();
        var path = Path.Combine(env.Root, "settings.json");
        var snapshot = new SettingsExportSnapshot(
            new AppConfig
            {
                ThemeName = "SISBolt",
                DefaultListName = "pedido",
                LastOpenedFile = @"C:\Users\user\lista.csv",
                OutputDir = @"C:\Users\user\Output",
                CheckUpdatesOnStartup = false,
            },
            SizeConfig.Default(),
            "2.1.35");

        var result = service.ExportToFile(path, snapshot);

        Assert.True(result.Success);
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        var json = File.ReadAllText(path, Encoding.UTF8);
        var parsed = JObject.Parse(json);
        Assert.Equal(1, (int)parsed["schemaVersion"]!);
        Assert.Equal("ListForge", (string)parsed["application"]!);
        Assert.Equal("2.1.35", (string)parsed["applicationVersion"]!);
        Assert.Contains("SISBolt", json);
        Assert.Contains("pedido", json);
        Assert.DoesNotContain("LastOpenedFile", json);
        Assert.DoesNotContain("lista.csv", json);
        Assert.DoesNotContain("SecretOutput", json);
        Assert.DoesNotContain("Trial", json);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportToFile_ReturnsFailureWhenPathCannotBeWritten()
    {
        using var env = SettingsExportTestEnvironment.Create();
        var service = new SettingsExportService();
        var snapshot = new SettingsExportSnapshot(new AppConfig(), SizeConfig.Default(), "2.1.35");

        var result = service.ExportToFile(env.Root, snapshot);

        Assert.False(result.Success);
        Assert.Equal("SettingsExportFailed", result.ErrorCode);
    }

    [Fact]
    public void ImportFromFile_AppliesExportedSafeSettingsAndPreservesLocalOnlyState()
    {
        using var env = SettingsExportTestEnvironment.Create();
        var service = new SettingsExportService();
        var path = Path.Combine(env.Root, "settings.json");
        var exportedSizes = SizeConfig.Default();
        exportedSizes.Groups["male"].BaseSizes = ["P", "M", "G", "GG"];

        var exportResult = service.ExportToFile(path, new SettingsExportSnapshot(
            new AppConfig
            {
                ThemeName = "SISBolt",
                EditorFontSize = 40,
                ShowJsonTab = true,
                ShowGenerateJsonButton = true,
                ShowCopyJsonButton = true,
                UseAdvancedJsonPieceMapping = true,
                AdvancedJsonPieceOrder = ["ShortSleeve", "Pants"],
                AdvancedSaveMode = "Zip",
                UseDefaultOutputDir = true,
                OutputDir = @"C:\Sensitive\Output",
                UseDefaultListName = true,
                DefaultListName = "pedido",
                DefaultCaseMode = "upper",
                DefaultInputSeparator = ";",
                CheckUpdatesOnStartup = false,
                LastOpenedFile = @"C:\Sensitive\lista.csv",
                LastAvailableUpdateVersion = "9.9.9",
            },
            exportedSizes,
            "2.1.35"));
        Assert.True(exportResult.Success);

        var current = new AppConfig
        {
            OutputDir = @"D:\ListForge",
            LastAvailableUpdateVersion = "2.1.40",
            LastOpenedFile = @"D:\old.csv",
        };
        var importResult = service.ImportFromFile(path, current, SizeConfig.Default());

        Assert.True(importResult.Success);
        var imported = importResult.Value!.Config;
        Assert.Equal("SISBolt", imported.ThemeName);
        Assert.Equal(32, imported.EditorFontSize);
        Assert.True(imported.ShowJsonTab);
        Assert.True(imported.UseAdvancedJsonPieceMapping);
        Assert.Equal(["ShortSleeve", "Pants"], imported.AdvancedJsonPieceOrder);
        Assert.Equal("Zip", imported.AdvancedSaveMode);
        Assert.True(imported.UseDefaultOutputDir);
        Assert.Equal(@"D:\ListForge", imported.OutputDir);
        Assert.Equal("2.1.40", imported.LastAvailableUpdateVersion);
        Assert.Equal("", imported.LastOpenedFile);
        Assert.Equal(["P", "M", "G", "GG"], importResult.Value.Sizes.Groups["male"].BaseSizes);
    }

    [Fact]
    public void ImportFromFile_ReturnsFailureForUnsupportedDocument()
    {
        using var env = SettingsExportTestEnvironment.Create();
        var service = new SettingsExportService();
        var path = Path.Combine(env.Root, "settings.json");
        File.WriteAllText(path, """{"schemaVersion":99,"application":"Other","settings":{}}""", new UTF8Encoding(false));

        var result = service.ImportFromFile(path, new AppConfig(), SizeConfig.Default());

        Assert.False(result.Success);
        Assert.Equal("SettingsImportUnsupportedDocument", result.ErrorCode);
    }

    private sealed class SettingsExportTestEnvironment : IDisposable
    {
        private SettingsExportTestEnvironment(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static SettingsExportTestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"listforge-settings-export-test-{Guid.NewGuid():N}");
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            Directory.CreateDirectory(root);
            return new SettingsExportTestEnvironment(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
