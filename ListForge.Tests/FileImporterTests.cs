using System;
using System.IO;
using ListForge.Core;

namespace ListForge.Tests;

public class FileImporterTests
{
    [Fact]
    public void NormalizeImportedText_NormalizesLineBreaksAndTrimsTrailingWhitespace()
    {
        var normalized = FileImporter.NormalizeImportedText(" ANA,10,G \r\nBRUNO,7,M\t\rCARLA,5,P\n\n");

        Assert.Equal("ANA,10,G\nBRUNO,7,M\nCARLA,5,P", normalized);
    }

    [Fact]
    public void WriteTextFileAndReadTextFile_RoundTripUtf8Text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"listforge-test-{Guid.NewGuid():N}.txt");

        try
        {
            FileImporter.WriteTextFile(path, "ANA,10,G\nJOAO,7,M");

            var text = FileImporter.ReadTextFile(path);

            Assert.Equal("ANA,10,G\nJOAO,7,M", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
