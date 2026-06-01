using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ListForge.Core;

public static class FileNameHelper
{
    public static string SanitizeBaseFilename(string name)
    {
        var text = (name ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            text = DateTime.Now.ToString("lista-yyyyMMdd-HHmmss");

        foreach (var ch in @"\/:*?""<>|")
            text = text.Replace(ch.ToString(), "_");

        text = Regex.Replace(text, @"\s+", " ").Trim(' ', '.');
        return string.IsNullOrEmpty(text) ? DateTime.Now.ToString("lista-yyyyMMdd-HHmmss") : text;
    }

    public static string VersionedPath(string directory, string baseName, string suffix)
    {
        var safeBase = SanitizeBaseFilename(baseName);
        var path = Path.Combine(directory, $"{safeBase}{suffix}");
        if (!File.Exists(path)) return path;

        var idx = 2;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{safeBase}_v{idx}{suffix}");
            if (!File.Exists(candidate)) return candidate;
            idx++;
        }
    }
}
