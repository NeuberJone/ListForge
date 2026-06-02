using ListForge.Services;

namespace ListForge.Tests;

public class FileImportServiceTests
{
    [Fact]
    public void ImportInputFile_ReadsPlainTextFile()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "lista.txt");
        File.WriteAllText(path, "ANA,10,G");
        var service = new FileImportService();

        try
        {
            var result = service.ImportInputFile(path);

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.True(result.Value!.IsPlainText);
            Assert.Equal("ANA,10,G", result.Value.Text);
            Assert.Equal("Lista carregada: lista.txt", result.Value.StatusMessage);
            Assert.Null(result.Value.ReviewMessage);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void ImportInputFile_ReturnsFailureForUnsupportedFormat()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "lista.xyz");
        File.WriteAllText(path, "ANA,10,G");
        var service = new FileImportService();

        try
        {
            var result = service.ImportInputFile(path);

            Assert.False(result.Success);
            Assert.Equal("Formato não suportado.", result.UserMessage);
            Assert.Equal("UnsupportedFileFormat", result.ErrorCode);
            Assert.Null(result.Exception);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void SaveTextFile_WritesFileAndReturnsPath()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "saida.txt");
        var service = new FileImportService();

        try
        {
            var result = service.SaveTextFile(path, "BIA,11,M");

            Assert.True(result.Success);
            Assert.Equal(path, result.Value);
            Assert.Equal("BIA,11,M", File.ReadAllText(path));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ListForge-FileImportServiceTests", Guid.NewGuid().ToString("N"));
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
