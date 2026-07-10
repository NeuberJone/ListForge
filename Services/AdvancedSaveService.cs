using System.IO.Compression;
using ListForge.Core;

namespace ListForge.Services;

public enum AdvancedSaveMode
{
    LooseFiles,
    Zip,
}

public sealed record AdvancedSaveRequest(
    string OutputDirectory,
    string BaseName,
    string InputText,
    string OutputText,
    string JsonText,
    AdvancedSaveMode Mode);

public sealed record AdvancedSaveResult(
    string BaseName,
    string OutputDirectory,
    AdvancedSaveMode Mode,
    IReadOnlyList<string> FilePaths,
    string? ZipPath);

public sealed class AdvancedSaveService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public OperationResult<AdvancedSaveResult> Save(AdvancedSaveRequest request)
    {
        var validation = Validate(request);
        if (!validation.Success)
        {
            AppLogger.Warning("AdvancedSave", validation.TechnicalMessage);
            return OperationResult<AdvancedSaveResult>.Fail(
                validation.UserMessage,
                validation.TechnicalMessage,
                validation.Exception,
                validation.ErrorCode);
        }

        try
        {
            var outputDirectory = Path.GetFullPath(request.OutputDirectory.Trim());
            var safeBaseName = FileNameHelper.SanitizeBaseFilename(request.BaseName);

            AppLogger.Info("AdvancedSave", $"Iniciando exportacao avancada. Modo: {request.Mode}.");
            Directory.CreateDirectory(outputDirectory);

            var actualBaseName = ResolveAvailableBaseName(outputDirectory, safeBaseName, request.Mode);
            var inputName = $"{actualBaseName}-entrada.txt";
            var outputName = $"{actualBaseName}-saida.txt";
            var jsonName = $"{actualBaseName}.json";

            if (request.Mode == AdvancedSaveMode.Zip)
            {
                var zipPath = Path.Combine(outputDirectory, $"{actualBaseName}.zip");
                SaveZip(zipPath, request, inputName, outputName, jsonName);

                AppLogger.Info("AdvancedSave", $"Exportacao avancada concluida em ZIP: {zipPath}");
                return OperationResult<AdvancedSaveResult>.Ok(
                    new AdvancedSaveResult(actualBaseName, outputDirectory, request.Mode, [zipPath], zipPath),
                    "Salvar avançado concluído.",
                    "Exportação avançada em ZIP concluída.");
            }

            var inputPath = Path.Combine(outputDirectory, inputName);
            var outputPath = Path.Combine(outputDirectory, outputName);
            var jsonPath = Path.Combine(outputDirectory, jsonName);

            File.WriteAllText(inputPath, request.InputText, Utf8NoBom);
            File.WriteAllText(outputPath, request.OutputText, Utf8NoBom);
            File.WriteAllText(jsonPath, request.JsonText, Utf8NoBom);

            AppLogger.Info("AdvancedSave", $"Exportacao avancada concluida em arquivos soltos: {outputDirectory}");
            return OperationResult<AdvancedSaveResult>.Ok(
                new AdvancedSaveResult(actualBaseName, outputDirectory, request.Mode, [inputPath, outputPath, jsonPath], null),
                "Salvar avançado concluído.",
                "Exportação avançada em arquivos soltos concluída.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("AdvancedSave", "Falha ao salvar exportacao avancada.", ex, request.OutputDirectory);
            return OperationResult<AdvancedSaveResult>.Fail(
                $"Falha ao usar o Salvar avançado.\n\n{ex.Message}",
                "Falha ao salvar exportação avançada.",
                ex,
                "AdvancedSaveFailed");
        }
    }

    private static OperationResult Validate(AdvancedSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BaseName))
            return OperationResult.Fail(
                "Informe um nome base para o Salvar avançado.",
                "Nome base vazio.",
                errorCode: "EmptyBaseName");

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            return OperationResult.Fail(
                "Escolha uma pasta para salvar os arquivos.",
                "Pasta de saída vazia.",
                errorCode: "EmptyOutputDirectory");

        if (string.IsNullOrWhiteSpace(request.InputText))
            return OperationResult.Fail(
                "Cole ou abra uma lista na entrada antes de usar o Salvar avançado.",
                "Entrada vazia.",
                errorCode: "EmptyInput");

        if (string.IsNullOrWhiteSpace(request.OutputText))
            return OperationResult.Fail(
                "Processe a lista antes de usar o Salvar avançado.",
                "Saída vazia.",
                errorCode: "EmptyOutput");

        if (string.IsNullOrWhiteSpace(request.JsonText))
            return OperationResult.Fail(
                "Gere ou atualize o JSON antes de usar o Salvar avançado.",
                "JSON vazio.",
                errorCode: "EmptyJson");

        return OperationResult.Ok();
    }

    private static string ResolveAvailableBaseName(string outputDirectory, string safeBaseName, AdvancedSaveMode mode)
    {
        var candidate = safeBaseName;
        var index = 2;

        while (TargetsExist(outputDirectory, candidate, mode))
        {
            candidate = $"{safeBaseName}_v{index}";
            index++;
        }

        return candidate;
    }

    private static bool TargetsExist(string outputDirectory, string baseName, AdvancedSaveMode mode)
    {
        if (mode == AdvancedSaveMode.Zip)
            return File.Exists(Path.Combine(outputDirectory, $"{baseName}.zip"));

        return File.Exists(Path.Combine(outputDirectory, $"{baseName}-entrada.txt"))
            || File.Exists(Path.Combine(outputDirectory, $"{baseName}-saida.txt"))
            || File.Exists(Path.Combine(outputDirectory, $"{baseName}.json"));
    }

    private static void SaveZip(
        string zipPath,
        AdvancedSaveRequest request,
        string inputName,
        string outputName,
        string jsonName)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddTextEntry(archive, inputName, request.InputText);
        AddTextEntry(archive, outputName, request.OutputText);
        AddTextEntry(archive, jsonName, request.JsonText);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Utf8NoBom);
        writer.Write(content);
    }
}
