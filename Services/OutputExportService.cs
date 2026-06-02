using System.Collections.Generic;
using ListForge.Core;

namespace ListForge.Services;

public sealed class OutputExportService
{
    public string SaveOutputText(string outputText, string outputDirectory, string baseName) =>
        ListProcessor.ExportOutputText(outputText, outputDirectory, baseName);

    public string SaveJson(List<Dictionary<string, string>> orders, string outputDirectory, string baseName) =>
        ListProcessor.ExportJson(orders, outputDirectory, baseName);
}
