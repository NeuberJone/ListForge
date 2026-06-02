using System.IO.Compression;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class SupportPackageServiceTests
{
    [Fact]
    public void Generate_CreatesZipWithSupportInfoAndSummaries()
    {
        using var env = SupportPackageTestEnvironment.Create();
        var service = new SupportPackageService();

        var result = service.Generate(env.OutputDir, env.AboutInfo);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.Value));
        using var archive = ZipFile.OpenRead(result.Value!);
        Assert.Contains(archive.Entries, entry => entry.FullName == "support-info.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName == "config-summary.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName == "sizes-summary.txt");

        var supportInfo = ReadEntry(archive, "support-info.txt");
        Assert.Contains("Privacidade", supportInfo);
        Assert.DoesNotContain(ConfigManager.TrialStatePath, supportInfo);
        Assert.DoesNotContain(ConfigManager.InternalStateDir, supportInfo);
    }

    [Fact]
    public void Generate_IncludesRecentLogsWithoutForbiddenFiles()
    {
        using var env = SupportPackageTestEnvironment.Create();
        File.WriteAllText(Path.Combine(ConfigManager.LogDir, "listforge-2026-06-01.log"), "log 1");
        File.WriteAllText(Path.Combine(ConfigManager.LogDir, "listforge-2026-06-02.log"), "log 2");
        File.WriteAllText(ConfigManager.TrialStatePath, "trial-state");
        File.WriteAllText(Path.Combine(ConfigManager.AppDir, "lista-real.txt"), "ANA,10,G");
        var service = new SupportPackageService();

        var result = service.Generate(env.OutputDir, env.AboutInfo);

        Assert.True(result.Success);
        using var archive = ZipFile.OpenRead(result.Value!);
        var entryNames = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains("logs/listforge-2026-06-01.log", entryNames);
        Assert.Contains("logs/listforge-2026-06-02.log", entryNames);
        Assert.DoesNotContain(entryNames, name => name.Contains("trial", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entryNames, name => name.Contains("lista-real", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_OmitsSensitiveConfigValues()
    {
        using var env = SupportPackageTestEnvironment.Create();
        ConfigManager.SaveConfig(new AppConfig
        {
            OutputDir = @"C:\Users\user\SecretOutput",
            LastOpenedFile = @"C:\Users\user\secret-list.csv",
            DefaultListName = "lista",
            ThemeName = "ListForge Dark",
        });
        var service = new SupportPackageService();

        var result = service.Generate(env.OutputDir, env.AboutInfo);

        Assert.True(result.Success);
        using var archive = ZipFile.OpenRead(result.Value!);
        var configSummary = ReadEntry(archive, "config-summary.txt");
        Assert.DoesNotContain("SecretOutput", configSummary);
        Assert.DoesNotContain("secret-list.csv", configSummary);
        Assert.Contains("ThemeName", configSummary);
    }

    [Fact]
    public void Generate_WorksWithoutLogs()
    {
        using var env = SupportPackageTestEnvironment.Create();
        var service = new SupportPackageService();

        var result = service.Generate(env.OutputDir, env.AboutInfo);

        Assert.True(result.Success);
        using var archive = ZipFile.OpenRead(result.Value!);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Entry not found: {name}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class SupportPackageTestEnvironment : IDisposable
    {
        private readonly string _root;

        private SupportPackageTestEnvironment(string root, string outputDir, AboutInfo aboutInfo)
        {
            _root = root;
            OutputDir = outputDir;
            AboutInfo = aboutInfo;
        }

        public string OutputDir { get; }

        public AboutInfo AboutInfo { get; }

        public static SupportPackageTestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"listforge-support-test-{Guid.NewGuid():N}");
            var appDir = Path.Combine(root, "app");
            var stateDir = Path.Combine(root, "state");
            var outputDir = Path.Combine(root, "out");

            ConfigManager.SetDirectoriesForTesting(appDir, stateDir);
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(ConfigManager.LogDir);
            Directory.CreateDirectory(ConfigManager.InternalStateDir);

            var aboutInfo = new AboutInfo(
                "ListForge",
                "2.1.22",
                "Trial",
                "Não definido",
                true,
                5,
                10,
                "Neuber Jone",
                "GitHub: https://github.com/NeuberJone",
                ConfigManager.AppDir,
                ConfigManager.LogDir,
                "Windows");

            return new SupportPackageTestEnvironment(root, outputDir, aboutInfo);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
