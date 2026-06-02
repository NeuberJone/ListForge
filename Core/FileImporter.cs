using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Exception? lastError = null;
        foreach (var enc in new[] { "utf-8-sig", "utf-8", "windows-1252", "iso-8859-1" })
        {
            try
            {
                var encoding = Encoding.GetEncoding(enc);
                return File.ReadAllText(path, encoding);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }
        AppLogger.Error("ImportTextFile", "Falha ao ler arquivo texto com as codificações suportadas.", lastError, path);
        throw new IOException("Não foi possível ler o arquivo com as codificações suportadas.");
    }

    public static void WriteTextFile(string path, string text)
    {
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppLogger.Error("WriteTextFile", "Falha ao gravar arquivo texto.", ex, path);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // PDF
    // ---------------------------------------------------------------
    public static string ReadPdfText(string path)
    {
        try
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
        catch (Exception ex)
        {
            AppLogger.Error("ImportPdf", "Falha ao extrair texto do PDF.", ex, path);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // Word (.docx)
    // ---------------------------------------------------------------
    public static string ReadDocxText(string path)
    {
        try
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
        catch (Exception ex)
        {
            AppLogger.Error("ImportWord", "Falha ao extrair texto do Word.", ex, path);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // Excel (.xlsx / .xlsm)
    // ---------------------------------------------------------------
    public static string ReadExcelText(string path)
    {
        try
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
        catch (Exception ex)
        {
            AppLogger.Error("ImportExcel", "Falha ao extrair texto da planilha.", ex, path);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // OCR via bundled Tesseract
    // ---------------------------------------------------------------
    public static string OcrImageToText(string path)
    {
        try
        {
            var cliText = TryOcrWithTesseractCli(path);
            if (!string.IsNullOrWhiteSpace(cliText))
                return cliText;

            var tessDataDir = ResolveTessDataDir();

            string bestText = "";
            int bestScore = -1;

            // Tesseract C# wrapper works on Pix — we try multiple PSM configs
            var psmModes = new[]
            {
                PageSegMode.Auto,
                PageSegMode.SingleColumn,
                PageSegMode.SingleBlock,
                PageSegMode.SparseText,
            };

            using var engine = new TesseractEngine(tessDataDir, "por+eng", EngineMode.Default);
            engine.SetVariable("preserve_interword_spaces", "1");

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
                    catch (Exception ex)
                    {
                        AppLogger.Warning("OcrWrapper", $"Falha em tentativa interna de OCR com PSM {psm}.", ex, path);
                    }
                }

                if (variant != pix) variant.Dispose();
            }

            if (string.IsNullOrWhiteSpace(bestText))
                throw new InvalidOperationException("Nenhum texto útil foi reconhecido na imagem.");

            return bestText;
        }
        catch (Exception ex)
        {
            AppLogger.Error("OcrImage", "Falha ao processar imagem por OCR.", ex, path);
            throw;
        }
    }

    private static string TryOcrWithTesseractCli(string path)
    {
        var exe = ResolveTesseractExe();
        if (exe == null)
        {
            AppLogger.Warning("OcrCli", "Tesseract CLI não encontrado. Tentando wrapper interno.", path);
            return "";
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process.StartInfo.ArgumentList.Add(path);
            process.StartInfo.ArgumentList.Add("stdout");
            process.StartInfo.ArgumentList.Add("-l");
            process.StartInfo.ArgumentList.Add("por+eng");
            process.StartInfo.ArgumentList.Add("--psm");
            process.StartInfo.ArgumentList.Add("6");
            process.StartInfo.ArgumentList.Add("--oem");
            process.StartInfo.ArgumentList.Add("1");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("preserve_interword_spaces=1");

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(30000))
            {
                try { process.Kill(); } catch { }
                AppLogger.Warning("OcrCli", "Timeout ao executar Tesseract CLI.", path);
                return "";
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                AppLogger.Warning("OcrCli", $"Tesseract CLI retornou código {process.ExitCode} sem texto útil.", path);
                return "";
            }

            return NormalizeOcrTableText(output);
        }
        catch (Exception ex)
        {
            AppLogger.Warning("OcrCli", "Falha ao executar Tesseract CLI.", ex, path);
            return "";
        }
    }

    private static string? ResolveTesseractExe()
    {
        var envCmd = (Environment.GetEnvironmentVariable("TESSERACT_CMD") ?? "").Trim();
        if (!string.IsNullOrEmpty(envCmd) && File.Exists(envCmd))
            return envCmd;

        var bundled = Path.Combine(AppContext.BaseDirectory, "tesseract", "tesseract.exe");
        if (File.Exists(bundled)) return bundled;

        var candidates = new[]
        {
            @"C:\Program Files\Tesseract-OCR\tesseract.exe",
            @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string NormalizeOcrTableText(string text)
    {
        var lines = NormalizeImportedText(text)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var normalized = new List<string>();

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^\s*Lista\s+de\s+", RegexOptions.IgnoreCase))
                continue;

            if (Regex.IsMatch(line, @"Nome\s+Tamanho\s+N[uú]mero", RegexOptions.IgnoreCase))
                continue;

            var clean = Regex.Replace(line, @"\s+v\s*$", "", RegexOptions.IgnoreCase).Trim();
            var columns = Regex.Split(clean, @"\s{2,}")
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToList();

            if (columns.Count >= 3 && Regex.IsMatch(columns[^1], @"^\d+$"))
            {
                var name = string.Join(" ", columns.Take(columns.Count - 2)).Trim();
                var size = columns[^2];
                var number = columns[^1];
                normalized.Add($"{name},{size},{number}");
                continue;
            }

            normalized.Add(clean);
        }

        return string.Join("\n", normalized).Trim();
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

        var ex = new InvalidOperationException(
            "O Tesseract OCR (tessdata) não foi encontrado.\n" +
            "Certifique-se de que a pasta tesseract/tessdata existe junto ao executável.");
        AppLogger.Error("OcrTessData", "Falha ao localizar tessdata do Tesseract.", ex);
        throw ex;
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
        catch (Exception ex)
        {
            AppLogger.Warning("OcrImageVariant", "Falha ao gerar variações da imagem para OCR. Usando imagem original.", ex);
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
