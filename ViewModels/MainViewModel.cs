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
using ListForge.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AppLogger = ListForge.Core.AppLogger;
using CoreHelper = ListForge.Core.SizeHelper;
using CoreProcessor = ListForge.Core.ListProcessor;
using TextSearchHelper = ListForge.Core.TextSearchHelper;
using TrialManager = ListForge.Core.TrialManager;

namespace ListForge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AboutService _aboutService = new();
    private readonly SupportPackageService _supportPackageService = new();
    private readonly ProcessingWorkflowService _processingWorkflowService = new();
    private readonly OutputExportService _outputExportService = new();
    private readonly FileImportService _fileImportService = new();

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
    private string _editorSortLabel = "Original";
    private string _findText = "";
    private string _replaceText = "";
    private bool _findMatchCase;
    private string _currentFileLabel = "Arquivo atual: (nova lista)";
    private string _statusText = ConfigManager.IsTrialBuild
        ? $"Pronto. Trial: {TrialManager.RemainingProcessings}/{TrialManager.Limit} processamento(s) restante(s)."
        : "Pronto.";
    private string _selectedOutputSection = "list";
    private string _selectedSockSize = "";
    private bool _showJsonSection;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_inputText, value)) return;
            _inputText = value;
            Notify();
            ClearValidationHighlights();
        }
    }
    public string OutputText { get => _outputText; set => Set(ref _outputText, value); }
    public string JsonText { get => _jsonText; set => Set(ref _jsonText, value); }
    public string EditorSeparator { get => _editorSeparator; set => Set(ref _editorSeparator, value); }
    public string EditorCaseLabel { get => _editorCaseLabel; set => Set(ref _editorCaseLabel, value); }
    public string EditorSortLabel { get => _editorSortLabel; set => Set(ref _editorSortLabel, value); }
    public string FindText { get => _findText; set { Set(ref _findText, value); ClearSearchHighlight(keepStatus: true); } }
    public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }
    public bool FindMatchCase { get => _findMatchCase; set { Set(ref _findMatchCase, value); ClearSearchHighlight(keepStatus: true); } }
    public string CurrentFileLabel { get => _currentFileLabel; set => Set(ref _currentFileLabel, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string SelectedOutputSection { get => _selectedOutputSection; set => Set(ref _selectedOutputSection, value); }
    public string SelectedSockSize { get => _selectedSockSize; set => Set(ref _selectedSockSize, value); }
    public bool ShowJsonSection { get => _showJsonSection; set => Set(ref _showJsonSection, value); }

    private IReadOnlyList<int> _validationHighlightLines = Array.Empty<int>();
    public IReadOnlyList<int> ValidationHighlightLines
    {
        get => _validationHighlightLines;
        private set => Set(ref _validationHighlightLines, value);
    }

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
    private double _editorFontSize = 13;
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
    public double EditorFontSize
    {
        get => _editorFontSize;
        set
        {
            var clamped = ClampEditorFontSize(value);
            if (Math.Abs(_editorFontSize - clamped) < 0.01) return;
            _editorFontSize = clamped;
            Notify();

            _cfg.EditorFontSize = clamped;
            try { ConfigManager.SaveConfig(_cfg); }
            catch (Exception ex) { AppLogger.Error("EditorFontSize", "Falha ao salvar tamanho da fonte no config.json.", ex, ConfigManager.ConfigPath); }
        }
    }
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
            try { ConfigManager.SaveConfig(_cfg); }
            catch (Exception ex) { AppLogger.Error("Theme", "Falha ao salvar tema no config.json.", ex, ConfigManager.ConfigPath); }
            RequestThemeChange?.Invoke(normalized);
        }
    }
    public string SizeSummary { get => _sizeSummary; set => Set(ref _sizeSummary, value); }
    public bool OutputDirEnabled => !UseDefaultOutputDir;
    public bool DefaultListNameEnabled => !UseDefaultListName;

    // ---------------------------------------------------------------
    // Bound properties — about
    // ---------------------------------------------------------------
    public string AboutProductName => _aboutService.ProductName;
    public string AboutVersion => _aboutService.Version;
    public string AboutEdition => _aboutService.Edition;
    public string AboutLicensedTo => _aboutService.LicensedTo;
    public string AboutAuthor => _aboutService.Author;
    public string AboutContact => _aboutService.Contact;
    public string AboutConfigPath => _aboutService.ConfigPath;
    public string AboutLogsPath => _aboutService.LogsPath;
    public bool AboutIsTrial => _aboutService.IsTrial;
    public string AboutTrialStatus => _aboutService.TrialStatus;
    public string AboutLicenseSummary => _aboutService.LicenseSummary;
    private bool _supportPackageIncludeLogs = true;
    public bool SupportPackageIncludeLogs { get => _supportPackageIncludeLogs; set => Set(ref _supportPackageIncludeLogs, value); }

    // ---------------------------------------------------------------
    // Size group vars (for settings UI)
    // ---------------------------------------------------------------
    public Dictionary<string, SizeGroupBindings> SizeGroupBindings { get; } = new()
    {
        ["male"] = new(),
        ["female"] = new(),
        ["child"] = new(),
        ["sock"] = new(),
    };

    // ---------------------------------------------------------------
    // Collections for ComboBoxes
    // ---------------------------------------------------------------
    public ObservableCollection<string> CaseLabels { get; } = ["Original", "Tudo maiúsculo", "Tudo minúsculo"];
    public ObservableCollection<string> SortLabels { get; } = ["Original", "Crescente", "Decrescente"];
    public ObservableCollection<string> ThemeNames { get; } = ["ListForge Dark", "ListForge Light", "SISBolt"];
    public ObservableCollection<string> SockSizeOptions { get; } = [];

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
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand CopyAboutInfoCommand { get; }
    public ICommand GenerateSupportPackageCommand { get; }
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
        RefreshSockSizeOptions();

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
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        CopyAboutInfoCommand = new RelayCommand(CopyAboutInfo);
        GenerateSupportPackageCommand = new RelayCommand(GenerateSupportPackage);
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

    public void RefreshAboutInfo()
    {
        Notify(nameof(AboutTrialStatus));
    }

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
        EditorFontSize = _cfg.EditorFontSize;
        ShowJsonSection = _cfg.ShowJsonTab;
    }

    private void LoadSizeConfigIntoBindings()
    {
        foreach (var groupKey in CoreHelper.EditableGroupOrder)
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

    private static ListForge.Core.ListSortMode LabelToSortMode(string label) => label switch
    {
        "Crescente" => ListForge.Core.ListSortMode.Ascending,
        "Decrescente" => ListForge.Core.ListSortMode.Descending,
        _ => ListForge.Core.ListSortMode.Original,
    };

    private static string NormalizeThemeName(string? themeName) => themeName switch
    {
        "SISBolt" or "SisBolt Dark" => "SISBolt",
        "ListForge Light" => "ListForge Light",
        _ => "ListForge Dark",
    };

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
        var result = _fileImportService.ImportInputFile(path);

        if (!result.Success || result.Value == null)
        {
            if (result.Exception != null)
                AppLogger.Error("ImportFile", result.TechnicalMessage, result.Exception, path);
            else
                AppLogger.Warning("ImportFile", result.TechnicalMessage, path);

            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var imported = result.Value;
        InputText = imported.Text;

        if (imported.IsPlainText)
        {
            _currentFile = path;
            CurrentFileLabel = $"Arquivo atual: {path}";
            _cfg.LastOpenedFile = path;
            ConfigManager.SaveConfig(_cfg);
        }
        else
        {
            _currentFile = null;
            CurrentFileLabel = $"Importado de: {Path.GetFileName(path)}";
        }

        StatusText = imported.StatusMessage;
        ClearSearchHighlight(keepStatus: true);

        if (!string.IsNullOrWhiteSpace(imported.ReviewMessage))
            MessageBox.Show(imported.ReviewMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveInputFile()
    {
        if (_currentFile == null) { SaveInputAsFile(); return; }

        try
        {
            if (File.Exists(_currentFile))
            {
                var readResult = _fileImportService.ReadTextFile(_currentFile);
                if (!readResult.Success)
                {
                    if (readResult.Exception != null)
                        AppLogger.Error("SaveInputFile", readResult.TechnicalMessage, readResult.Exception, _currentFile);
                    MessageBox.Show(readResult.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (readResult.Value != InputText)
                    ConfigManager.CreateBackup(_currentFile);
            }

            var saveResult = _fileImportService.SaveTextFile(_currentFile, InputText);
            if (!saveResult.Success)
            {
                if (saveResult.Exception != null)
                    AppLogger.Error("SaveInputFile", saveResult.TechnicalMessage, saveResult.Exception, _currentFile);
                MessageBox.Show($"Falha ao salvar.\n\n{saveResult.Exception?.Message ?? saveResult.UserMessage}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cfg.LastOpenedFile = _currentFile;
            ConfigManager.SaveConfig(_cfg);
            StatusText = $"Entrada salva: {Path.GetFileName(_currentFile)}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveInputFile", "Falha ao salvar entrada.", ex, _currentFile);
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

            var result = _fileImportService.SaveTextFile(dlg.FileName, InputText);
            if (!result.Success)
            {
                if (result.Exception != null)
                    AppLogger.Error("SaveInputAsFile", result.TechnicalMessage, result.Exception, dlg.FileName);
                MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentFile = dlg.FileName;
            CurrentFileLabel = $"Arquivo atual: {dlg.FileName}";
            _cfg.LastOpenedFile = dlg.FileName;
            ConfigManager.SaveConfig(_cfg);
            StatusText = $"Entrada salva como: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveInputAsFile", "Falha ao salvar entrada como novo arquivo.", ex, dlg.FileName);
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenBackupsFolder()
    {
        FolderService.OpenFolder(
            ConfigManager.BackupDir,
            "OpenBackupsFolder",
            "Falha ao abrir pasta de backups.",
            message => MessageBox.Show(message, ConfigManager.AppName));
    }

    private void OpenConfigFolder()
    {
        FolderService.OpenFolder(
            ConfigManager.AppDir,
            "OpenConfigFolder",
            "Falha ao abrir pasta de configuração.",
            message => MessageBox.Show(message, ConfigManager.AppName));
    }

    private void OpenLogsFolder()
    {
        FolderService.OpenFolder(
            ConfigManager.LogDir,
            "OpenLogsFolder",
            "Falha ao abrir pasta de logs.",
            message => MessageBox.Show(message, ConfigManager.AppName));
    }

    private void CopyAboutInfo()
    {
        try
        {
            Clipboard.SetText(_aboutService.BuildSupportText());
            AppLogger.Info("About", "Informações da tela Sobre copiadas.");
            StatusText = "Informações do produto copiadas.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("About", "Falha ao copiar informações da tela Sobre.", ex);
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateSupportPackage()
    {
        var warning = SupportPackageIncludeLogs
            ? "Os logs podem conter caminhos de arquivos. Revise o pacote antes de enviar."
            : "O pacote será gerado sem logs recentes. Revise o pacote antes de enviar.";

        if (MessageBox.Show(warning, ConfigManager.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            return;

        var dlg = new OpenFolderDialog { Title = "Escolha onde salvar o pacote de suporte" };
        if (dlg.ShowDialog() != true)
            return;

        var options = new SupportPackageOptions(IncludeLogs: SupportPackageIncludeLogs);
        var result = _supportPackageService.Generate(dlg.FolderName, _aboutService.BuildInfo(), options);
        if (!result.Success || result.Value == null)
        {
            if (result.Exception != null)
                AppLogger.Error("SupportPackage", result.TechnicalMessage, result.Exception);
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var packagePath = result.Value;
        StatusText = $"Pacote de suporte gerado: {Path.GetFileName(packagePath)}";
        MessageBox.Show(
            $"Pacote de suporte gerado com sucesso.\n\n{packagePath}\n\nAntes de enviar, revise o arquivo se houver informações sensíveis.",
            ConfigManager.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
            AppLogger.Warning("ExtractFromLink", "Link rejeitado por formato inválido.");
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
            AppLogger.Error("ExtractFromLink", "Falha ao extrair lista do link.", ex);
            StatusText = $"Erro: {ex.Message}";
            MessageBox.Show($"Falha ao extrair a lista do link.\n\n{ex.Message}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------------------------------------------------------------
    // Processing
    // ---------------------------------------------------------------
    private void ProcessAndPreview()
    {
        try
        {
            var result = _processingWorkflowService.Execute(new ProcessingWorkflowRequest(
                InputText,
                EditorSeparator,
                _sizeCfg,
                LabelToCaseMode(EditorCaseLabel),
                LabelToSortMode(EditorSortLabel)));

            if (result.Status == ProcessingWorkflowStatus.EmptyInput)
            {
                MessageBox.Show("Cole ou abra uma lista na entrada.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (result.Status == ProcessingWorkflowStatus.ValidationFailed)
            {
                var validationIssues = result.ValidationIssues;
                ValidationHighlightLines = validationIssues.Select(issue => issue.LineNumber).ToArray();
                var summary = string.Join("\n", validationIssues
                    .Take(12)
                    .Select(issue => $"Linha {issue.LineNumber}: {issue.Message}"));
                if (validationIssues.Count > 12)
                    summary += $"\n... e mais {validationIssues.Count - 12} linha(s).";

                AppLogger.Warning("ValidateInput", $"Pré-validação encontrou {validationIssues.Count} problema(s).");
                RequestScrollToLine?.Invoke(validationIssues[0].LineNumber);
                StatusText = $"Pré-validação: {validationIssues.Count} problema(s) encontrado(s).";
                MessageBox.Show(summary, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (result.Status == ProcessingWorkflowStatus.TrialLimitReached)
            {
                var message = "Limite de processamentos da versão Trial atingido.";
                AppLogger.Warning("ProcessList", message);
                StatusText = message;
                MessageBox.Show(message, ConfigManager.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (result.Status == ProcessingWorkflowStatus.NoRows)
            {
                AppLogger.Warning("ProcessList", "Processamento não encontrou linhas válidas.");
                MessageBox.Show("Nenhuma linha válida encontrada.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshAboutInfo();

            _rows = result.Rows;
            _lastOrders = result.Orders;
            _lastJson = result.JsonPreview;

            OutputText = result.OutputText;
            JsonText = result.JsonPreview;
            SelectedOutputSection = "list";
            ClearValidationHighlights();

            StatusText = $"Processado: {result.Rows.Count} linha(s) | Ordenação: {EditorSortLabel} | Separador: {CoreProcessor.SeparatorLabel(EditorSeparator)!.Replace("\"", "'")}{TrialManager.StatusSuffix}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessList", "Falha ao processar lista.", ex);
            GotoErrorLine(ex.Message);
            StatusText = $"Erro: {ex.Message}";
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GotoErrorLine(string message)
    {
        var m = System.Text.RegularExpressions.Regex.Match(message, @"[Ll]inha\s+(\d+)");
        if (!m.Success) return;
        var lineNumber = int.Parse(m.Groups[1].Value);
        ValidationHighlightLines = [lineNumber];
        RequestScrollToLine?.Invoke(lineNumber);
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

        var result = _outputExportService.SaveOutputText(OutputText, dir, name);
        if (!result.Success || result.Value == null)
        {
            if (result.Exception != null)
                AppLogger.Error("SaveOutput", result.TechnicalMessage, result.Exception);
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var path = result.Value;
        StatusText = $"Saída salva: {Path.GetFileName(path)}";
        MessageBox.Show($"Saída salva:\n{path}", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void GenerateJson()
    {
        if (_lastOrders.Count == 0) { ProcessAndPreview(); if (_lastOrders.Count == 0) return; }

        var dir = ResolveOutputDir();
        if (dir == null) return;
        var name = ResolveOutputName();
        if (name == null) return;

        var result = _outputExportService.SaveJson(_lastOrders, dir, name);
        if (!result.Success || result.Value == null)
        {
            if (result.Exception != null)
                AppLogger.Error("GenerateJson", result.TechnicalMessage, result.Exception);
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var path = result.Value;
        StatusText = $"JSON gerado: {Path.GetFileName(path)}";
        MessageBox.Show($"JSON gerado:\n{path}\n\nRegistros: {_lastOrders.Count}",
            ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
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
        ClearValidationHighlights();
        StatusText = "Campos limpos.";
    }

    private void ClearValidationHighlights() =>
        ValidationHighlightLines = Array.Empty<int>();

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
            AppLogger.Error("CleanSpaces", "Falha ao limpar espaços da entrada.", ex);
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
        InputText = TextSearchHelper.ReplaceAt(InputText, start, len, ReplaceText);
        BuildSearchMatches();
        _searchCurrentIdx = Math.Min(_searchCurrentIdx, _searchMatches.Count - 1);
        SearchHighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(FindText)) return;
        InputText = TextSearchHelper.ReplaceAll(InputText, FindText, ReplaceText, FindMatchCase);
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
        _searchMatches = TextSearchHelper.FindMatches(InputText, FindText, FindMatchCase);
        _searchCurrentIdx = -1;

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
            AppLogger.Error("SaveSettings", "Falha ao validar configurações de tamanhos.", ex);
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
        _cfg.EditorFontSize = ClampEditorFontSize(EditorFontSize);
        _cfg.LastOpenedFile = _currentFile ?? "";

        try
        {
            ConfigManager.SaveConfig(_cfg);
            ConfigManager.SaveSizeConfig(newSizeCfg);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveSettings", "Falha ao salvar configurações.", ex);
            MessageBox.Show("Falha ao salvar configurações.\n\n" + ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _sizeCfg = newSizeCfg;
        RefreshSizeSummary();
        RefreshSockSizeOptions();

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
        try
        {
            _cfg = ConfigManager.ResetConfig();
            LoadConfigIntoProperties();
            StatusText = "Configurações gerais restauradas para o padrão.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("RestoreDefaultSettings", "Falha ao restaurar configurações gerais.", ex, ConfigManager.ConfigPath);
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static double ClampEditorFontSize(double value) =>
        Math.Clamp(double.IsNaN(value) ? 13 : value, 8, 32);

    private void RestoreDefaultSizes()
    {
        try
        {
            _sizeCfg = ConfigManager.ResetSizeConfig();
            LoadSizeConfigIntoBindings();
            RefreshSizeSummary();
            RefreshSockSizeOptions();
            StatusText = "Tamanhos restaurados para o padrão.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("RestoreDefaultSizes", "Falha ao restaurar tamanhos padrão.", ex, ConfigManager.SizeConfigPath);
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private SizeConfig BuildSizeConfigFromUI()
    {
        var cfg = ConfigManager.LoadSizeConfig();
        foreach (var groupKey in CoreHelper.EditableGroupOrder)
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

    private void RefreshSockSizeOptions()
    {
        var selected = SelectedSockSize;
        SockSizeOptions.Clear();
        SockSizeOptions.Add("");

        var normalized = CoreHelper.Normalize(_sizeCfg);
        if (normalized.Groups.TryGetValue(CoreHelper.GroupSock, out var sockGroup))
        {
            foreach (var size in CoreHelper.BuildGroupSizes(sockGroup))
                SockSizeOptions.Add(size);
        }

        SelectedSockSize = SockSizeOptions.Contains(selected) ? selected : "";
    }
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
