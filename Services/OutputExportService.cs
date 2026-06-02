using System.Collections.Generic;
using ListForge.Core;

namespace ListForge.Services;

public sealed class OutputExportService
{
    public OperationResult<string> SaveOutputText(string outputText, string outputDirectory, string baseName)
    {
        try
        {
            return OperationResult<string>.Ok(ListProcessor.ExportOutputText(outputText, outputDirectory, baseName));
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                $"Falha ao salvar saída organizada.\n\n{ex.Message}",
                "Falha ao exportar saída textual.",
                ex,
                "ExportOutputFailed");
        }
    }

    public OperationResult<string> SaveJson(List<Dictionary<string, string>> orders, string outputDirectory, string baseName)
    {
        try
        {
            return OperationResult<string>.Ok(ListProcessor.ExportJson(orders, outputDirectory, baseName));
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                $"Falha ao gerar JSON.\n\n{ex.Message}",
                "Falha ao exportar JSON.",
                ex,
                "ExportJsonFailed");
        }
    }
}
