using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ListForge.Config;
using Tesseract;
using UglyToad.PdfPig;

namespace ListForge.Core;

public static class FileImporter
{
    public static readonly HashSet<string> TextExtensions = [".txt", ".csv", ".list"];
    public static readonly HashSet<string> PdfExtensions = [".pdf"];
    public static readonly HashSet<string> WordExtensions = [".docx", ".doc"];
    public static readonly HashSet<string> ExcelExtensions = [".xlsx", ".xlsm", ".xls"];
    public static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp"];

    // ---------------------------------------------------------------
    // Text files
    // ---------------------------------------------------------------
    public static string ReadTextFile(string path)
    {
        foreach (var enc in new[] { "utf-8-sig", "utf-8", "windows-1252", "iso-8859-1" })
        {
            try
            {
                var encoding = Encoding.GetEncoding(enc);
                return File.ReadAllText(path, encoding);
            }
            catch { /* try next */ }
        }
        throw new IOException("Não foi possível ler o arquivo com as codificações suportadas.");
    }

    public static void WriteTextFile(string path, string text) =>
        File.WriteAllText(path, text, new UTF8Encoding(false));

    // ---------------------------------------------------------------
    // PDF
    // ---------------------------------------------------------------
    public static string ReadPdfText(string path)
    {
        using var doc = PdfDocument.Open(path);
        var chunks = new List<string>();

        foreach (var page in doc.GetPages())
        {
            var words = page.GetWords().Select(w => w.Text);
            var text = string.Join(" ", words).Trim();
            if (!string.IsNullOrEmpty(text))
                chunks.Add(text);
        }

        var result = string.Join("\n\n", chunks).Trim();
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException(
                "Não consegui extrair texto desse PDF.\nSe ele for um PDF escaneado como imagem, use uma imagem/OCR.");

        return result;
    }

    // ---------------------------------------------------------------
    // Word (.docx)
    // ---------------------------------------------------------------
    public static string ReadDocxText(string path)
    {
        using var wordDoc = WordprocessingDocument.Open(path, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Documento Word sem corpo.");

        var lines = new List<string>();

        foreach (var para in body.Elements<Paragraph>())
        {
            var text = NormalizeCell(para.InnerText);
            if (!string.IsNullOrEmpty(text)) lines.Add(text);
        }

        foreach (var table in body.Elements<Table>())
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>()
                    .Select(c => NormalizeCell(c.InnerText))
                    .ToList();

                while (cells.Count > 0 && cells[^1] == "") cells.RemoveAt(cells.Count - 1);
                if (cells.Any(c => !string.IsNullOrEmpty(c)))
                    lines.Add(string.Join(",", cells));
            }
        }

        var result = string.Join("\n", lines).Trim();
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException("Não consegui extrair texto útil desse arquivo Word.");
        return result;
    }

    // ---------------------------------------------------------------
    // Excel (.xlsx / .xlsm)
    // ---------------------------------------------------------------
    public static string ReadExcelText(string path)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook(path);
        var ws = wb.Worksheets.First();

        var lines = new List<string>();

        foreach (var row in ws.RowsUsed())
        {
            var cells = row.Cells()
                .Select(c => NormalizeCell(c.Value.ToString() ?? ""))
                .ToList();

            while (cells.Count > 0 && cells[^1] == "") cells.RemoveAt(cells.Count - 1);
            if (cells.Any(c => !string.IsNullOrEmpty(c)))
                lines.Add(string.Join(",", cells));
        }

        var result = string.Join("\n", lines).Trim();
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException("Não consegui extrair texto útil dessa planilha.");
        return result;
    }

    // ---------------------------------------------------------------
    // OCR via bundled Tesseract
    // ---------------------------------------------------------------
    public static string OcrImageToText(string path)
    {
        var tessDataDir = ResolveTessDataDir();

        string bestText = "";
        int bestScore = -1;

        // Tesseract C# wrapper works on Pix — we try multiple PSM configs
        var psmModes = new[]
        {
            PageSegMode.Auto,
            PageSegMode.SingleColumn,
            PageSegMode.SingleBlockVertText,
        };

        using var engine = new TesseractEngine(tessDataDir, "por+eng", EngineMode.Default);

        using var pix = Pix.LoadFromFile(path);

        // Try upscaled grayscale and binarized variants
        var variants = BuildImageVariants(pix);

        foreach (var variant in variants)
        {
            foreach (var psm in psmModes)
            {
                try
                {
                    using var page = engine.Process(variant, psm);
                    var text = NormalizeImportedText(page.GetText() ?? "");
                    var score = ScoreOcrText(text);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestText = text;
                    }
                }
                catch { /* try next */ }
            }

            if (variant != pix) variant.Dispose();
        }

        if (string.IsNullOrWhiteSpace(bestText))
            throw new InvalidOperationException("Nenhum texto útil foi reconhecido na imagem.");

        return bestText;
    }

    private static string ResolveTessDataDir()
    {
        // 1. Environment variable override
        var envCmd = (Environment.GetEnvironmentVariable("TESSERACT_CMD") ?? "").Trim();
        if (!string.IsNullOrEmpty(envCmd) && File.Exists(envCmd))
        {
            var tessData = Path.Combine(Path.GetDirectoryName(envCmd)!, "tessdata");
            if (Directory.Exists(tessData)) return tessData;
        }

        // 2. Bundled tesseract/ directory beside the exe
        var exeDir = AppContext.BaseDirectory;
        var bundledTessData = Path.Combine(exeDir, "tesseract", "tessdata");
        if (Directory.Exists(bundledTessData)) return bundledTessData;

        // 3. System installs
        var candidates = new[]
        {
            @"C:\Program Files\Tesseract-OCR\tessdata",
            @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
        };

        foreach (var c in candidates)
            if (Directory.Exists(c)) return c;

        throw new InvalidOperationException(
            "O Tesseract OCR (tessdata) não foi encontrado.\n" +
            "Certifique-se de que a pasta tesseract/tessdata existe junto ao executável.");
    }

    private static List<Pix> BuildImageVariants(Pix original)
    {
        var variants = new List<Pix>();

        try
        {
            // Grayscale + auto-contrast via scale
            var gray = original.ConvertRGBToGray();
            var scale = Math.Max(original.Width, original.Height) < 1400 ? 3 : 2;
            var scaled = gray.Scale(scale, scale);
            variants.Add(scaled);

            // Binary (threshold ~185 → 0.73 of 255)
            var bw = scaled.BinarizeOtsuAdaptiveThreshold(32, 32, 0, 0, 0.1f);
            if (bw != null) variants.Add(bw);
        }
        catch
        {
            variants.Add(original);
        }

        return variants;
    }

    private static int ScoreOcrText(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return -1;
        var useful = lines.Count(l => Regex.IsMatch(l, @"[A-Za-zÀ-ÿ0-9]"));
        return useful * 10 + lines.Count;
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------
    public static string NormalizeImportedText(string text) =>
        string.Join("\n",
            text.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n')
                .Select(l => l.TrimEnd()))
        .Trim();

    private static string NormalizeCell(string? value)
    {
        if (value == null) return "";
        return Regex.Replace(
            value.Replace("\r", " ").Replace("\n", " ").Replace("\x07", " "),
            @"\s+", " ").Trim();
    }
}
