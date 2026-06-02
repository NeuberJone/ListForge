using ListForge.Services;
using Newtonsoft.Json.Linq;

namespace ListForge.Tests;

public class OutputExportServiceTests
{
    [Fact]
    public void SaveOutputText_UsesVersionedTextFile()
    {
        var service = new OutputExportService();
        var dir = CreateTempDir();

        try
        {
            var first = service.SaveOutputText("ANA,10,G", dir, "lista");
            var second = service.SaveOutputText("BIA,11,M", dir, "lista");

            Assert.Equal("lista.txt", Path.GetFileName(first));
            Assert.Equal("lista_v2.txt", Path.GetFileName(second));
            Assert.Equal("ANA,10,G", File.ReadAllText(first));
            Assert.Equal("BIA,11,M", File.ReadAllText(second));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void SaveJson_WritesWrappedOrders()
    {
        var service = new OutputExportService();
        var dir = CreateTempDir();

        try
        {
            var path = service.SaveJson(
                [new Dictionary<string, string> { ["Name"] = "ANA", ["Number"] = "10" }],
                dir,
                "lista");

            var root = JObject.Parse(File.ReadAllText(path));
            Assert.Equal("List", (string?)root["title"]);
            Assert.Equal("ANA", (string?)root["orders"]?[0]?["Name"]);
            Assert.Equal("10", (string?)root["orders"]?[0]?["Number"]);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ListForge-OutputExportServiceTests", Guid.NewGuid().ToString("N"));
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
