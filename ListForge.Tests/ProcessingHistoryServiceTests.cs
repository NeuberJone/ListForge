using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class ProcessingHistoryServiceTests
{
    [Fact]
    public void Add_SavesAndLoadsEntriesNewestFirst()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "processing-history.json");
        var clock = new Queue<DateTimeOffset>([
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
        ]);
        var service = new ProcessingHistoryService(path, () => clock.Dequeue());

        try
        {
            var first = service.Add(NewEntry("lista-a.txt", 2, @"C:\Saidas\lista-a.txt"));
            var second = service.Add(NewEntry("lista-b.txt", 3, @"C:\Saidas\lista-b.txt"));

            Assert.True(first.Success);
            Assert.True(second.Success);

            var loaded = service.Load();
            Assert.Equal(2, loaded.Count);
            Assert.Equal("lista-b.txt", loaded[0].SourceDisplayName);
            Assert.Equal("lista-a.txt", loaded[1].SourceDisplayName);
            Assert.Equal(3, loaded[0].ProcessedLineCount);
            Assert.Equal(@"C:\Saidas\lista-b.txt", loaded[0].OutputPath);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Add_KeepsOnlyOneHundredEntriesWithoutDeduplication()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "processing-history.json");
        var current = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var service = new ProcessingHistoryService(path, () => current = current.AddMinutes(1));

        try
        {
            for (var i = 0; i < 105; i++)
            {
                var result = service.Add(NewEntry("lista.txt", i, $@"C:\Saidas\lista-{i}.txt"));
                Assert.True(result.Success);
            }

            var loaded = service.Load();
            Assert.Equal(ProcessingHistoryService.MaxEntries, loaded.Count);
            Assert.Equal(@"C:\Saidas\lista-104.txt", loaded[0].OutputPath);
            Assert.Equal(@"C:\Saidas\lista-5.txt", loaded[^1].OutputPath);
            Assert.Equal(100, loaded.Count(entry => entry.SourceDisplayName == "lista.txt"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Add_StoresOnlySafeSourceMetadata()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "processing-history.json");
        var service = new ProcessingHistoryService(path);

        try
        {
            var result = service.Add(NewEntry(
                @"C:\Users\neube\Documents\entrada-real-secreta.xlsx",
                1,
                @"C:\Saidas\lista.txt",
                ProcessingHistorySourceTypes.File));

            Assert.True(result.Success);
            var loaded = service.Load().Single();
            var rawJson = File.ReadAllText(path);

            Assert.Equal("entrada-real-secreta.xlsx", loaded.SourceDisplayName);
            Assert.DoesNotContain(@"C:\Users\neube\Documents", rawJson);
            Assert.DoesNotContain("ANA,10,G", rawJson);
            Assert.DoesNotContain("token", rawJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void BuildSafeSource_UsesFriendlyNamesForPastedTextAndLinks()
    {
        var pasted = ProcessingHistoryService.BuildSafeSource(null, "Arquivo atual: (nova lista)");
        var link = ProcessingHistoryService.BuildSafeSource(null, "Arquivo atual: (lista extraída do link)");

        Assert.Equal("Texto colado", pasted.DisplayName);
        Assert.Equal(ProcessingHistorySourceTypes.PastedText, pasted.SourceType);
        Assert.Equal("Lista extraída de link", link.DisplayName);
        Assert.Equal(ProcessingHistorySourceTypes.Link, link.SourceType);
    }

    [Fact]
    public void Load_InvalidFileStartsEmptyAndPreservesInvalidFile()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "processing-history.json");
        File.WriteAllText(path, "{ invalid json");
        var service = new ProcessingHistoryService(path);

        try
        {
            var loaded = service.Load();

            Assert.Empty(loaded);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.GetFiles(dir, "processing-history.json.invalid-*"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Clear_RemovesOnlyHistoryMetadata()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "processing-history.json");
        var outputPath = Path.Combine(dir, "saida.txt");
        File.WriteAllText(outputPath, "ANA,10,G");
        var service = new ProcessingHistoryService(path);

        try
        {
            Assert.True(service.Add(NewEntry("lista.txt", 1, outputPath)).Success);

            var clear = service.Clear();

            Assert.True(clear.Success);
            Assert.Empty(service.Load());
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void OpenOutputFolder_MissingFileReturnsFriendlyFailure()
    {
        var service = new ProcessingHistoryService(Path.Combine(CreateTempDir(), "processing-history.json"));

        var result = service.OpenOutputFolder(NewEntry("lista.txt", 1, @"C:\Saidas\arquivo-inexistente.txt"));

        Assert.False(result.Success);
        Assert.Equal("O arquivo de saída não foi encontrado.", result.UserMessage);
    }

    private static ProcessingHistoryEntry NewEntry(
        string sourceDisplayName,
        int processedLineCount,
        string outputPath,
        string sourceType = ProcessingHistorySourceTypes.PastedText) =>
        new()
        {
            SourceDisplayName = sourceDisplayName,
            SourceType = sourceType,
            ProcessedLineCount = processedLineCount,
            OutputPath = outputPath,
        };

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ListForge-ProcessingHistoryServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteTempDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Temporary test directories can be left for the OS to clean up.
        }
    }
}
