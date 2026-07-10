using System.IO.Compression;
using ListForge.Config;
using ListForge.Core;
using ListForge.Services;

namespace ListForge.Tests;

public class AdvancedSaveServiceTests
{
    [Fact]
    public void Save_LooseFilesWritesExpectedNamesAndContents()
    {
        using var env = AdvancedSaveTestEnvironment.Create();
        var service = new AdvancedSaveService();

        var result = service.Save(Request(env.OutputDir, "pedido-teste", AdvancedSaveMode.LooseFiles));

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(
            ["pedido-teste-entrada.txt", "pedido-teste-saida.txt", "pedido-teste.json"],
            result.Value!.FilePaths.Select(path => Path.GetFileName(path) ?? "").ToArray());
        Assert.Equal("ANA,10,P", File.ReadAllText(Path.Combine(env.OutputDir, "pedido-teste-entrada.txt")));
        Assert.Equal("ANA,10,P", File.ReadAllText(Path.Combine(env.OutputDir, "pedido-teste-saida.txt")));
        Assert.Equal("""{"orders":[]}""", File.ReadAllText(Path.Combine(env.OutputDir, "pedido-teste.json")));
    }

    [Fact]
    public void Save_ZipWritesOnlyExpectedEntries()
    {
        using var env = AdvancedSaveTestEnvironment.Create();
        var service = new AdvancedSaveService();

        var result = service.Save(Request(env.OutputDir, "pedido-teste", AdvancedSaveMode.Zip));

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("pedido-teste.zip", Path.GetFileName(result.Value!.ZipPath));

        using var archive = ZipFile.OpenRead(result.Value.ZipPath!);
        var expectedEntries = new[]
        {
            "pedido-teste-entrada.txt",
            "pedido-teste-saida.txt",
            "pedido-teste.json",
        }.OrderBy(name => name).ToArray();
        var actualEntries = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name).ToArray();

        Assert.Equal(
            expectedEntries,
            actualEntries);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("log", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("config", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("trial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_UsesSameVersionedBaseForAllLooseFiles()
    {
        using var env = AdvancedSaveTestEnvironment.Create();
        var service = new AdvancedSaveService();
        File.WriteAllText(Path.Combine(env.OutputDir, "pedido-entrada.txt"), "existing");

        var result = service.Save(Request(env.OutputDir, "pedido", AdvancedSaveMode.LooseFiles));

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(
            ["pedido_v2-entrada.txt", "pedido_v2-saida.txt", "pedido_v2.json"],
            result.Value!.FilePaths.Select(path => Path.GetFileName(path) ?? "").ToArray());
    }

    [Fact]
    public void Save_SanitizesInvalidBaseName()
    {
        using var env = AdvancedSaveTestEnvironment.Create();
        var service = new AdvancedSaveService();

        var result = service.Save(Request(env.OutputDir, "pedido:teste/01", AdvancedSaveMode.LooseFiles));

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("pedido_teste_01", result.Value!.BaseName);
        Assert.True(File.Exists(Path.Combine(env.OutputDir, "pedido_teste_01-entrada.txt")));
    }

    [Theory]
    [InlineData("", "ANA,10,P", "ANA,10,P", """{"orders":[]}""", "EmptyBaseName")]
    [InlineData("pedido", "", "ANA,10,P", """{"orders":[]}""", "EmptyInput")]
    [InlineData("pedido", "ANA,10,P", "", """{"orders":[]}""", "EmptyOutput")]
    [InlineData("pedido", "ANA,10,P", "ANA,10,P", "", "EmptyJson")]
    public void Save_BlocksMissingRequiredData(
        string baseName,
        string input,
        string output,
        string json,
        string errorCode)
    {
        using var env = AdvancedSaveTestEnvironment.Create();
        var service = new AdvancedSaveService();

        var result = service.Save(new AdvancedSaveRequest(
            env.OutputDir,
            baseName,
            input,
            output,
            json,
            AdvancedSaveMode.LooseFiles));

        Assert.False(result.Success);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Empty(Directory.GetFiles(env.OutputDir));
    }

    [Fact]
    public void Save_DoesNotConsumeTrialCredit()
    {
        using var env = AdvancedSaveTestEnvironment.Create(isTrial: true, limit: 2);
        var service = new AdvancedSaveService();

        var result = service.Save(Request(env.OutputDir, "pedido", AdvancedSaveMode.Zip));

        Assert.True(result.Success);
        Assert.Equal(2, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    private static AdvancedSaveRequest Request(string outputDir, string baseName, AdvancedSaveMode mode) =>
        new(
            outputDir,
            baseName,
            "ANA,10,P",
            "ANA,10,P",
            """{"orders":[]}""",
            mode);

    private sealed class AdvancedSaveTestEnvironment : IDisposable
    {
        private readonly string _root;

        private AdvancedSaveTestEnvironment(string root)
        {
            _root = root;
            OutputDir = Path.Combine(root, "out");
            Directory.CreateDirectory(OutputDir);
        }

        public string OutputDir { get; }

        public static AdvancedSaveTestEnvironment Create(bool isTrial = false, int limit = 10)
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-AdvancedSaveServiceTests", Guid.NewGuid().ToString("N"));
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
            AppLogger.SetLogDirectoryForTesting(Path.Combine(root, "logs"));
            TrialManager.SetTrialModeForTesting(isTrial, limit);
            return new AdvancedSaveTestEnvironment(root);
        }

        public void Dispose()
        {
            TrialManager.SetTrialModeForTesting(null);
            AppLogger.SetLogDirectoryForTesting(null);

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
