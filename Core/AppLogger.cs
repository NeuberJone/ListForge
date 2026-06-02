using System;
using System.IO;
using System.Reflection;
using System.Text;
using ListForge.Config;

namespace ListForge.Core;

public static class AppLogger
{
    private static readonly object Sync = new();
    private static string? _logDirectoryOverride;

    public static string LogDirectory =>
        _logDirectoryOverride ?? Path.Combine(ConfigManager.AppDir, "logs");

    public static string CurrentLogPath =>
        BuildLogPath(DateTime.Now);

    public static void Info(string context, string message, string? relatedFilePath = null) =>
        Write("INFO", context, message, null, relatedFilePath);

    public static void Warning(string context, string message, string? relatedFilePath = null) =>
        Write("WARN", context, message, null, relatedFilePath);

    public static void Warning(string context, string message, Exception exception, string? relatedFilePath = null) =>
        Write("WARN", context, message, exception, relatedFilePath);

    public static void Error(string context, string message, Exception? exception = null, string? relatedFilePath = null) =>
        Write("ERROR", context, message, exception, relatedFilePath);

    public static string BuildLogPath(DateTime date) =>
        Path.Combine(LogDirectory, $"listforge-{date:yyyy-MM-dd}.log");

    public static void SetLogDirectoryForTesting(string? directory) =>
        _logDirectoryOverride = directory;

    private static void Write(string level, string context, string message, Exception? exception, string? relatedFilePath)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = FormatEntry(level, context, message, exception, relatedFilePath);

            lock (Sync)
                File.AppendAllText(CurrentLogPath, entry, new UTF8Encoding(false));
        }
        catch
        {
            // Logging must never break the app or create recursive logging failures.
        }
    }

    private static string FormatEntry(
        string level,
        string context,
        string message,
        Exception? exception,
        string? relatedFilePath)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var sb = new StringBuilder();

        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [ListForge {version} - {ConfigManager.EditionName}] [{context}]");
        sb.AppendLine($"Message: {message}");

        if (!string.IsNullOrWhiteSpace(relatedFilePath))
            sb.AppendLine($"File: {relatedFilePath}");

        if (exception != null)
        {
            sb.AppendLine($"Exception: {exception.GetType().FullName}: {exception.Message}");
            if (exception.InnerException != null)
                sb.AppendLine($"InnerException: {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                sb.AppendLine($"StackTrace: {exception.StackTrace}");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
