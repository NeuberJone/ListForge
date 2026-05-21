using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ListForge.Config;
using ListForge.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CoreHelper = ListForge.Core.SizeHelper;
using CoreProcessor = ListForge.Core.ListProcessor;
using FileImporter = ListForge.Core.FileImporter;

namespace ListForge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // ---------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------
    private AppConfig _cfg;
    private SizeConfig _sizeCfg;
    private string? _currentFile;
    private List<ParsedRow> _rows = [];
    private List<Dictionary<string, string>> _lastOrders = [];
    private string _lastJson = "";

    // ---------------------------------------------------------------
    // Bound properties — editor
    // ---------------------------------------------------------------
    private string _inputText = "G,JÃO,10\nJOÃO,5,G,M\nMANEL,PP\nJUACA,JUSÉ,PP\n";
    private string _outputText = "";
    private string _jsonText = "";
    private string _editorSeparator = ",";
    private string _editorCaseLabel = "Original";
    private string _findText = "";
    private string _replaceText = "";
    private bool _findMatchCase;
    private string _currentFileLabel = "Arquivo atual: (nova lista)";
    private string _statusText = "Pronto.";
    private string _selectedOutputSection = "list";
    private bool _showJsonSection;

    public string InputText { get => _inputText; set => Set(ref _inputText, value); }
    public string OutputText { get => _outputText; set => Set(ref _outputText, value); }
    public string JsonText { get => _jsonText; set => Set(ref _jsonText, value); }
    public string EditorSeparator { get => _editorSeparator; set => Set(ref _editorSeparator, value); }
    public string EditorCaseLabel { get => _editorCaseLabel; set => Set(ref _editorCaseLabel, value); }
    public string FindText { get => _findText; set { Set(ref _findText, value); ClearSearchHighlight(keepStatus: true); } }
    public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }
    public bool FindMatchCase { get => _findMatchCase; set { Set(ref _findMatchCase, value); ClearSearchHighlight(keepStatus: true); } }
    public string CurrentFileLabel { get => _currentFileLabel; set => Set(ref _currentFileLabel, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string SelectedOutputSection { get => _selectedOutputSection; set => Set(ref _selectedOutputSection, value); }
    public bool ShowJsonSection { get => _showJsonSection; set => Set(ref _showJsonSection, value); }

    // ---------------------------------------------------------------
    // Bound properties — settings
    // ---------------------------------------------------------------
    private bool _showJsonTab;
    private bool _showGenerateJsonButton;
    private bool _showCopyJsonButton;
    private bool _useDefaultOutputDir;
    private string _outputDir = "";
    private bool _useDefaultListName;
    private string _defaultListName = "lista";
    private string _defaultCaseLabel = "Original";
    private string _defaultSeparator = ",";
    private string _themeName = "ListForge Dark";
    private string _sizeSummary = "";

    public bool ShowJsonTab { get => _showJsonTab; set { Set(ref _showJsonTab, value); ShowJsonSection = value; } }
    public bool ShowGenerateJsonButton { get => _showGenerateJsonButton; set => Set(ref _showGenerateJsonButton, value); }
    public bool ShowCopyJsonButton { get => _showCopyJsonButton; set => Set(ref _showCopyJsonButton, value); }
    public bool UseDefaultOutputDir { get => _useDefaultOutputDir; set { Set(ref _useDefaultOutputDir, value); Notify(nameof(OutputDirEnabled)); } }
    public string OutputDir { get => _outputDir; set => Set(ref _outputDir, value); }
    public bool UseDefaultListName { get => _useDefaultListName; set { Set(ref _useDefaultListName, value); Notify(nameof(DefaultListNameEnabled)); } }
    public string DefaultListName { get => _defaultListName; set => Set(ref _defaultListName, value); }
    public string DefaultCaseLabel { get => _defaultCaseLabel; set => Set(ref _defaultCaseLabel, value); }
    public string DefaultSeparator { get => _defaultSeparator; set => Set(ref _defaultSeparator, value); }
    public string ThemeName
    {
        get => _themeName;
        set
        {
            var normalized = NormalizeThemeName(value);
            if (EqualityComparer<string>.Default.Equals(_themeName, normalized)) return;
            _themeName = normalized;
            Notify();

            _cfg.ThemeName = normalized;
            try { ConfigManager.SaveConfig(_cfg); } catch { }
            RequestThemeChange?.Invoke(normalized);
        }
    }
    public string SizeSummary { get => _sizeSummary; set => Set(ref _sizeSummary, value); }
    public bool OutputDirEnabled => !UseDefaultOutputDir;
    public bool DefaultListNameEnabled => !UseDefaultListName;

    // ---------------------------------------------------------------
    // Size group vars (for settings UI)
    // ---------------------------------------------------------------
    public Dictionary<string, SizeGroupBindings> SizeGroupBindings { get; } = new()
    {
        ["male"] = new(),
        ["female"] = new(),
        ["child"] = new(),
    };

    // ---------------------------------------------------------------
    // Collections for ComboBoxes
    // ---------------------------------------------------------------
    public ObservableCollection<string> CaseLabels { get; } = ["Original", "Tudo maiúsculo", "Tudo minúsculo"];
    public ObservableCollection<string> ThemeNames { get; } = ["ListForge Dark", "ListForge Light", "SISBolt"];

    // ---------------------------------------------------------------
    // Commands
    // ---------------------------------------------------------------
    public ICommand OpenInputFileCommand { get; }
    public ICommand SaveInputFileCommand { get; }
    public ICommand SaveInputAsFileCommand { get; }
    public ICommand ExtractFromLinkCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand CopyOutputCommand { get; }
    public ICommand SaveOutputCommand { get; }
    public ICommand CopyJsonCommand { get; }
    public ICommand GenerateJsonCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand OpenBackupsFolderCommand { get; }
    public ICommand CleanSpacesCommand { get; }
    public ICommand ResetSeparatorCommand { get; }
    public ICommand FindNextCommand { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand ReplaceCurrentCommand { get; }
    public ICommand ReplaceAllCommand { get; }
    public ICommand ClearSearchHighlightCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RestoreDefaultSettingsCommand { get; }
    public ICommand RestoreDefaultSizesCommand { get; }
    public ICommand PickOutputFolderCommand { get; }
    public ICommand ApplyThemeCommand { get; }

    // Search state (exposed so View can use it)
    private List<(int start, int length)> _searchMatches = [];
    private int _searchCurrentIdx = -1;
    public List<(int start, int length)> SearchMatches => _searchMatches;
    public int SearchCurrentIdx => _searchCurrentIdx;
    public event EventHandler? SearchHighlightChanged;

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------
    public MainViewModel()
    {
        _cfg = ConfigManager.LoadConfig();
        _sizeCfg = ConfigManager.LoadSizeConfig();
        LoadConfigIntoProperties();
        LoadSizeConfigIntoBindings();
        RefreshSizeSummary();

        OpenInputFileCommand = new RelayCommand(OpenInputFile);
        SaveInputFileCommand = new RelayCommand(SaveInputFile);
        SaveInputAsFileCommand = new RelayCommand(SaveInputAsFile);
        ExtractFromLinkCommand = new RelayCommand(ExtractFromLink);
        ProcessCommand = new RelayCommand(ProcessAndPreview);
        CopyOutputCommand = new RelayCommand(CopyOutput);
        SaveOutputCommand = new RelayCommand(SaveOutput);
        CopyJsonCommand = new RelayCommand(CopyJson);
        GenerateJsonCommand = new RelayCommand(GenerateJson);
        ClearAllCommand = new RelayCommand(ClearAll);
        UndoCommand = new RelayCommand(() => StatusText = "Use Ctrl+Z no editor.");
        OpenBackupsFolderCommand = new RelayCommand(OpenBackupsFolder);
        CleanSpacesCommand = new RelayCommand(CleanSpaces);
        ResetSeparatorCommand = new RelayCommand(() => { EditorSeparator = ","; StatusText = "Separador redefinido para \",\"."; });
        FindNextCommand = new RelayCommand(FindNext);
        FindPreviousCommand = new RelayCommand(FindPrevious);
        ReplaceCurrentCommand = new RelayCommand(ReplaceCurrent);
        ReplaceAllCommand = new RelayCommand(ReplaceAll);
        ClearSearchHighlightCommand = new RelayCommand(() => ClearSearchHighlight());
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        RestoreDefaultSettingsCommand = new RelayCommand(RestoreDefaultSettings);
        RestoreDefaultSizesCommand = new RelayCommand(RestoreDefaultSizes);
        PickOutputFolderCommand = new RelayCommand(PickOutputFolder);
        ApplyThemeCommand = new RelayCommand(() => RequestThemeChange?.Invoke(ThemeName));

        if (!string.IsNullOrEmpty(_cfg.LastOpenedFile))
        {
            _currentFile = _cfg.LastOpenedFile;
            CurrentFileLabel = $"Arquivo atual: {_currentFile}";
        }
    }

    public event Action<string>? RequestThemeChange;

    // ---------------------------------------------------------------
    // Config loading
    // ---------------------------------------------------------------
    private void LoadConfigIntoProperties()
    {
        ShowJsonTab = _cfg.ShowJsonTab;
        ShowGenerateJsonButton = _cfg.ShowGenerateJsonButton;
        ShowCopyJsonButton = _cfg.ShowCopyJsonButton;
        UseDefaultOutputDir = _cfg.UseDefaultOutputDir;
        OutputDir = _cfg.OutputDir;
        UseDefaultListName = _cfg.UseDefaultListName;
        DefaultListName = _cfg.DefaultListName;
        DefaultCaseLabel = CaseModeToLabel(_cfg.DefaultCaseMode);
        DefaultSeparator = _cfg.DefaultInputSeparator;
        ThemeName = NormalizeThemeName(_cfg.ThemeName);
        EditorSeparator = _cfg.DefaultInputSeparator;
        EditorCaseLabel = CaseModeToLabel(_cfg.DefaultCaseMode);
        ShowJsonSection = _cfg.ShowJsonTab;
    }

    private void LoadSizeConfigIntoBindings()
    {
        foreach (var groupKey in new[] { "male", "female", "child" })
        {
            if (!_sizeCfg.Groups.TryGetValue(groupKey, out var group)) continue;
            var b = SizeGroupBindings[groupKey];
            b.BaseSizes = CoreHelper.TokensToCsv(group.BaseSizes);
            b.Prefixes = CoreHelper.TokensToCsv(group.Prefixes);
            b.Suffixes = CoreHelper.TokensToCsv(group.Suffixes);
        }
    }

    // ---------------------------------------------------------------
    // Case mode helpers
    // ---------------------------------------------------------------
    private static string CaseModeToLabel(string mode) => mode switch
    {
        "upper" => "Tudo maiúsculo",
        "lower" => "Tudo minúsculo",
        _ => "Original",
    };

    private static string LabelToCaseMode(string label) => label switch
    {
        "Tudo maiúsculo" => "upper",
        "Tudo minúsculo" => "lower",
        _ => "original",
    };

    private static string NormalizeThemeName(string? themeName) => themeName switch
    {
        "SISBolt" or "SisBolt Dark" => "SISBolt",
        "ListForge Light" => "ListForge Light",
        _ => "ListForge Dark",
    };

    private static string FriendlyError(Exception ex) =>
        ex.GetBaseException().Message;

    // ---------------------------------------------------------------
    // File operations
    // ---------------------------------------------------------------
    private void OpenInputFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "ListForge — Abrir lista",
            Filter =
                "Arquivos compatíveis|*.txt;*.csv;*.list;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.xlsm;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|" +
                "Texto|*.txt;*.csv;*.list|PDF|*.pdf|Word|*.doc;*.docx|Excel|*.xls;*.xlsx;*.xlsm|" +
                "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|Todos|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        var path = dlg.FileName;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            if (FileImporter.TextExtensions.Contains(ext))
            {
                InputText = FileImporter.ReadTextFile(path);
                _currentFile = path;
                CurrentFileLabel = $"Arquivo atual: {path}";
                _cfg.LastOpenedFile = path;
                ConfigManager.SaveConfig(_cfg);
                StatusText = $"Lista carregada: {Path.GetFileName(path)}";
                ClearSearchHighlight(keepStatus: true);
                return;
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
                warning = "Texto extraído da imagem via OCR.\n\nConfira o conteúdo — OCR não é 100% confiável.";
            }
            else
            {
                MessageBox.Show("Formato não suportado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var normalized = FileImporter.NormalizeImportedText(imported);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Não foi possível obter conteúdo útil desse arquivo.");

            InputText = normalized;
            _currentFile = null;
            CurrentFileLabel = $"Importado de: {Path.GetFileName(path)}";
            StatusText = $"Conteúdo importado: {Path.GetFileName(path)}";
            ClearSearchHighlight(keepStatus: true);
            MessageBox.Show(warning, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FriendlyError(ex), ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveInputFile()
    {
        if (_currentFile == null) { SaveInputAsFile(); return; }

        try
        {
            if (File.Exists(_currentFile))
            {
                var onDisk = FileImporter.ReadTextFile(_currentFile);
                if (onDisk != InputText)
                    ConfigManager.CreateBackup(_currentFile);
            }
            FileImporter.WriteTextFile(_currentFile, InputText);
            _cfg.LastOpenedFile = _currentFile;
            ConfigManager.SaveConfig(_cfg);
            StatusText = $"Entrada salva: {Path.GetFileName(_currentFile)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao salvar.\n\n{ex.Message}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveInputAsFile()
    {
        var dlg = new SaveFileDialog
        {
            Title = "ListForge — Salvar entrada como",
            DefaultExt = ".txt",
            Filter = "Arquivos de texto|*.txt|CSV|*.csv|Todos|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (File.Exists(dlg.FileName)) ConfigManager.CreateBackup(dlg.FileName);
            FileImporter.WriteTextFile(dlg.FileName, InputText);
            _currentFile = dlg.FileName;
            CurrentFileLabel = $"Arquivo atual: {dlg.FileName}";
            _cfg.LastOpenedFile = dlg.FileName;
            ConfigManager.SaveConfig(_cfg);
            StatusText = $"Entrada salva como: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenBackupsFolder()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", ConfigManager.BackupDir); }
        catch (Exception ex) { MessageBox.Show(ex.Message, ConfigManager.AppName); }
    }

    // ---------------------------------------------------------------
    // Extract from URL
    // ---------------------------------------------------------------
    private async void ExtractFromLink()
    {
        var url = ListForge.UI.Views.InputDialog.Show(
            "Cole o link do JSON para extrair a lista:", ConfigManager.AppName);
        if (string.IsNullOrWhiteSpace(url)) return;

        url = url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("O link precisa começar com http:// ou https://.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", $"{ConfigManager.AppName}/1.0");
            var json = await client.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<JToken>(json)
                ?? throw new InvalidOperationException("JSON inválido.");

            var extracted = CoreProcessor.ExtractListTextFromJsonData(data);
            if (string.IsNullOrWhiteSpace(extracted))
                throw new InvalidOperationException("Nenhuma linha foi extraída do link.");

            InputText = extracted;
            _currentFile = null;
            CurrentFileLabel = "Arquivo atual: (lista extraída do link)";
            StatusText = "Lista extraída do link.";
            ClearSearchHighlight(keepStatus: true);
            ProcessAndPreview();
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
            MessageBox.Show($"Falha ao extrair a lista do link.\n\n{ex.Message}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------------------------------------------------------------
    // Processing
    // ---------------------------------------------------------------
    private void ProcessAndPreview()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            MessageBox.Show("Cole ou abra uma lista na entrada.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var rows = CoreProcessor.ProcessText(InputText, EditorSeparator, _sizeCfg);
            if (rows.Count == 0)
            {
                MessageBox.Show("Nenhuma linha válida encontrada.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var caseMode = LabelToCaseMode(EditorCaseLabel);
            var organized = CoreProcessor.BuildOutput(rows, _sizeCfg, caseMode);
            var orders = CoreProcessor.BuildOrdersFromOrderlist(rows, _sizeCfg, caseMode);
            var preview = CoreProcessor.BuildJsonPreview(orders);

            _rows = rows;
            _lastOrders = orders;
            _lastJson = preview;

            OutputText = organized;
            JsonText = preview;
            SelectedOutputSection = "list";

            StatusText = $"Processado: {rows.Count} linha(s) | Separador: {CoreProcessor.SeparatorLabel(EditorSeparator)!.Replace("\"", "'")}";
        }
        catch (Exception ex)
        {
            GotoErrorLine(ex.Message);
            var message = FriendlyError(ex);
            StatusText = $"Erro: {message}";
            MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GotoErrorLine(string message)
    {
        var m = System.Text.RegularExpressions.Regex.Match(message, @"[Ll]inha\s+(\d+)");
        if (!m.Success) return;
        RequestScrollToLine?.Invoke(int.Parse(m.Groups[1].Value));
    }

    public event Action<int>? RequestScrollToLine;

    // ---------------------------------------------------------------
    // Output actions
    // ---------------------------------------------------------------
    private void CopyOutput()
    {
        if (string.IsNullOrWhiteSpace(OutputText))
        {
            MessageBox.Show("Não há saída para copiar.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Clipboard.SetText(OutputText);
        StatusText = "Saída organizada copiada.";
    }

    private void CopyJson()
    {
        if (string.IsNullOrWhiteSpace(_lastJson))
        {
            MessageBox.Show("Ainda não há prévia do JSON. Clique em Processar.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Clipboard.SetText(_lastJson);
        StatusText = "JSON copiado.";
    }

    private void SaveOutput()
    {
        if (string.IsNullOrWhiteSpace(OutputText)) ProcessAndPreview();
        if (string.IsNullOrWhiteSpace(OutputText)) return;

        var dir = ResolveOutputDir();
        if (dir == null) return;
        var name = ResolveOutputName();
        if (name == null) return;

        try
        {
            var path = CoreProcessor.ExportOutputText(OutputText, dir, name);
            StatusText = $"Saída salva: {Path.GetFileName(path)}";
            MessageBox.Show($"Saída salva:\n{path}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateJson()
    {
        if (_lastOrders.Count == 0) { ProcessAndPreview(); if (_lastOrders.Count == 0) return; }

        var dir = ResolveOutputDir();
        if (dir == null) return;
        var name = ResolveOutputName();
        if (name == null) return;

        try
        {
            var path = CoreProcessor.ExportJson(_lastOrders, dir, name);
            StatusText = $"JSON gerado: {Path.GetFileName(path)}";
            MessageBox.Show($"JSON gerado:\n{path}\n\nRegistros: {_lastOrders.Count}",
                ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? ResolveOutputDir()
    {
        if (UseDefaultOutputDir)
        {
            if (string.IsNullOrWhiteSpace(OutputDir))
            {
                MessageBox.Show("Defina uma pasta padrão de saída nas configurações.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            Directory.CreateDirectory(OutputDir);
            return OutputDir;
        }

        var dlg = new OpenFolderDialog { Title = "Escolha a pasta de saída" };
        if (dlg.ShowDialog() != true) return null;
        return dlg.FolderName;
    }

    private string? ResolveOutputName()
    {
        var suggested = string.IsNullOrWhiteSpace(_currentFile)
            ? (string.IsNullOrWhiteSpace(DefaultListName) ? "lista" : DefaultListName)
            : CoreProcessor.SanitizeBaseFilename(Path.GetFileNameWithoutExtension(_currentFile));

        if (UseDefaultListName)
        {
            var n = CoreProcessor.SanitizeBaseFilename(DefaultListName);
            if (string.IsNullOrEmpty(n))
            {
                MessageBox.Show("Defina um nome padrão válido.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return n;
        }

        var typed = ListForge.UI.Views.InputDialog.Show(
            "Informe o nome da lista/arquivo:", ConfigManager.AppName, suggested);
        if (typed == null) return null;

        var base_ = CoreProcessor.SanitizeBaseFilename(typed);
        if (string.IsNullOrEmpty(base_))
        {
            MessageBox.Show("Nome inválido.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        return base_;
    }

    private void PickOutputFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Escolha a pasta padrão de saída" };
        if (dlg.ShowDialog() == true)
            OutputDir = dlg.FolderName;
    }

    // ---------------------------------------------------------------
    // Clean / Clear
    // ---------------------------------------------------------------
    private void ClearAll()
    {
        InputText = "";
        OutputText = "";
        JsonText = "";
        _rows = [];
        _lastOrders = [];
        _lastJson = "";
        _currentFile = null;
        CurrentFileLabel = "Arquivo atual: (nova lista)";
        ClearSearchHighlight(keepStatus: true);
        StatusText = "Campos limpos.";
    }

    private void CleanSpaces()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            MessageBox.Show("Não há texto para limpar.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            InputText = CoreProcessor.CleanTextBySeparator(InputText, EditorSeparator);
            StatusText = $"Espaços removidos usando separador {CoreProcessor.SeparatorLabel(EditorSeparator)!.Replace("\"", "'")}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------------------------------------------------------------
    // Search (state only — actual text highlighting is done in View)
    // ---------------------------------------------------------------
    public void FindNext()
    {
        BuildSearchMatches();
        if (_searchMatches.Count == 0) return;
        _searchCurrentIdx = (_searchCurrentIdx + 1) % _searchMatches.Count;
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindPrevious()
    {
        BuildSearchMatches();
        if (_searchMatches.Count == 0) return;
        _searchCurrentIdx = (_searchCurrentIdx - 1 + _searchMatches.Count) % _searchMatches.Count;
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceCurrent()
    {
        BuildSearchMatches();
        if (_searchMatches.Count == 0) return;
        if (_searchCurrentIdx < 0) _searchCurrentIdx = 0;

        var (start, len) = _searchMatches[_searchCurrentIdx];
        InputText = InputText[..start] + ReplaceText + InputText[(start + len)..];
        BuildSearchMatches();
        _searchCurrentIdx = Math.Min(_searchCurrentIdx, _searchMatches.Count - 1);
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(FindText)) return;
        var comp = FindMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        InputText = InputText.Replace(FindText, ReplaceText, comp);
        BuildSearchMatches();
        StatusText = "Substituição concluída.";
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSearchHighlight(bool keepStatus = false)
    {
        _searchMatches = [];
        _searchCurrentIdx = -1;
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
        if (!keepStatus) StatusText = "Destaque da busca removido.";
    }

    private void BuildSearchMatches()
    {
        _searchMatches = [];
        _searchCurrentIdx = -1;

        var term = FindText ?? "";
        if (string.IsNullOrEmpty(term)) return;

        var text = InputText ?? "";
        var comp = FindMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var idx = 0;

        while (true)
        {
            var pos = text.IndexOf(term, idx, comp);
            if (pos < 0) break;
            _searchMatches.Add((pos, term.Length));
            idx = pos + term.Length;
        }

        if (_searchMatches.Count > 0) _searchCurrentIdx = 0;
    }

    // ---------------------------------------------------------------
    // Settings
    // ---------------------------------------------------------------
    private void SaveSettings()
    {
        if (UseDefaultOutputDir && string.IsNullOrWhiteSpace(OutputDir))
        {
            MessageBox.Show("Informe uma pasta padrão de saída.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (UseDefaultListName && string.IsNullOrWhiteSpace(DefaultListName))
        {
            MessageBox.Show("Informe um nome padrão válido.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SizeConfig newSizeCfg;
        try { newSizeCfg = BuildSizeConfigFromUI(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var oldTheme = _cfg.ThemeName;

        _cfg.ShowJsonTab = ShowJsonTab;
        _cfg.ShowGenerateJsonButton = ShowGenerateJsonButton;
        _cfg.ShowCopyJsonButton = ShowCopyJsonButton;
        _cfg.UseDefaultOutputDir = UseDefaultOutputDir;
        _cfg.OutputDir = OutputDir.Trim();
        _cfg.UseDefaultListName = UseDefaultListName;
        _cfg.DefaultListName = DefaultListName.Trim();
        _cfg.DefaultCaseMode = LabelToCaseMode(DefaultCaseLabel);
        _cfg.DefaultInputSeparator = string.IsNullOrWhiteSpace(DefaultSeparator) ? "," : DefaultSeparator.Trim();
        _cfg.ThemeName = ThemeName;
        _cfg.LastOpenedFile = _currentFile ?? "";

        ConfigManager.SaveConfig(_cfg);
        ConfigManager.SaveSizeConfig(newSizeCfg);
        _sizeCfg = newSizeCfg;
        RefreshSizeSummary();

        EditorCaseLabel = CaseModeToLabel(_cfg.DefaultCaseMode);
        EditorSeparator = _cfg.DefaultInputSeparator;
        ShowJsonSection = ShowJsonTab;

        // Always apply theme (handles both first-time change and re-save)
        RequestThemeChange?.Invoke(ThemeName);

        StatusText = "Configurações salvas.";
        MessageBox.Show("Configurações salvas com sucesso.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreDefaultSettings()
    {
        _cfg = ConfigManager.ResetConfig();
        LoadConfigIntoProperties();
        StatusText = "Configurações gerais restauradas para o padrão.";
    }

    private void RestoreDefaultSizes()
    {
        _sizeCfg = ConfigManager.ResetSizeConfig();
        LoadSizeConfigIntoBindings();
        RefreshSizeSummary();
        StatusText = "Tamanhos restaurados para o padrão.";
    }

    private SizeConfig BuildSizeConfigFromUI()
    {
        var cfg = ConfigManager.LoadSizeConfig();
        foreach (var groupKey in new[] { "male", "female", "child" })
        {
            var b = SizeGroupBindings[groupKey];
            var bases = CoreHelper.ParseCsvTokens(b.BaseSizes);
            var prefixes = CoreHelper.ParseCsvTokens(b.Prefixes);
            var suffixes = CoreHelper.ParseCsvTokens(b.Suffixes);

            if (bases.Count == 0)
                throw new ArgumentException($"Informe ao menos um tamanho-base para {CoreHelper.GroupLabels[groupKey]}.");

            cfg = CoreHelper.UpdateGroupConfig(cfg, groupKey, bases, prefixes, suffixes);
        }
        return cfg;
    }

    private void RefreshSizeSummary() =>
        SizeSummary = CoreHelper.BuildSizeSummary(_sizeCfg);
}

// ---------------------------------------------------------------
// Helper class for size group UI bindings
// ---------------------------------------------------------------
public class SizeGroupBindings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(f, v)) return;
        f = v; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private string _baseSizes = "";
    private string _prefixes = "";
    private string _suffixes = "";

    public string BaseSizes { get => _baseSizes; set => Set(ref _baseSizes, value); }
    public string Prefixes { get => _prefixes; set => Set(ref _prefixes, value); }
    public string Suffixes { get => _suffixes; set => Set(ref _suffixes, value); }
}
