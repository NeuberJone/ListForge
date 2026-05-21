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
        var bundledTesseract = ResolveBundledTesseractExe();
        if (bundledTesseract != null)
            return OcrImageWithBundledCli(path, bundledTesseract);

        string bestText = "";
        int bestScore = -1;

        // Tesseract C# wrapper works on Pix — we try multiple PSM configs
        var psmModes = new[]
        {
            PageSegMode.Auto,
            PageSegMode.SingleColumn,
            PageSegMode.SingleBlock,
            PageSegMode.SingleBlockVertText,
        };

        var tessDataDir = ResolveTessDataDir();
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
                    var text = NormalizeOcrText(page.GetText() ?? "");
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

    private static string OcrImageWithBundledCli(string path, string exe)
    {
        var exeDir = Path.GetDirectoryName(Path.GetFullPath(exe));
        if (string.IsNullOrWhiteSpace(exeDir))
            throw new InvalidOperationException($"Pasta do Tesseract invÃ¡lida: {exe}");

        var tessData = Path.Combine(exeDir, "tessdata");
        if (!Directory.Exists(tessData))
            throw new InvalidOperationException($"Pasta tessdata nÃ£o encontrada: {tessData}");

        var info = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = exeDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add(path);
        info.ArgumentList.Add("stdout");
        info.ArgumentList.Add("--tessdata-dir");
        info.ArgumentList.Add(tessData);
        info.ArgumentList.Add("-l");
        info.ArgumentList.Add("por+eng");
        info.ArgumentList.Add("--psm");
        info.ArgumentList.Add("6");

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("NÃ£o foi possÃ­vel iniciar o Tesseract OCR.");

        var text = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(30000))
        {
            try { process.Kill(); } catch { }
            throw new InvalidOperationException("O Tesseract OCR demorou demais para ler a imagem.");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Falha no Tesseract OCR.\n\n{error.Trim()}");

        var normalized = NormalizeOcrText(text);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Nenhum texto Ãºtil foi reconhecido na imagem.");

        return normalized;
    }

    private static string? ResolveBundledTesseractExe()
    {
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Environment.CurrentDirectory;
        var processDir = Path.GetDirectoryName(Environment.ProcessPath ?? "");

        var candidates = new List<string>();
        AddTesseractCandidate(candidates, baseDir);
        AddTesseractCandidate(candidates, processDir);
        AddTesseractCandidate(candidates, currentDir);

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void AddTesseractCandidate(List<string> candidates, string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        candidates.Add(Path.Combine(root, "tesseract", "tesseract.exe"));
    }

    private static string? GetDirectoryIfFileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        return Path.GetDirectoryName(Path.GetFullPath(path));
    }

    private static string? ResolveTessDataBesideExe(string path)
    {
        var dir = GetDirectoryIfFileExists(path);
        if (string.IsNullOrWhiteSpace(dir)) return null;

        var tessData = Path.Combine(dir, "tessdata");
        return Directory.Exists(tessData) ? tessData : null;
    }

    private static string? ResolveBundledTessDataDir()
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Environment.ProcessPath ?? ""),
            Environment.CurrentDirectory,
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var tessData = Path.Combine(root, "tesseract", "tessdata");
            if (Directory.Exists(tessData)) return tessData;
        }

        return null;
    }

    private static string ResolveTessDataDir()
    {
        // 1. Environment variable override
        var envCmd = (Environment.GetEnvironmentVariable("TESSERACT_CMD") ?? "").Trim();
        var envTessData = ResolveTessDataBesideExe(envCmd);
        if (envTessData != null) return envTessData;

        // 2. Bundled tesseract/ directory beside the exe
        var bundledTessData = ResolveBundledTessDataDir();
        if (bundledTessData != null) return bundledTessData;

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
        var tableRows = lines.Count(IsLikelyOcrTableRow);
        return tableRows * 100 + useful * 10 + lines.Count;
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

    private static string NormalizeOcrText(string text)
    {
        var normalized = NormalizeImportedText(text);
        var tableRows = ExtractOcrTableRows(normalized);
        return tableRows.Count >= 2
            ? string.Join("\n", tableRows)
            : normalized;
    }

    private static List<string> ExtractOcrTableRows(string text)
    {
        var result = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = Regex.Replace(rawLine.Trim(), @"\s+", " ");
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (Regex.IsMatch(line, @"lista\s+de|nome\s+tamanho|tamanho\s+n[uú]mero", RegexOptions.IgnoreCase))
                continue;

            var parsed = TryParseOcrTableRow(line);
            if (parsed != null) result.Add(parsed);
        }
        return result;
    }

    private static bool IsLikelyOcrTableRow(string line) =>
        TryParseOcrTableRow(Regex.Replace(line.Trim(), @"\s+", " ")) != null;

    private static string? TryParseOcrTableRow(string line)
    {
        line = Regex.Replace(line, @"\s+[vV]\s*$", "").Trim();
        const string sizePattern = @"(?:XXGG|XLGG|XGG|GG|XG|PP|BLPP|BLP|BLM|BLG|BLGG|BLXG|P|M|G|[0-9]{1,2}A?)";

        var nameSizeNumber = Regex.Match(
            line,
            $@"^(?<name>.+?)\s+(?<size>{sizePattern})\s+(?<number>\d+)$",
            RegexOptions.IgnoreCase);
        if (nameSizeNumber.Success)
            return BuildCsvRow(nameSizeNumber.Groups["name"].Value, nameSizeNumber.Groups["number"].Value, nameSizeNumber.Groups["size"].Value);

        var nameNumberSize = Regex.Match(
            line,
            $@"^(?<name>.+?)\s+(?<number>\d+)\s+(?<size>{sizePattern})$",
            RegexOptions.IgnoreCase);
        if (nameNumberSize.Success)
            return BuildCsvRow(nameNumberSize.Groups["name"].Value, nameNumberSize.Groups["number"].Value, nameNumberSize.Groups["size"].Value);

        return null;
    }

    private static string BuildCsvRow(string name, string number, string size)
    {
        var cleanName = Regex.Replace(name.Trim(), @"\s+", " ");
        var cleanNumber = number.Trim();
        var cleanSize = size.Trim().ToUpperInvariant();
        return $"{cleanName},{cleanNumber},{cleanSize}";
    }

    private static string NormalizeCell(string? value)
    {
        if (value == null) return "";
        return Regex.Replace(
            value.Replace("\r", " ").Replace("\n", " ").Replace("\x07", " "),
            @"\s+", " ").Trim();
    }
}
