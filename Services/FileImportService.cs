using ListForge.Core;

namespace ListForge.Services;

public sealed record FileImportResult(
    string Text,
    bool IsPlainText,
    string StatusMessage,
    string? ReviewMessage);

public sealed class FileImportService
{
    public OperationResult<FileImportResult> ImportInputFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (FileImporter.TextExtensions.Contains(ext))
            {
                var text = FileImporter.ReadTextFile(path);
                return OperationResult<FileImportResult>.Ok(new FileImportResult(
                    text,
                    true,
                    $"Lista carregada: {Path.GetFileName(path)}",
                    null));
            }

            string imported;
            string warning;

            if (FileImporter.PdfExtensions.Contains(ext))
            {
                imported = FileImporter.ReadPdfText(path);
                warning = "Texto extraído do PDF.\n\nConfira o conteúdo antes de processar.";
            }
            else if (FileImporter.WordExtensions.Contains(ext))
            {
                imported = FileImporter.ReadDocxText(path);
                warning = "Texto extraído do Word.\n\nConfira o conteúdo antes de processar.";
            }
            else if (FileImporter.ExcelExtensions.Contains(ext))
            {
                imported = FileImporter.ReadExcelText(path);
                warning = "Texto extraído da planilha.\n\nConfira o conteúdo antes de processar.";
            }
            else if (FileImporter.ImageExtensions.Contains(ext))
            {
                imported = FileImporter.OcrImageToText(path);
                warning = "Texto extraído da imagem via OCR.\n\nConfira o conteúdo - OCR não é 100% confiável.";
            }
            else
            {
                return OperationResult<FileImportResult>.Fail(
                    "Formato não suportado.",
                    $"Formato de arquivo não suportado: {path}",
                    errorCode: "UnsupportedFileFormat");
            }

            var normalized = FileImporter.NormalizeImportedText(imported);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Não foi possível obter conteúdo útil desse arquivo.");

            return OperationResult<FileImportResult>.Ok(new FileImportResult(
                normalized,
                false,
                $"Importado: {Path.GetFileName(path)}",
                warning));
        }
        catch (Exception ex)
        {
            return OperationResult<FileImportResult>.Fail(
                $"Erro ao importar arquivo:\n\n{ex.Message}",
                "Falha ao importar arquivo.",
                ex,
                "ImportFileFailed");
        }
    }

    public OperationResult<string> ReadTextFile(string path)
    {
        try
        {
            return OperationResult<string>.Ok(FileImporter.ReadTextFile(path));
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                $"Erro ao ler arquivo:\n\n{ex.Message}",
                "Falha ao ler arquivo de texto.",
                ex,
                "ReadTextFileFailed");
        }
    }

    public OperationResult<string> SaveTextFile(string path, string text)
    {
        try
        {
            FileImporter.WriteTextFile(path, text);
            return OperationResult<string>.Ok(path);
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(
                $"Erro ao salvar arquivo:\n\n{ex.Message}",
                "Falha ao salvar arquivo de texto.",
                ex,
                "SaveTextFileFailed");
        }
    }
}
