using System;
using System.IO;
using ListForge.Core;

namespace ListForge.Tests;

public class AppLoggerTests
{
    [Fact]
    public void BuildLogPath_UsesDailyListForgeFileName()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"listforge-logs-{Guid.NewGuid():N}");

        try
        {
            AppLogger.SetLogDirectoryForTesting(dir);

            var path = AppLogger.BuildLogPath(new DateTime(2026, 5, 31));

            Assert.Equal(Path.Combine(dir, "listforge-2026-05-31.log"), path);
        }
        finally
        {
            AppLogger.SetLogDirectoryForTesting(null);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Info_WritesMessageToDailyLog()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"listforge-logs-{Guid.NewGuid():N}");

        try
        {
            AppLogger.SetLogDirectoryForTesting(dir);

            AppLogger.Info("TestContext", "Mensagem de teste.");

            var logFile = Directory.GetFiles(dir, "listforge-*.log").Single();
            var text = File.ReadAllText(logFile);

            Assert.Contains("[INFO]", text);
            Assert.Contains("[TestContext]", text);
            Assert.Contains("Mensagem de teste.", text);
            Assert.Contains("ListForge", text);
        }
        finally
        {
            AppLogger.SetLogDirectoryForTesting(null);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Error_DoesNotThrowWhenLogDirectoryIsInvalid()
    {
        var invalidDir = Path.Combine(Path.GetTempPath(), $"listforge-log-file-{Guid.NewGuid():N}");
        File.WriteAllText(invalidDir, "");

        try
        {
            AppLogger.SetLogDirectoryForTesting(invalidDir);

            var ex = Record.Exception(() =>
                AppLogger.Error("TestContext", "Erro esperado em diretório inválido.", new InvalidOperationException("teste")));

            Assert.Null(ex);
        }
        finally
        {
            AppLogger.SetLogDirectoryForTesting(null);
            if (File.Exists(invalidDir))
                File.Delete(invalidDir);
        }
    }
}
