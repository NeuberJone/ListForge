using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AppLogger = ListForge.Core.AppLogger;
using CoreHelper = ListForge.Core.SizeHelper;
using CoreProcessor = ListForge.Core.ListProcessor;
using TextSearchHelper = ListForge.Core.TextSearchHelper;

namespace ListForge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private const string AdvancedSaveLooseFilesLabel = "Arquivos soltos";
    private const string AdvancedSaveZipLabel = "Arquivo ZIP";

    private readonly ILicenseService _licenseService = new LocalTrialLicenseService();
    private readonly AboutService _aboutService;
    private readonly SupportPackageService _supportPackageService = new();
    private readonly ProcessingWorkflowService _processingWorkflowService;
    private readonly OutputExportService _outputExportService = new();
    private readonly AdvancedSaveService _advancedSaveService = new();
    private readonly SettingsExportService _settingsExportService = new();
    private readonly FileImportService _fileImportService = new();
    private readonly LinkListImportService _linkListImportService = new();
    private readonly JsonPieceMappingService _jsonPieceMappingService = new();
    private readonly WorkProfileService _workProfileService = new();
    private readonly ProcessingPreviewService _processingPreviewService;
    private readonly ProcessingHistoryService _processingHistoryService = new();
    private readonly ListComparisonService _listComparisonService = new();
    private readonly DistributionInfoService _distributionInfoService = new();
    private readonly IUpdateDiscoveryService _githubUpdateService;
    private readonly IUpdatePackageService _updateInstallerService;
    private bool _isLoadingConfig;
    private bool _isRefreshingAdvancedJsonPieceSlots;
    private bool _isApplyingWorkProfile;
    private bool _isProcessingBusy;
    private bool _isComparisonBusy;
    private DistributionInfo _distributionInfo;
    private CancellationTokenSource? _updateCancellation;
    private DateTimeOffset? _lastManualUpdateCheckUtc;
    private DateTimeOffset? _lastUpdateCheckSessionUtc;
    private PreparedUpdatePackage? _preparedUpdatePackage;
    private bool _lastUpdateFailureWasDownload;

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
    private string _lastValidOutputText = "";
    private string _lastValidJsonText = "";
    private bool _isUpdatingGeneratedText;
    private WorkProfile? _selectedWorkProfile;
    private ProcessingHistoryEntry? _selectedHistoryEntry;
    private bool _hasUnsavedWorkProfileChanges;
    private string _currentSourceType = ProcessingHistorySourceTypes.PastedText;
    private ComparisonSnapshot? _comparisonSnapshot;
    private long _inputRevision;
    private long _comparisonInputRevision = -1;

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
    private string _statusText = "Pronto.";
    private string _selectedOutputSection = "list";
    private string _selectedSockSize = "";
    private bool _showJsonSection;
    private bool _allowOutputEditing;
    private bool _hasPendingOutputEdit;
    private bool _hasPendingJsonEdit;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_inputText, value)) return;
            _inputText = value;
            _inputRevision++;
            Notify();
            ClearValidationHighlights();
            RefreshAdvancedJsonPieceSlots();
            NotifyComparisonState();
        }
    }
    public string OutputText
    {
        get => _outputText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_outputText, value)) return;
            _outputText = value;
            Notify();
            if (!_isUpdatingGeneratedText && AllowOutputEditing)
            {
                HasPendingOutputEdit = !string.Equals(_outputText, _lastValidOutputText, StringComparison.Ordinal);
                if (HasPendingOutputEdit)
                    HasPendingJsonEdit = false;
            }
            NotifyComparisonState();
        }
    }
    public string JsonText
    {
        get => _jsonText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_jsonText, value)) return;
            _jsonText = value;
            Notify();
            if (!_isUpdatingGeneratedText && AllowOutputEditing)
            {
                HasPendingJsonEdit = !string.Equals(_jsonText, _lastValidJsonText, StringComparison.Ordinal);
                if (HasPendingJsonEdit)
                    HasPendingOutputEdit = false;
            }
            NotifyComparisonState();
        }
    }
    public string EditorSeparator
    {
        get => _editorSeparator;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_editorSeparator, value)) return;
            _editorSeparator = value;
            Notify();
            RefreshAdvancedJsonPieceSlots();
            MarkWorkProfileChanged();
        }
    }
    public string EditorCaseLabel { get => _editorCaseLabel; set { Set(ref _editorCaseLabel, value); MarkWorkProfileChanged(); } }
    public string EditorSortLabel { get => _editorSortLabel; set { Set(ref _editorSortLabel, value); MarkWorkProfileChanged(); } }
    public string FindText { get => _findText; set { Set(ref _findText, value); ClearSearchHighlight(keepStatus: true); } }
    public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }
    public bool FindMatchCase { get => _findMatchCase; set { Set(ref _findMatchCase, value); ClearSearchHighlight(keepStatus: true); } }
    public string CurrentFileLabel { get => _currentFileLabel; set => Set(ref _currentFileLabel, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string SelectedOutputSection { get => _selectedOutputSection; set => Set(ref _selectedOutputSection, value); }
    public string SelectedSockSize { get => _selectedSockSize; set => Set(ref _selectedSockSize, value); }
    public bool AllowOutputEditing
    {
        get => _allowOutputEditing;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_allowOutputEditing, value)) return;

            if (!value && HasPendingOutputOrJsonEdit)
            {
                var result = MessageBox.Show(
                    "Existem alterações na saída ou no JSON. Clique em Sim para aplicar, Não para descartar ou Cancelar para continuar editando.",
                    ConfigManager.AppName,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    Notify();
                    return;
                }

                if (result == MessageBoxResult.Yes && !ApplyOutputEdits())
                {
                    Notify();
                    return;
                }

                if (result == MessageBoxResult.No)
                    DiscardOutputEdits();
            }

            _allowOutputEditing = value;
            Notify();
            Notify(nameof(IsGeneratedOutputReadOnly));
            Notify(nameof(CanApplyOutputEdits));
            Notify(nameof(CanDiscardOutputEdits));
            NotifyComparisonState();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool IsGeneratedOutputReadOnly => !AllowOutputEditing;
    public bool HasPendingOutputEdit
    {
        get => _hasPendingOutputEdit;
        private set
        {
            Set(ref _hasPendingOutputEdit, value);
            Notify(nameof(HasPendingOutputOrJsonEdit));
            Notify(nameof(CanApplyOutputEdits));
            Notify(nameof(CanDiscardOutputEdits));
            NotifyComparisonState();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool HasPendingJsonEdit
    {
        get => _hasPendingJsonEdit;
        private set
        {
            Set(ref _hasPendingJsonEdit, value);
            Notify(nameof(HasPendingOutputOrJsonEdit));
            Notify(nameof(CanApplyOutputEdits));
            Notify(nameof(CanDiscardOutputEdits));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool HasPendingOutputOrJsonEdit => HasPendingOutputEdit || HasPendingJsonEdit;
    public bool CanApplyOutputEdits => AllowOutputEditing && HasPendingOutputOrJsonEdit;
    public bool CanDiscardOutputEdits => AllowOutputEditing && HasPendingOutputOrJsonEdit;
    public bool ShowJsonSection
    {
        get => _showJsonSection;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_showJsonSection, value)) return;
            _showJsonSection = value;
            Notify();
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
        }
    }

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
    private bool _useAdvancedJsonPieceMapping;
    private bool _advancedListEnabled;
    private bool _useDefaultOutputDir;
    private string _outputDir = "";
    private bool _useDefaultListName;
    private string _defaultListName = "lista";
    private string _defaultCaseLabel = "Original";
    private string _defaultSeparator = ",";
    private string _themeName = "ListForge Dark";
    private string _advancedSaveModeLabel = AdvancedSaveLooseFilesLabel;
    private double _editorFontSize = 13;
    private bool _forgeModeEnabled;
    private bool _forgeAnvilEnabled = true;
    private bool _forgeHeatEnabled = true;
    private bool _forgeSparksEnabled = true;
    private bool _forgeImpactEnabled = true;
    private int _forgeEffectPulse;
    private string _forgeStatusText = "Forja em repouso.";
    private bool _forgeIsHot;
    private bool _checkUpdatesOnStartup = true;
    private bool _isUpdateBusy;
    private bool _isExtractingFromLink;
    private double _updateDownloadProgress;
    private long _updateDownloadedBytes;
    private long _updateTotalBytes;
    private UpdateStatus _updateStatus = UpdateStatus.Idle;
    private string _updateStatusText = "Nenhuma verificação foi realizada nesta sessão.";
    private UpdateReleaseInfo? _availableUpdateRelease;
    private string _sizeSummary = "";

    public bool ShowJsonTab
    {
        get => _showJsonTab;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_showJsonTab, value)) return;
            _showJsonTab = value;
            Notify();
            ShowJsonSection = value;
            Notify(nameof(HasJsonFeaturesEnabled));
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
        }
    }
    public bool ShowGenerateJsonButton
    {
        get => _showGenerateJsonButton;
        set
        {
            Set(ref _showGenerateJsonButton, value);
            Notify(nameof(HasJsonFeaturesEnabled));
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
        }
    }
    public bool ShowCopyJsonButton
    {
        get => _showCopyJsonButton;
        set
        {
            Set(ref _showCopyJsonButton, value);
            Notify(nameof(HasJsonFeaturesEnabled));
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
        }
    }
    public bool UseAdvancedJsonPieceMapping
    {
        get => _useAdvancedJsonPieceMapping;
        set
        {
            Set(ref _useAdvancedJsonPieceMapping, value);
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
        }
    }
    public bool AdvancedListEnabled
    {
        get => _advancedListEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_advancedListEnabled, value)) return;
            _advancedListEnabled = value;
            Notify();

            ShowJsonTab = value;
            ShowGenerateJsonButton = value;
            ShowCopyJsonButton = value;
            UseAdvancedJsonPieceMapping = value;
            Notify(nameof(HasJsonFeaturesEnabled));
            Notify(nameof(ShowAdvancedEditorOptions));
            Notify(nameof(ShowAdvancedSaveButton));
            Notify(nameof(ShowAdvancedJsonOptions));
            Notify(nameof(AdvancedJsonPieceSlotsEnabled));
            if (!_isLoadingConfig)
                SaveAdvancedListSettings();
            MarkWorkProfileChanged();
        }
    }
    public bool UseDefaultOutputDir { get => _useDefaultOutputDir; set { Set(ref _useDefaultOutputDir, value); Notify(nameof(OutputDirEnabled)); MarkWorkProfileChanged(); } }
    public string OutputDir { get => _outputDir; set { Set(ref _outputDir, value); MarkWorkProfileChanged(); } }
    public bool UseDefaultListName { get => _useDefaultListName; set { Set(ref _useDefaultListName, value); Notify(nameof(DefaultListNameEnabled)); MarkWorkProfileChanged(); } }
    public string DefaultListName { get => _defaultListName; set { Set(ref _defaultListName, value); MarkWorkProfileChanged(); } }
    public string DefaultCaseLabel { get => _defaultCaseLabel; set { Set(ref _defaultCaseLabel, value); MarkWorkProfileChanged(); } }
    public string DefaultSeparator { get => _defaultSeparator; set { Set(ref _defaultSeparator, value); MarkWorkProfileChanged(); } }
    public string AdvancedSaveModeLabel { get => _advancedSaveModeLabel; set { Set(ref _advancedSaveModeLabel, value); MarkWorkProfileChanged(); } }
    public bool CheckUpdatesOnStartup
    {
        get => _checkUpdatesOnStartup;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_checkUpdatesOnStartup, value)) return;
            _checkUpdatesOnStartup = value;
            Notify();

            _cfg.CheckUpdatesOnStartup = value;
            try { ConfigManager.SaveConfig(_cfg); }
            catch (Exception ex) { AppLogger.Error("Update", "Falha ao salvar preferência de atualização no config.json.", ex, ConfigManager.ConfigPath); }
        }
    }
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
    public bool ForgeModeEnabled
    {
        get => _forgeModeEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_forgeModeEnabled, value)) return;
            _forgeModeEnabled = value;
            Notify();
            Notify(nameof(ProcessButtonText));
            Notify(nameof(QuickProcessButtonText));
            NotifyForgeState();
            SaveForgeSettings();
        }
    }
    public bool ForgeAnvilEnabled
    {
        get => _forgeAnvilEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_forgeAnvilEnabled, value)) return;
            _forgeAnvilEnabled = value;
            Notify();
            NotifyForgeState();
            SaveForgeSettings();
        }
    }
    public bool ForgeHeatEnabled
    {
        get => _forgeHeatEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_forgeHeatEnabled, value)) return;
            _forgeHeatEnabled = value;
            Notify();
            NotifyForgeState();
            SaveForgeSettings();
        }
    }
    public bool ForgeSparksEnabled
    {
        get => _forgeSparksEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_forgeSparksEnabled, value)) return;
            _forgeSparksEnabled = value;
            Notify();
            NotifyForgeState();
            SaveForgeSettings();
        }
    }
    public bool ForgeImpactEnabled
    {
        get => _forgeImpactEnabled;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_forgeImpactEnabled, value)) return;
            _forgeImpactEnabled = value;
            Notify();
            NotifyForgeState();
            SaveForgeSettings();
        }
    }
    public bool ShowForgePanel => ForgeModeEnabled && ForgeAnvilEnabled;
    public bool ShowForgeHeat => ForgeModeEnabled && ForgeHeatEnabled;
    public bool ShowForgeSparks => ForgeModeEnabled && ForgeSparksEnabled;
    public bool ShowForgeImpact => ForgeModeEnabled && ForgeImpactEnabled;
    public int ForgeEffectPulse { get => _forgeEffectPulse; private set => Set(ref _forgeEffectPulse, value); }
    public string ForgeStatusText { get => _forgeStatusText; private set => Set(ref _forgeStatusText, value); }
    public string ForgeTemperatureText => ForgeModeEnabled
        ? (_forgeIsHot ? "Temperatura: aço vivo" : "Temperatura: brasa baixa")
        : "Modo Forja desativado";
    public string ProcessButtonText => ForgeModeEnabled ? "Forjar" : "Processar";
    public string QuickProcessButtonText => ForgeModeEnabled ? "Forja expressa" : "Processar rápido";
    public bool IsProcessingActionEnabled => !IsProcessingBusy;
    public bool IsProcessingBusy
    {
        get => _isProcessingBusy;
        private set
        {
            if (EqualityComparer<bool>.Default.Equals(_isProcessingBusy, value)) return;
            _isProcessingBusy = value;
            Notify();
            Notify(nameof(IsProcessingActionEnabled));
            NotifyComparisonState();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool IsComparisonBusy
    {
        get => _isComparisonBusy;
        private set
        {
            if (EqualityComparer<bool>.Default.Equals(_isComparisonBusy, value)) return;
            _isComparisonBusy = value;
            Notify();
            NotifyComparisonState();
        }
    }
    public bool CanCompareInputOutput =>
        !IsProcessingBusy
        && !IsComparisonBusy
        && IsComparisonSnapshotCurrent(_comparisonSnapshot);
    private bool IsComparisonSnapshotCurrent(ComparisonSnapshot? snapshot) =>
        snapshot != null
        && !HasPendingOutputOrJsonEdit
        && _comparisonInputRevision == _inputRevision
        && !string.IsNullOrWhiteSpace(snapshot.InputText)
        && !string.IsNullOrWhiteSpace(snapshot.OutputText)
        && string.Equals(InputText, snapshot.InputText, StringComparison.Ordinal)
        && string.Equals(_lastValidOutputText, snapshot.OutputText, StringComparison.Ordinal);
    public string ComparisonActionTooltip => CanCompareInputOutput
        ? "Compara semanticamente a entrada processada com a saída organizada."
        : _comparisonSnapshot != null
          && (_comparisonInputRevision != _inputRevision
              || !string.Equals(InputText, _comparisonSnapshot.InputText, StringComparison.Ordinal)
              || !string.Equals(_lastValidOutputText, _comparisonSnapshot.OutputText, StringComparison.Ordinal)
              || HasPendingOutputOrJsonEdit)
            ? "A entrada ou a saída foi alterada. Gere uma nova comparação."
            : "Gere uma saída para comparar com a entrada original.";
    public string SizeSummary { get => _sizeSummary; set => Set(ref _sizeSummary, value); }
    public bool OutputDirEnabled => !UseDefaultOutputDir;
    public bool DefaultListNameEnabled => !UseDefaultListName;
    public bool HasJsonFeaturesEnabled => AdvancedListEnabled;
    public bool ShowAdvancedEditorOptions => AdvancedListEnabled;
    public bool IsExtractFromLinkEnabled => !IsExtractingFromLink;
    public bool ShowAdvancedJsonOptions => AdvancedListEnabled && ShowJsonSection;
    public bool AdvancedJsonPieceSlotsEnabled => ShowAdvancedJsonOptions;
    public bool ShowAdvancedSaveButton => ShowAdvancedEditorOptions;
    public string InstalledVersion => _aboutService.Version;
    public string DistributionDisplayName => _distributionInfo.DisplayName;
    public UpdateStatus CurrentUpdateStatus
    {
        get => _updateStatus;
        private set
        {
            Set(ref _updateStatus, value);
            NotifyUpdateStateProperties();
        }
    }
    public bool IsUpdateBusy
    {
        get => _isUpdateBusy;
        private set
        {
            Set(ref _isUpdateBusy, value);
            Notify(nameof(IsUpdateProgressVisible));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool IsExtractingFromLink
    {
        get => _isExtractingFromLink;
        private set
        {
            Set(ref _isExtractingFromLink, value);
            Notify(nameof(IsExtractFromLinkEnabled));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool IsUpdateProgressVisible => CurrentUpdateStatus == UpdateStatus.Downloading;
    public bool IsCheckingUpdateVisible => CurrentUpdateStatus == UpdateStatus.Checking;
    public bool HasAvailableUpdate => AvailableUpdateRelease != null;
    public bool HasDownloadableUpdate => AvailableUpdateRelease?.GetAssetFor(_distributionInfo.Kind) != null;
    public bool IsCheckUpdateActionVisible => CurrentUpdateStatus is not UpdateStatus.Checking
        and not UpdateStatus.Downloading
        and not UpdateStatus.Installing;
    public bool IsDownloadUpdateActionVisible => HasDownloadableUpdate
        && (CurrentUpdateStatus is UpdateStatus.UpdateAvailable or UpdateStatus.Failed);
    public bool IsCancelUpdateActionVisible => CurrentUpdateStatus == UpdateStatus.Downloading;
    public bool IsInstallUpdateActionVisible => _preparedUpdatePackage != null
        && _distributionInfo.CanRunInstallerUpdate
        && CurrentUpdateStatus == UpdateStatus.ReadyToInstall;
    public bool IsOpenUpdateFolderActionVisible => _preparedUpdatePackage != null
        && !_distributionInfo.CanRunInstallerUpdate
        && CurrentUpdateStatus == UpdateStatus.Downloaded;
    public bool IsReleasePageActionVisible => HasAvailableUpdate;
    public string CheckUpdatesActionText => (CurrentUpdateStatus is UpdateStatus.Offline or UpdateStatus.Failed)
        && !_lastUpdateFailureWasDownload
            ? "Tentar novamente"
            : "Verificar atualizações";
    public string DownloadUpdateActionText => CurrentUpdateStatus == UpdateStatus.Failed && _lastUpdateFailureWasDownload
        ? "Tentar baixar novamente"
        : "Baixar atualização";
    public string AvailableUpdateVersionText => AvailableUpdateRelease == null
        ? "Não identificada"
        : GitHubUpdateService.ToThreePartVersion(AvailableUpdateRelease.Version);
    public string AvailableUpdateNotesText => AvailableUpdateRelease == null
        ? ""
        : SummarizeReleaseNotes(AvailableUpdateRelease.Notes);
    public string UpdateSourceText => AvailableUpdateRelease?.Source switch
    {
        UpdateSource.ConfiguredManifest => "Manifesto configurado",
        UpdateSource.OfficialManifest => "Servidor oficial",
        UpdateSource.GitHub => "GitHub Releases (fonte alternativa)",
        _ => "Não identificada",
    };
    public string LastUpdateCheckSessionText => _lastUpdateCheckSessionUtc.HasValue
        ? _lastUpdateCheckSessionUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
        : "Nenhuma nesta sessão";
    public string UpdateDownloadDetails => _updateTotalBytes > 0
        ? $"{FormatBytes(_updateDownloadedBytes)} de {FormatBytes(_updateTotalBytes)} ({UpdateDownloadProgress:0}%)"
        : CurrentUpdateStatus == UpdateStatus.Downloading ? "Preparando download..." : "";
    public UpdateReleaseInfo? AvailableUpdateRelease
    {
        get => _availableUpdateRelease;
        private set
        {
            Set(ref _availableUpdateRelease, value);
            Notify(nameof(HasAvailableUpdate));
            Notify(nameof(HasDownloadableUpdate));
            Notify(nameof(AvailableUpdateVersionText));
            Notify(nameof(AvailableUpdateNotesText));
            Notify(nameof(UpdateSourceText));
            NotifyUpdateStateProperties();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public double UpdateDownloadProgress
    {
        get => _updateDownloadProgress;
        private set
        {
            Set(ref _updateDownloadProgress, Math.Clamp(value, 0, 100));
            Notify(nameof(IsUpdateProgressVisible));
            Notify(nameof(UpdateDownloadDetails));
        }
    }
    public string UpdateStatusText { get => _updateStatusText; private set => Set(ref _updateStatusText, value); }
    public ObservableCollection<WorkProfile> WorkProfiles { get; } = [];
    public WorkProfile? SelectedWorkProfile
    {
        get => _selectedWorkProfile;
        set
        {
            if (ReferenceEquals(_selectedWorkProfile, value))
                return;

            ChangeSelectedWorkProfile(value);
        }
    }
    public bool HasUnsavedWorkProfileChanges
    {
        get => _hasUnsavedWorkProfileChanges;
        private set
        {
            if (EqualityComparer<bool>.Default.Equals(_hasUnsavedWorkProfileChanges, value)) return;
            _hasUnsavedWorkProfileChanges = value;
            Notify();
            Notify(nameof(WorkProfileChangeStatus));
            Notify(nameof(ActiveWorkProfileDisplayName));
            RefreshWorkProfileDisplayNames();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public string ActiveWorkProfileDisplayName => SelectedWorkProfile == null
        ? "Perfil de trabalho"
        : HasUnsavedWorkProfileChanges ? $"{SelectedWorkProfile.Name}*" : SelectedWorkProfile.Name;
    public string WorkProfileChangeStatus => HasUnsavedWorkProfileChanges
        ? "Alterações não salvas no perfil"
        : "Perfil salvo";
    public bool CanManageSelectedWorkProfile => SelectedWorkProfile != null;
    public bool CanRenameSelectedWorkProfile => SelectedWorkProfile != null && !SelectedWorkProfile.IsDefault;
    public bool CanDeleteSelectedWorkProfile => SelectedWorkProfile != null && !SelectedWorkProfile.IsDefault;

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
    public bool SupportPackageIncludeLogs => true;

    // ---------------------------------------------------------------
    // Bound properties — history
    // ---------------------------------------------------------------
    public ObservableCollection<ProcessingHistoryEntry> ProcessingHistoryEntries { get; } = [];
    public ProcessingHistoryEntry? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            if (ReferenceEquals(_selectedHistoryEntry, value)) return;
            _selectedHistoryEntry = value;
            Notify();
            Notify(nameof(CanOpenSelectedHistoryOutput));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool HasProcessingHistoryEntries => ProcessingHistoryEntries.Count > 0;
    public bool ShowEmptyProcessingHistory => !HasProcessingHistoryEntries;
    public bool CanOpenSelectedHistoryOutput => SelectedHistoryEntry != null;
    public string ProcessingHistoryPath => Path.Combine(ConfigManager.AppDir, "processing-history.json");

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
    public ObservableCollection<string> AdvancedSaveModeLabels { get; } = [AdvancedSaveLooseFilesLabel, AdvancedSaveZipLabel];
    public ObservableCollection<string> SockSizeOptions { get; } = [];
    public ObservableCollection<PieceTypeOption> JsonPieceTypeOptions { get; } =
    [
        new("", "Selecionar"),
        .. PieceTypeMapper.AvailableOptions,
    ];
    public ObservableCollection<AdvancedJsonPieceSlot> AdvancedJsonPieceSlots { get; } = [];

    // ---------------------------------------------------------------
    // Commands
    // ---------------------------------------------------------------
    public ICommand OpenInputFileCommand { get; }
    public ICommand SaveInputFileCommand { get; }
    public ICommand SaveInputAsFileCommand { get; }
    public ICommand ExtractFromLinkCommand { get; }
    public ICommand ExtractNewListFromLinkCommand { get; }
    public ICommand AppendFromLinkCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand QuickProcessCommand { get; }
    public ICommand CompareInputOutputCommand { get; }
    public ICommand CopyOutputCommand { get; }
    public ICommand SaveOutputCommand { get; }
    public ICommand AdvancedSaveCommand { get; }
    public ICommand ApplyOutputEditsCommand { get; }
    public ICommand DiscardOutputEditsCommand { get; }
    public ICommand CopyJsonCommand { get; }
    public ICommand GenerateJsonCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand OpenBackupsFolderCommand { get; }
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand CopyAboutInfoCommand { get; }
    public ICommand GenerateSupportPackageCommand { get; }
    public ICommand OpenSelectedHistoryOutputCommand { get; }
    public ICommand ClearProcessingHistoryCommand { get; }
    public ICommand ExportSettingsCommand { get; }
    public ICommand ImportSettingsCommand { get; }
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
    public ICommand CheckUpdatesCommand { get; }
    public ICommand DownloadAvailableUpdateCommand { get; }
    public ICommand CancelUpdateDownloadCommand { get; }
    public ICommand InstallAvailableUpdateCommand { get; }
    public ICommand OpenDownloadedUpdateFolderCommand { get; }
    public ICommand OpenUpdateReleasePageCommand { get; }
    public ICommand CreateWorkProfileCommand { get; }
    public ICommand SaveWorkProfileCommand { get; }
    public ICommand DiscardWorkProfileChangesCommand { get; }
    public ICommand RenameWorkProfileCommand { get; }
    public ICommand DuplicateWorkProfileCommand { get; }
    public ICommand DeleteWorkProfileCommand { get; }
    public ICommand RestoreDefaultWorkProfileCommand { get; }

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
        : this(null, null, null)
    {
    }

    internal MainViewModel(
        IUpdateDiscoveryService? updateDiscoveryService,
        IUpdatePackageService? updatePackageService,
        DistributionInfo? distributionInfo)
    {
        _githubUpdateService = updateDiscoveryService ?? new GitHubUpdateService();
        _updateInstallerService = updatePackageService ?? new UpdateInstallerService();
        _aboutService = new AboutService(_licenseService);
        _processingWorkflowService = new ProcessingWorkflowService(_licenseService);
        _processingPreviewService = new ProcessingPreviewService(_processingWorkflowService);
        _distributionInfo = distributionInfo ?? _distributionInfoService.GetCurrentDistribution();
        _cfg = ConfigManager.LoadConfig();
        _sizeCfg = ConfigManager.LoadSizeConfig();
        EnsureWorkProfiles(saveIfChanged: true);
        LoadConfigIntoProperties();
        LoadSizeConfigIntoBindings();
        RefreshWorkProfiles();
        ApplyActiveWorkProfileOnStartup();
        RefreshSizeSummary();
        RefreshSockSizeOptions();

        OpenInputFileCommand = new RelayCommand(OpenInputFile);
        SaveInputFileCommand = new RelayCommand(SaveInputFile);
        SaveInputAsFileCommand = new RelayCommand(SaveInputAsFile);
        ExtractFromLinkCommand = new AsyncRelayCommand(() => ExtractFromLinkAsync(ExtractedListDestination.NewList), () => !IsExtractingFromLink);
        ExtractNewListFromLinkCommand = new AsyncRelayCommand(() => ExtractFromLinkAsync(ExtractedListDestination.NewList), () => !IsExtractingFromLink);
        AppendFromLinkCommand = new AsyncRelayCommand(() => ExtractFromLinkAsync(ExtractedListDestination.CurrentList), () => !IsExtractingFromLink);
        ProcessCommand = new RelayCommand(AnalyzeAndProcessWithPreview, () => !IsProcessingBusy);
        QuickProcessCommand = new RelayCommand(() => ProcessAndPreview(), () => !IsProcessingBusy);
        CompareInputOutputCommand = new AsyncRelayCommand(CompareInputOutputAsync, () => CanCompareInputOutput);
        CopyOutputCommand = new RelayCommand(CopyOutput);
        SaveOutputCommand = new RelayCommand(SaveOutput);
        AdvancedSaveCommand = new RelayCommand(AdvancedSave);
        ApplyOutputEditsCommand = new RelayCommand(() => ApplyOutputEdits(), () => CanApplyOutputEdits);
        DiscardOutputEditsCommand = new RelayCommand(DiscardOutputEdits, () => CanDiscardOutputEdits);
        CopyJsonCommand = new RelayCommand(CopyJson);
        GenerateJsonCommand = new RelayCommand(GenerateJson);
        ClearAllCommand = new RelayCommand(ClearAll);
        UndoCommand = new RelayCommand(() => StatusText = "Use Ctrl+Z no editor.");
        OpenBackupsFolderCommand = new RelayCommand(OpenBackupsFolder);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        CopyAboutInfoCommand = new RelayCommand(CopyAboutInfo);
        GenerateSupportPackageCommand = new RelayCommand(GenerateSupportPackage);
        OpenSelectedHistoryOutputCommand = new RelayCommand(OpenSelectedHistoryOutput, () => CanOpenSelectedHistoryOutput);
        ClearProcessingHistoryCommand = new RelayCommand(ClearProcessingHistory, () => HasProcessingHistoryEntries);
        ExportSettingsCommand = new RelayCommand(ExportSettings);
        ImportSettingsCommand = new RelayCommand(ImportSettings);
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
        CheckUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesAsync(isAutomatic: false), () => !IsUpdateBusy);
        DownloadAvailableUpdateCommand = new AsyncRelayCommand(DownloadAvailableUpdateAsync, () => !IsUpdateBusy && HasDownloadableUpdate);
        CancelUpdateDownloadCommand = new RelayCommand(CancelUpdateDownload, () => CurrentUpdateStatus == UpdateStatus.Downloading);
        InstallAvailableUpdateCommand = new AsyncRelayCommand(InstallPreparedUpdateAsync, () => IsInstallUpdateActionVisible);
        OpenDownloadedUpdateFolderCommand = new RelayCommand(OpenDownloadedUpdateFolder, () => IsOpenUpdateFolderActionVisible);
        OpenUpdateReleasePageCommand = new RelayCommand(OpenUpdateReleasePage, () => IsReleasePageActionVisible);
        CreateWorkProfileCommand = new RelayCommand(CreateWorkProfile);
        SaveWorkProfileCommand = new RelayCommand(SaveActiveWorkProfile, () => SelectedWorkProfile != null && HasUnsavedWorkProfileChanges);
        DiscardWorkProfileChangesCommand = new RelayCommand(DiscardWorkProfileChanges, () => SelectedWorkProfile != null && HasUnsavedWorkProfileChanges);
        RenameWorkProfileCommand = new RelayCommand(RenameWorkProfile, () => CanRenameSelectedWorkProfile);
        DuplicateWorkProfileCommand = new RelayCommand(DuplicateWorkProfile, () => CanManageSelectedWorkProfile);
        DeleteWorkProfileCommand = new RelayCommand(DeleteWorkProfile, () => CanDeleteSelectedWorkProfile);
        RestoreDefaultWorkProfileCommand = new RelayCommand(RestoreDefaultWorkProfile);
        StatusText = _licenseService.IsTrial
            ? $"Pronto. Trial: {_licenseService.RemainingProcessings}/{_licenseService.ProcessingLimit} processamento(s) restante(s)."
            : "Pronto.";
        LoadProcessingHistory();

    }

    public event Action<string>? RequestThemeChange;
    public event Action? RequestShutdown;
    public event Action<ComparisonViewModel>? RequestComparison;

    public void RefreshAboutInfo()
    {
        Notify(nameof(AboutTrialStatus));
    }

    public Task CheckForUpdatesOnStartupAsync()
    {
        if (!CheckUpdatesOnStartup || _distributionInfo.IsDevelopment)
            return Task.CompletedTask;

        if (!UpdateCheckPolicy.ShouldRunAutomaticCheck(_cfg.LastUpdateCheckUtc, DateTimeOffset.UtcNow))
        {
            if (HasAvailableUpdate)
            {
                CurrentUpdateStatus = UpdateStatus.UpdateAvailable;
                UpdateStatusText = BuildStoredUpdateStatus(AvailableUpdateRelease!);
            }
            else
            {
                CurrentUpdateStatus = UpdateStatus.UpToDate;
                UpdateStatusText = "O ListForge está atualizado.";
            }
            return Task.CompletedTask;
        }

        return CheckForUpdatesAsync(isAutomatic: true);
    }

    internal async Task CheckForUpdatesAsync(bool isAutomatic)
    {
        if (IsUpdateBusy)
            return;

        var checkStartedUtc = DateTimeOffset.UtcNow;
        if (!isAutomatic &&
            !UpdateCheckPolicy.ShouldRunManualCheck(_lastManualUpdateCheckUtc, checkStartedUtc))
        {
            UpdateStatusText = "Aguarde um minuto antes de verificar novamente.";
            return;
        }

        if (!isAutomatic)
            _lastManualUpdateCheckUtc = checkStartedUtc;

        _cfg.LastUpdateCheckUtc = checkStartedUtc;
        try
        {
            ConfigManager.SaveConfig(_cfg);
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "Update",
                "Falha ao salvar o horário da verificação de atualizações.",
                ex,
                ConfigManager.ConfigPath);
        }

        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        IsUpdateBusy = true;
        ResetUpdateProgress();
        CurrentUpdateStatus = UpdateStatus.Checking;
        UpdateStatusText = "Verificando atualizações...";
        _lastUpdateFailureWasDownload = false;
        AppLogger.Info("Update", isAutomatic
            ? "Verificação automática iniciada."
            : "Verificação manual iniciada.");

        try
        {
            var currentVersion = ResolveCurrentVersion();
            var checkResult = await _githubUpdateService
                .CheckForUpdatesAsync(currentVersion, _distributionInfo, _updateCancellation.Token)
                .ConfigureAwait(true);
            _lastUpdateCheckSessionUtc = DateTimeOffset.UtcNow;
            Notify(nameof(LastUpdateCheckSessionText));

            if (!checkResult.Success || checkResult.Value == null)
            {
                LogUpdateFailure(checkResult.TechnicalMessage, checkResult.Exception);
                UpdateStatusText = isAutomatic
                    ? "Não foi possível verificar atualizações automaticamente."
                    : "Não foi possível verificar se existe uma nova versão. Tente novamente.";
                CurrentUpdateStatus = string.Equals(checkResult.ErrorCode, "AllUpdateSourcesFailed", StringComparison.Ordinal)
                    ? UpdateStatus.Offline
                    : UpdateStatus.Failed;
                return;
            }

            var info = checkResult.Value;
            SaveUpdateCheckInfo(info);
            _preparedUpdatePackage = null;

            if (info.Availability != UpdateAvailability.UpdateAvailable || info.Release == null)
            {
                AvailableUpdateRelease = null;
                UpdateStatusText = info.UserMessage;
                CurrentUpdateStatus = UpdateStatus.UpToDate;
                return;
            }

            AvailableUpdateRelease = info.Release;
            CurrentUpdateStatus = UpdateStatus.UpdateAvailable;
            UpdateStatusText = BuildStoredUpdateStatus(info.Release);
        }
        catch (OperationCanceledException)
        {
            CurrentUpdateStatus = HasAvailableUpdate ? UpdateStatus.UpdateAvailable : UpdateStatus.Idle;
            UpdateStatusText = "Verificação de atualização cancelada.";
        }
        finally
        {
            IsUpdateBusy = false;
            _updateCancellation.Dispose();
            _updateCancellation = null;
        }
    }

    internal async Task DownloadAvailableUpdateAsync()
    {
        if (AvailableUpdateRelease == null || IsUpdateBusy)
            return;

        _updateCancellation?.Dispose();
        _updateCancellation = new CancellationTokenSource();
        IsUpdateBusy = true;
        ResetUpdateProgress();
        CurrentUpdateStatus = UpdateStatus.Downloading;
        UpdateStatusText = "Baixando atualização...";
        _lastUpdateFailureWasDownload = false;

        try
        {
            var progress = new Progress<UpdateDownloadProgressInfo>(UpdateDownloadProgressState);
            var downloadResult = await _updateInstallerService
                .DownloadAndValidateAsync(
                    AvailableUpdateRelease,
                    _distributionInfo,
                    progress,
                    _updateCancellation.Token)
                .ConfigureAwait(true);

            if (!downloadResult.Success || downloadResult.Value == null)
            {
                LogUpdateFailure(downloadResult.TechnicalMessage, downloadResult.Exception);
                if (string.Equals(downloadResult.ErrorCode, "DownloadCanceled", StringComparison.Ordinal))
                {
                    CurrentUpdateStatus = UpdateStatus.UpdateAvailable;
                    UpdateStatusText = "Download cancelado. A atualização continua disponível.";
                    return;
                }

                _lastUpdateFailureWasDownload = true;
                CurrentUpdateStatus = UpdateStatus.Failed;
                UpdateStatusText = downloadResult.UserMessage;
                return;
            }

            _preparedUpdatePackage = downloadResult.Value;
            CurrentUpdateStatus = _distributionInfo.CanRunInstallerUpdate
                ? UpdateStatus.ReadyToInstall
                : UpdateStatus.Downloaded;
            UpdateStatusText = _distributionInfo.CanRunInstallerUpdate
                ? "Atualização baixada e validada. Pronta para instalação."
                : "Atualização baixada e validada. Abra a pasta para substituir esta edição manualmente.";
        }
        finally
        {
            IsUpdateBusy = false;
            _updateCancellation.Dispose();
            _updateCancellation = null;
        }
    }

    internal async Task InstallPreparedUpdateAsync()
    {
        if (_preparedUpdatePackage == null || IsUpdateBusy || !_distributionInfo.CanRunInstallerUpdate)
            return;

        IsUpdateBusy = true;
        CurrentUpdateStatus = UpdateStatus.Installing;
        UpdateStatusText = "Validando e iniciando o instalador...";
        var startResult = await Task.Run(() => _updateInstallerService.StartInstaller(_preparedUpdatePackage));
        if (!startResult.Success)
        {
            LogUpdateFailure(startResult.TechnicalMessage, startResult.Exception);
            CurrentUpdateStatus = UpdateStatus.ReadyToInstall;
            UpdateStatusText = startResult.UserMessage;
            IsUpdateBusy = false;
            return;
        }

        CurrentUpdateStatus = UpdateStatus.Installing;
        UpdateStatusText = "Instalador iniciado. O ListForge será fechado.";
        IsUpdateBusy = false;
        RequestShutdown?.Invoke();
    }

    private void OpenDownloadedUpdateFolder()
    {
        if (_preparedUpdatePackage == null)
            return;

        var result = _updateInstallerService.OpenDownloadFolder(_preparedUpdatePackage);
        if (!result.Success)
        {
            LogUpdateFailure(result.TechnicalMessage, result.Exception);
            UpdateStatusText = result.UserMessage;
        }
    }

    private void OpenUpdateReleasePage()
    {
        if (AvailableUpdateRelease == null)
            return;

        var result = _updateInstallerService.OpenReleasePage(AvailableUpdateRelease);
        if (!result.Success)
        {
            LogUpdateFailure(result.TechnicalMessage, result.Exception);
            UpdateStatusText = result.UserMessage;
        }
    }

    private string BuildStoredUpdateStatus(UpdateReleaseInfo release)
    {
        var newVersion = GitHubUpdateService.ToThreePartVersion(release.Version);
        var sourceMessage = release.Source == UpdateSource.GitHub
            ? " O servidor principal não respondeu; a verificação foi concluída por uma fonte alternativa."
            : "";
        var asset = release.GetAssetFor(_distributionInfo.Kind);
        var actionMessage = asset != null
            ? " Use Baixar atualização para continuar."
            : _distributionInfo.IsTrial
                ? " A atualização Trial deve ser baixada manualmente pela página da versão."
                : " A edição portátil deve ser substituída manualmente pela página da versão.";
        return $"Uma nova versão está disponível: {newVersion}.{actionMessage}{sourceMessage}";
    }

    private void SaveUpdateCheckInfo(UpdateCheckInfo info)
    {
        _cfg.LastUpdateAvailability = info.Availability.ToString();

        if (info.Availability == UpdateAvailability.UpdateAvailable && info.Release != null)
            SaveAvailableUpdateRelease(info.Release);
        else
            ClearAvailableUpdateCache();

        try
        {
            ConfigManager.SaveConfig(_cfg);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Update", "Falha ao salvar resultado da verificação de atualizações.", ex, ConfigManager.ConfigPath);
        }
    }

    private void SaveAvailableUpdateRelease(UpdateReleaseInfo release)
    {
        _cfg.LastAvailableUpdateVersion = GitHubUpdateService.ToThreePartVersion(release.Version);
        _cfg.LastAvailableUpdateTagName = release.TagName;
        _cfg.LastAvailableUpdateReleaseUrl = release.HtmlUrl;
        _cfg.LastAvailableUpdateNotes = release.Notes;
        _cfg.LastAvailableUpdateInstallerName = release.InstallerAsset.Name;
        _cfg.LastAvailableUpdateInstallerUrl = release.InstallerAsset.DownloadUrl;
        _cfg.LastAvailableUpdateInstallerSizeBytes = release.InstallerAsset.SizeBytes;
        _cfg.LastAvailableUpdateInstallerSha256 = release.InstallerAsset.Sha256 ?? "";
        _cfg.LastAvailableUpdateChecksumsName = release.ChecksumsAsset?.Name ?? "";
        _cfg.LastAvailableUpdateChecksumsUrl = release.ChecksumsAsset?.DownloadUrl ?? "";
        _cfg.LastAvailableUpdateChecksumsSizeBytes = release.ChecksumsAsset?.SizeBytes ?? 0;
        _cfg.LastAvailableUpdateChecksumsSha256 = release.ChecksumsAsset?.Sha256 ?? "";
        SaveCachedAsset(release.PortableAsset, isTrial: false);
        SaveCachedAsset(release.TrialAsset, isTrial: true);
        _cfg.LastAvailableUpdateSource = release.Source.ToString();
    }

    private void SaveCachedAsset(UpdateAssetInfo? asset, bool isTrial)
    {
        if (isTrial)
        {
            _cfg.LastAvailableUpdateTrialName = asset?.Name ?? "";
            _cfg.LastAvailableUpdateTrialUrl = asset?.DownloadUrl ?? "";
            _cfg.LastAvailableUpdateTrialSizeBytes = asset?.SizeBytes ?? 0;
            _cfg.LastAvailableUpdateTrialSha256 = asset?.Sha256 ?? "";
            return;
        }

        _cfg.LastAvailableUpdatePortableName = asset?.Name ?? "";
        _cfg.LastAvailableUpdatePortableUrl = asset?.DownloadUrl ?? "";
        _cfg.LastAvailableUpdatePortableSizeBytes = asset?.SizeBytes ?? 0;
        _cfg.LastAvailableUpdatePortableSha256 = asset?.Sha256 ?? "";
    }

    private void ClearAvailableUpdateCache()
    {
        _cfg.LastAvailableUpdateVersion = "";
        _cfg.LastAvailableUpdateTagName = "";
        _cfg.LastAvailableUpdateReleaseUrl = "";
        _cfg.LastAvailableUpdateNotes = "";
        _cfg.LastAvailableUpdateInstallerName = "";
        _cfg.LastAvailableUpdateInstallerUrl = "";
        _cfg.LastAvailableUpdateInstallerSizeBytes = 0;
        _cfg.LastAvailableUpdateInstallerSha256 = "";
        _cfg.LastAvailableUpdateChecksumsName = "";
        _cfg.LastAvailableUpdateChecksumsUrl = "";
        _cfg.LastAvailableUpdateChecksumsSizeBytes = 0;
        _cfg.LastAvailableUpdateChecksumsSha256 = "";
        SaveCachedAsset(null, isTrial: false);
        SaveCachedAsset(null, isTrial: true);
        _cfg.LastAvailableUpdateSource = "";
    }

    private void RestoreCachedUpdateState()
    {
        if (string.Equals(_cfg.LastUpdateAvailability, nameof(UpdateAvailability.UpdateAvailable), StringComparison.OrdinalIgnoreCase)
            && TryCreateCachedAvailableUpdate(out var release)
            && IsNewerThanInstalled(release.Version))
        {
            AvailableUpdateRelease = release;
            CurrentUpdateStatus = UpdateStatus.UpdateAvailable;
            UpdateStatusText = BuildStoredUpdateStatus(release);
            return;
        }

        if (string.Equals(_cfg.LastUpdateAvailability, nameof(UpdateAvailability.UpToDate), StringComparison.OrdinalIgnoreCase)
            || string.Equals(_cfg.LastUpdateAvailability, nameof(UpdateAvailability.RemoteOlder), StringComparison.OrdinalIgnoreCase))
        {
            AvailableUpdateRelease = null;
            CurrentUpdateStatus = UpdateStatus.UpToDate;
            UpdateStatusText = "O ListForge está atualizado.";
        }
    }

    private bool TryCreateCachedAvailableUpdate(out UpdateReleaseInfo release)
    {
        release = null!;
        if (!GitHubUpdateService.TryParseReleaseVersion(_cfg.LastAvailableUpdateVersion, out var version)
            || string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateInstallerName)
            || string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateInstallerUrl))
        {
            return false;
        }

        var installer = new UpdateAssetInfo(
            _cfg.LastAvailableUpdateInstallerName,
            _cfg.LastAvailableUpdateInstallerUrl,
            _cfg.LastAvailableUpdateInstallerSizeBytes,
            string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateInstallerSha256) ? null : _cfg.LastAvailableUpdateInstallerSha256);

        UpdateAssetInfo? checksums = null;
        if (!string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateChecksumsName)
            && !string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateChecksumsUrl))
        {
            checksums = new UpdateAssetInfo(
                _cfg.LastAvailableUpdateChecksumsName,
                _cfg.LastAvailableUpdateChecksumsUrl,
                _cfg.LastAvailableUpdateChecksumsSizeBytes,
                string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateChecksumsSha256) ? null : _cfg.LastAvailableUpdateChecksumsSha256);
        }

        release = new UpdateReleaseInfo(
            version,
            string.IsNullOrWhiteSpace(_cfg.LastAvailableUpdateTagName) ? $"v{GitHubUpdateService.ToThreePartVersion(version)}" : _cfg.LastAvailableUpdateTagName,
            _cfg.LastAvailableUpdateReleaseUrl,
            _cfg.LastAvailableUpdateNotes,
            installer,
            checksums,
            CreateCachedAsset(
                _cfg.LastAvailableUpdatePortableName,
                _cfg.LastAvailableUpdatePortableUrl,
                _cfg.LastAvailableUpdatePortableSizeBytes,
                _cfg.LastAvailableUpdatePortableSha256),
            CreateCachedAsset(
                _cfg.LastAvailableUpdateTrialName,
                _cfg.LastAvailableUpdateTrialUrl,
                _cfg.LastAvailableUpdateTrialSizeBytes,
                _cfg.LastAvailableUpdateTrialSha256),
            Enum.TryParse<UpdateSource>(_cfg.LastAvailableUpdateSource, ignoreCase: true, out var source)
                ? source
                : UpdateSource.OfficialManifest);
        return true;
    }

    private static UpdateAssetInfo? CreateCachedAsset(string name, string url, long size, string sha256)
    {
        return string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || size <= 0
            ? null
            : new UpdateAssetInfo(name, url, size, string.IsNullOrWhiteSpace(sha256) ? null : sha256);
    }

    private bool IsNewerThanInstalled(Version version) =>
        NormalizeVersion(version).CompareTo(NormalizeVersion(ResolveCurrentVersion())) > 0;

    private static Version NormalizeVersion(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));

    private void CancelUpdateDownload()
    {
        _updateCancellation?.Cancel();
        UpdateStatusText = "Cancelando atualização...";
    }

    private void UpdateDownloadProgressState(UpdateDownloadProgressInfo progress)
    {
        _updateDownloadedBytes = Math.Max(0, progress.DownloadedBytes);
        _updateTotalBytes = Math.Max(0, progress.TotalBytes);
        UpdateDownloadProgress = progress.Percentage;
        UpdateStatusText = $"Baixando atualização: {UpdateDownloadProgress:0}%.";
        Notify(nameof(UpdateDownloadDetails));
    }

    private void ResetUpdateProgress()
    {
        _updateDownloadedBytes = 0;
        _updateTotalBytes = 0;
        UpdateDownloadProgress = 0;
        Notify(nameof(UpdateDownloadDetails));
    }

    private void NotifyUpdateStateProperties()
    {
        Notify(nameof(IsUpdateProgressVisible));
        Notify(nameof(IsCheckingUpdateVisible));
        Notify(nameof(IsCheckUpdateActionVisible));
        Notify(nameof(IsDownloadUpdateActionVisible));
        Notify(nameof(IsCancelUpdateActionVisible));
        Notify(nameof(IsInstallUpdateActionVisible));
        Notify(nameof(IsOpenUpdateFolderActionVisible));
        Notify(nameof(IsReleasePageActionVisible));
        Notify(nameof(CheckUpdatesActionText));
        Notify(nameof(DownloadUpdateActionText));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string SummarizeReleaseNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "Sem resumo disponível.";

        var lines = notes
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimStart('#', '-', '*', ' '))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(5);
        var summary = string.Join(" ", lines);
        return summary.Length <= 500 ? summary : summary[..500] + "...";
    }

    private Version ResolveCurrentVersion()
    {
        return GitHubUpdateService.TryParseReleaseVersion(InstalledVersion, out var version)
            ? version
            : new Version(0, 0, 0);
    }

    private static void LogUpdateFailure(string technicalMessage, Exception? exception)
    {
        if (exception != null)
            AppLogger.Warning("Update", technicalMessage, exception);
        else
            AppLogger.Warning("Update", technicalMessage);
    }

    // ---------------------------------------------------------------
    // Config loading
    // ---------------------------------------------------------------
    private void LoadConfigIntoProperties()
    {
        _isLoadingConfig = true;
        try
        {
            AdvancedListEnabled = _cfg.UseAdvancedJsonPieceMapping;
            UseDefaultOutputDir = _cfg.UseDefaultOutputDir;
            OutputDir = _cfg.OutputDir;
            UseDefaultListName = _cfg.UseDefaultListName;
            DefaultListName = _cfg.DefaultListName;
            DefaultCaseLabel = CaseModeToLabel(_cfg.DefaultCaseMode);
            DefaultSeparator = _cfg.DefaultInputSeparator;
            AdvancedSaveModeLabel = AdvancedSaveModeToLabel(ParseAdvancedSaveMode(_cfg.AdvancedSaveMode));
            ThemeName = NormalizeThemeName(_cfg.ThemeName);
            CheckUpdatesOnStartup = _cfg.CheckUpdatesOnStartup;
            EditorSeparator = _cfg.DefaultInputSeparator;
            EditorCaseLabel = CaseModeToLabel(_cfg.DefaultCaseMode);
            EditorFontSize = _cfg.EditorFontSize;
            ForgeModeEnabled = _cfg.ForgeModeEnabled;
            ForgeAnvilEnabled = _cfg.ForgeAnvilEnabled;
            ForgeHeatEnabled = _cfg.ForgeHeatEnabled;
            ForgeSparksEnabled = _cfg.ForgeSparksEnabled;
            ForgeImpactEnabled = _cfg.ForgeImpactEnabled;
            ShowJsonSection = AdvancedListEnabled;
            RefreshAdvancedJsonPieceSlots(_cfg.AdvancedJsonPieceOrder);
            RestoreCachedUpdateState();
        }
        finally
        {
            _isLoadingConfig = false;
        }
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

    private void EnsureWorkProfiles(bool saveIfChanged)
    {
        var before = JsonConvert.SerializeObject(_cfg.WorkProfiles);
        var beforeActive = _cfg.ActiveWorkProfileId;
        _workProfileService.EnsureProfiles(_cfg, _workProfileService.CaptureFromConfig(_cfg, "Original"));
        var changed = before != JsonConvert.SerializeObject(_cfg.WorkProfiles)
            || beforeActive != _cfg.ActiveWorkProfileId;

        if (changed && saveIfChanged)
        {
            try { ConfigManager.SaveConfig(_cfg); }
            catch (Exception ex) { AppLogger.Error("WorkProfiles", "Falha ao salvar perfis de trabalho.", ex, ConfigManager.ConfigPath); }
        }
    }

    private void RefreshWorkProfiles()
    {
        WorkProfiles.Clear();
        foreach (var profile in _cfg.WorkProfiles)
            WorkProfiles.Add(profile);

        var active = _workProfileService.GetActiveProfile(_cfg);
        _selectedWorkProfile = active;
        RefreshWorkProfileDisplayNames();
        Notify(nameof(SelectedWorkProfile));
        NotifyWorkProfileState();
    }

    private void RefreshWorkProfileDisplayNames()
    {
        foreach (var profile in WorkProfiles)
            profile.DisplayName = ReferenceEquals(profile, SelectedWorkProfile) && HasUnsavedWorkProfileChanges
                ? $"{profile.Name}*"
                : profile.Name;

        Notify(nameof(WorkProfiles));
    }

    private void ApplyActiveWorkProfileOnStartup()
    {
        var profile = _workProfileService.GetActiveProfile(_cfg);
        if (profile == null)
            return;

        ApplyWorkProfileSettings(profile.Settings, persist: true);
        HasUnsavedWorkProfileChanges = false;
    }

    private void ChangeSelectedWorkProfile(WorkProfile? profile)
    {
        if (profile == null)
            return;

        var previous = _selectedWorkProfile;
        if (HasUnsavedWorkProfileChanges)
        {
            var choice = MessageBox.Show(
                "Existem alterações não salvas no perfil atual.\n\nClique em Sim para salvar, Não para descartar ou Cancelar para continuar no perfil atual.",
                ConfigManager.AppName,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
            {
                Notify(nameof(SelectedWorkProfile));
                return;
            }

            if (choice == MessageBoxResult.Yes && !SaveActiveWorkProfile(showMessage: false))
            {
                Notify(nameof(SelectedWorkProfile));
                return;
            }

            if (choice == MessageBoxResult.No && previous != null)
                HasUnsavedWorkProfileChanges = false;
        }

        _selectedWorkProfile = profile;
        _cfg.ActiveWorkProfileId = profile.Id;
        ApplyWorkProfileSettings(profile.Settings, persist: true);
        HasUnsavedWorkProfileChanges = false;
        Notify(nameof(SelectedWorkProfile));
        NotifyWorkProfileState();
        StatusText = $"Perfil aplicado: {profile.Name}";
    }

    private void ApplyWorkProfileSettings(WorkProfileSettings settings, bool persist)
    {
        _isApplyingWorkProfile = true;
        try
        {
            DefaultSeparator = settings.DefaultInputSeparator;
            EditorSeparator = settings.DefaultInputSeparator;
            DefaultCaseLabel = CaseModeToLabel(settings.DefaultCaseMode);
            EditorCaseLabel = CaseModeToLabel(settings.DefaultCaseMode);
            EditorSortLabel = settings.EditorSortMode;
            AdvancedListEnabled = settings.UseAdvancedJsonPieceMapping;
            AdvancedSaveModeLabel = AdvancedSaveModeToLabel(ParseAdvancedSaveMode(settings.AdvancedSaveMode));
            UseDefaultOutputDir = settings.UseDefaultOutputDir;
            OutputDir = settings.OutputDir;
            UseDefaultListName = settings.UseDefaultListName;
            DefaultListName = settings.DefaultListName;
            ShowJsonSection = AdvancedListEnabled;
            RefreshAdvancedJsonPieceSlots(settings.AdvancedJsonPieceOrder);
            ApplyWorkProfileSettingsToConfig(settings);

            if (persist)
                ConfigManager.SaveConfig(_cfg);
        }
        catch (Exception ex)
        {
            AppLogger.Error("WorkProfiles", "Falha ao aplicar perfil de trabalho.", ex);
            MessageBox.Show("Não foi possível aplicar o perfil selecionado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isApplyingWorkProfile = false;
        }
    }

    private void ApplyWorkProfileSettingsToConfig(WorkProfileSettings settings)
    {
        _cfg.DefaultInputSeparator = string.IsNullOrWhiteSpace(settings.DefaultInputSeparator) ? "," : settings.DefaultInputSeparator.Trim();
        _cfg.DefaultCaseMode = LabelToCaseMode(CaseModeToLabel(settings.DefaultCaseMode));
        _cfg.UseAdvancedJsonPieceMapping = settings.UseAdvancedJsonPieceMapping;
        _cfg.ShowJsonTab = settings.UseAdvancedJsonPieceMapping;
        _cfg.ShowGenerateJsonButton = settings.UseAdvancedJsonPieceMapping;
        _cfg.ShowCopyJsonButton = settings.UseAdvancedJsonPieceMapping;
        _cfg.AdvancedJsonPieceOrder = settings.AdvancedJsonPieceOrder
            .Select(PieceTypeMapper.NormalizeKey)
            .Where(PieceTypeMapper.IsKnownKey)
            .ToList();
        _cfg.AdvancedSaveMode = AdvancedSaveModeToConfigValue(AdvancedSaveModeLabelToMode(AdvancedSaveModeToLabel(ParseAdvancedSaveMode(settings.AdvancedSaveMode))));
        _cfg.UseDefaultOutputDir = settings.UseDefaultOutputDir;
        _cfg.OutputDir = settings.OutputDir.Trim();
        _cfg.UseDefaultListName = settings.UseDefaultListName;
        _cfg.DefaultListName = string.IsNullOrWhiteSpace(settings.DefaultListName) ? "lista" : settings.DefaultListName.Trim();
        if (SelectedWorkProfile != null)
            _cfg.ActiveWorkProfileId = SelectedWorkProfile.Id;
    }

    private WorkProfileSettings CaptureWorkProfileSettingsFromUi() =>
        new()
        {
            DefaultInputSeparator = string.IsNullOrWhiteSpace(DefaultSeparator) ? "," : DefaultSeparator.Trim(),
            DefaultCaseMode = LabelToCaseMode(DefaultCaseLabel),
            EditorSortMode = EditorSortLabel,
            UseAdvancedJsonPieceMapping = AdvancedListEnabled,
            AdvancedJsonPieceOrder = AdvancedJsonPieceSlots
                .Select(slot => PieceTypeMapper.NormalizeKey(slot.SelectedPieceType))
                .Where(PieceTypeMapper.IsKnownKey)
                .ToList(),
            AdvancedSaveMode = AdvancedSaveModeToConfigValue(AdvancedSaveModeLabelToMode(AdvancedSaveModeLabel)),
            UseDefaultOutputDir = UseDefaultOutputDir,
            OutputDir = OutputDir.Trim(),
            UseDefaultListName = UseDefaultListName,
            DefaultListName = string.IsNullOrWhiteSpace(DefaultListName) ? "lista" : DefaultListName.Trim(),
        };

    private void MarkWorkProfileChanged()
    {
        if (_isLoadingConfig || _isApplyingWorkProfile || SelectedWorkProfile == null)
            return;

        HasUnsavedWorkProfileChanges = _workProfileService.HasUnsavedChanges(_cfg, CaptureWorkProfileSettingsFromUi());
    }

    private void NotifyWorkProfileState()
    {
        Notify(nameof(ActiveWorkProfileDisplayName));
        Notify(nameof(WorkProfileChangeStatus));
        Notify(nameof(CanManageSelectedWorkProfile));
        Notify(nameof(CanRenameSelectedWorkProfile));
        Notify(nameof(CanDeleteSelectedWorkProfile));
        CommandManager.InvalidateRequerySuggested();
    }

    private void CreateWorkProfile()
    {
        var name = ListForge.UI.Views.InputDialog.Show("Nome do novo perfil:", ConfigManager.AppName);
        if (name == null)
            return;

        var result = _workProfileService.CreateProfile(_cfg, name, CaptureWorkProfileSettingsFromUi());
        if (!result.Success || result.Value == null)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveWorkProfileConfigOrShowError();
        var createdId = result.Value.Id;
        RefreshWorkProfiles();
        SelectedWorkProfile = WorkProfiles.FirstOrDefault(p => p.Id == createdId);
        HasUnsavedWorkProfileChanges = false;
        StatusText = $"Perfil criado: {result.Value.Name}";
    }

    private bool SaveActiveWorkProfile(bool showMessage = true)
    {
        var result = _workProfileService.SaveActiveProfile(_cfg, CaptureWorkProfileSettingsFromUi());
        if (!result.Success)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        ApplyWorkProfileSettingsToConfig(CaptureWorkProfileSettingsFromUi());
        if (!SaveWorkProfileConfigOrShowError())
            return false;

        HasUnsavedWorkProfileChanges = false;
        RefreshWorkProfiles();
        StatusText = "Alterações salvas no perfil.";
        if (showMessage)
            MessageBox.Show("Alterações salvas no perfil.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private void SaveActiveWorkProfile() => SaveActiveWorkProfile(showMessage: true);

    private void DiscardWorkProfileChanges()
    {
        var profile = SelectedWorkProfile;
        if (profile == null)
            return;

        ApplyWorkProfileSettings(profile.Settings, persist: true);
        HasUnsavedWorkProfileChanges = false;
        StatusText = "Alterações do perfil descartadas.";
    }

    private void RenameWorkProfile()
    {
        var profile = SelectedWorkProfile;
        if (profile == null)
            return;

        var name = ListForge.UI.Views.InputDialog.Show("Novo nome do perfil:", ConfigManager.AppName, profile.Name);
        if (name == null)
            return;

        var result = _workProfileService.RenameProfile(_cfg, profile.Id, name);
        if (!result.Success)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveWorkProfileConfigOrShowError();
        RefreshWorkProfiles();
        StatusText = "Perfil renomeado.";
    }

    private void DuplicateWorkProfile()
    {
        var profile = SelectedWorkProfile;
        if (profile == null)
            return;

        var result = _workProfileService.DuplicateProfile(_cfg, profile.Id);
        if (!result.Success || result.Value == null)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveWorkProfileConfigOrShowError();
        var duplicatedId = result.Value.Id;
        RefreshWorkProfiles();
        SelectedWorkProfile = WorkProfiles.FirstOrDefault(p => p.Id == duplicatedId);
        StatusText = $"Perfil duplicado: {result.Value.Name}";
    }

    private void DeleteWorkProfile()
    {
        var profile = SelectedWorkProfile;
        if (profile == null)
            return;

        if (MessageBox.Show($"Deseja excluir o perfil \"{profile.Name}\"?", ConfigManager.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var result = _workProfileService.DeleteProfile(_cfg, profile.Id);
        if (!result.Success)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveWorkProfileConfigOrShowError();
        RefreshWorkProfiles();
        ApplyActiveWorkProfileOnStartup();
        StatusText = "Perfil excluído.";
    }

    private void RestoreDefaultWorkProfile()
    {
        if (MessageBox.Show("Restaurar o perfil Padrão para as configurações oficiais?", ConfigManager.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var result = _workProfileService.RestoreDefaultProfile(_cfg);
        if (!result.Success)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveWorkProfileConfigOrShowError();
        RefreshWorkProfiles();
        ApplyActiveWorkProfileOnStartup();
        StatusText = "Perfil Padrão restaurado.";
    }

    private bool SaveWorkProfileConfigOrShowError()
    {
        try
        {
            ConfigManager.SaveConfig(_cfg);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("WorkProfiles", "Falha ao salvar perfis de trabalho.", ex, ConfigManager.ConfigPath);
            MessageBox.Show("Não foi possível salvar os perfis de trabalho.\n\n" + ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
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

    private static AdvancedSaveMode ParseAdvancedSaveMode(string? value) =>
        string.Equals(value, "Zip", StringComparison.OrdinalIgnoreCase)
            ? AdvancedSaveMode.Zip
            : AdvancedSaveMode.LooseFiles;

    private static string AdvancedSaveModeToConfigValue(AdvancedSaveMode mode) =>
        mode == AdvancedSaveMode.Zip ? "Zip" : "LooseFiles";

    private static string AdvancedSaveModeToLabel(AdvancedSaveMode mode) =>
        mode == AdvancedSaveMode.Zip ? AdvancedSaveZipLabel : AdvancedSaveLooseFilesLabel;

    private static AdvancedSaveMode AdvancedSaveModeLabelToMode(string? label) =>
        string.Equals(label, AdvancedSaveZipLabel, StringComparison.OrdinalIgnoreCase)
            ? AdvancedSaveMode.Zip
            : AdvancedSaveMode.LooseFiles;

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
            _currentSourceType = ProcessingHistorySourceTypes.File;
            CurrentFileLabel = $"Arquivo atual: {path}";
        }
        else
        {
            _currentFile = null;
            _currentSourceType = ProcessingHistorySourceTypes.ImportedFile;
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
            _currentSourceType = ProcessingHistorySourceTypes.File;
            CurrentFileLabel = $"Arquivo atual: {dlg.FileName}";
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
        const string warning = "Os logs podem conter caminhos de arquivos. Revise o pacote antes de enviar.";

        if (MessageBox.Show(warning, ConfigManager.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            return;

        var dlg = new OpenFolderDialog { Title = "Escolha onde salvar o pacote de suporte" };
        if (dlg.ShowDialog() != true)
            return;

        if (!TryBuildSettingsExportSnapshot(out var settingsSnapshot))
            return;

        var snapshot = new SupportPackageSnapshot(InputText ?? "", _lastValidOutputText ?? "", settingsSnapshot!);
        var options = new SupportPackageOptions(IncludeLogs: true);
        var result = _supportPackageService.Generate(dlg.FolderName, _aboutService.BuildInfo(), options, snapshot);
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

    private void ExportSettings()
    {
        if (!TryBuildSettingsExportSnapshot(out var snapshot))
            return;

        var dlg = new SaveFileDialog
        {
            Title = "ListForge — Exportar configurações",
            DefaultExt = ".json",
            Filter = "JSON|*.json|Todos|*.*",
            FileName = SettingsExportService.BuildDefaultFileName(InstalledVersion),
        };
        if (dlg.ShowDialog() != true)
            return;

        var result = _settingsExportService.ExportToFile(dlg.FileName, snapshot!);
        if (!result.Success)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        StatusText = $"Configurações exportadas: {Path.GetFileName(dlg.FileName)}";
        MessageBox.Show(
            $"Configurações exportadas com sucesso.\n\n{dlg.FileName}",
            ConfigManager.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ImportSettings()
    {
        var dlg = new OpenFileDialog
        {
            Title = "ListForge — Importar configurações",
            DefaultExt = ".json",
            Filter = "JSON|*.json|Todos|*.*",
        };
        if (dlg.ShowDialog() != true)
            return;

        if (MessageBox.Show(
                "Importar as configurações deste arquivo?\n\nAs preferências importáveis serão aplicadas e salvas. O arquivo atual, a entrada, a saída e o JSON não serão alterados.",
                ConfigManager.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _settingsExportService.ImportFromFile(dlg.FileName, _cfg, _sizeCfg);
        if (!result.Success || result.Value == null)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _cfg = result.Value.Config;
        _sizeCfg = result.Value.Sizes;

        try
        {
            ConfigManager.SaveConfig(_cfg);
            ConfigManager.SaveSizeConfig(_sizeCfg);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsImport", "Falha ao salvar configurações importadas.", ex);
            MessageBox.Show("As configurações foram lidas, mas não puderam ser salvas.\n\n" + ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        LoadConfigIntoProperties();
        LoadSizeConfigIntoBindings();
        RefreshSizeSummary();
        RefreshSockSizeOptions();
        RequestThemeChange?.Invoke(ThemeName);

        StatusText = $"Configurações importadas: {Path.GetFileName(dlg.FileName)}";
        MessageBox.Show(
            "Configurações importadas com sucesso.",
            ConfigManager.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private bool TryBuildSettingsExportSnapshot(out SettingsExportSnapshot? snapshot)
    {
        snapshot = null;

        SizeConfig sizes;
        try
        {
            sizes = BuildSizeConfigFromUI();
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsExport", "Falha ao validar configurações antes da exportação.", ex);
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        var config = new AppConfig
        {
            ShowJsonTab = AdvancedListEnabled,
            ShowGenerateJsonButton = AdvancedListEnabled,
            ShowCopyJsonButton = AdvancedListEnabled,
            UseAdvancedJsonPieceMapping = AdvancedListEnabled,
            AdvancedJsonPieceOrder = AdvancedJsonPieceSlots
                .Select(slot => PieceTypeMapper.NormalizeKey(slot.SelectedPieceType))
                .Where(PieceTypeMapper.IsKnownKey)
                .ToList(),
            AdvancedSaveMode = AdvancedSaveModeToConfigValue(AdvancedSaveModeLabelToMode(AdvancedSaveModeLabel)),
            UseDefaultOutputDir = UseDefaultOutputDir,
            OutputDir = "",
            UseDefaultListName = UseDefaultListName,
            DefaultListName = DefaultListName.Trim(),
            DefaultCaseMode = LabelToCaseMode(DefaultCaseLabel),
            DefaultInputSeparator = string.IsNullOrWhiteSpace(DefaultSeparator) ? "," : DefaultSeparator.Trim(),
            ThemeName = ThemeName,
            EditorFontSize = ClampEditorFontSize(EditorFontSize),
            ForgeModeEnabled = ForgeModeEnabled,
            ForgeAnvilEnabled = ForgeAnvilEnabled,
            ForgeHeatEnabled = ForgeHeatEnabled,
            ForgeSparksEnabled = ForgeSparksEnabled,
            ForgeImpactEnabled = ForgeImpactEnabled,
            CheckUpdatesOnStartup = CheckUpdatesOnStartup,
        };

        snapshot = new SettingsExportSnapshot(config, sizes, InstalledVersion);
        return true;
    }

    // ---------------------------------------------------------------
    // Extract from URL
    // ---------------------------------------------------------------
    private async Task ExtractFromLinkAsync(ExtractedListDestination destination)
    {
        if (IsExtractingFromLink)
            return;

        var url = ListForge.UI.Views.InputDialog.Show(
            "Cole o link do JSON para extrair a lista:", ConfigManager.AppName);
        if (string.IsNullOrWhiteSpace(url)) return;

        if (destination == ExtractedListDestination.NewList
            && HasCurrentListContent()
            && MessageBox.Show(
                "A lista atual será substituída somente se a extração terminar com sucesso.\n\nDeseja continuar?",
                ConfigManager.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            StatusText = "Extração cancelada.";
            AppLogger.Info("ExtractFromLink", "Criação de nova lista por link cancelada antes da extração.");
            return;
        }

        IsExtractingFromLink = true;
        StatusText = "Extraindo lista...";

        try
        {
            var result = await _linkListImportService
                .ExtractAsync(url.Trim(), EditorSeparator, _sizeCfg)
                .ConfigureAwait(true);

            if (!result.Success || result.Value == null)
            {
                if (result.Exception != null)
                    AppLogger.Error("ExtractFromLink", result.TechnicalMessage, result.Exception);
                else
                    AppLogger.Warning("ExtractFromLink", result.TechnicalMessage);

                StatusText = result.UserMessage;
                MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (destination == ExtractedListDestination.NewList)
            {
                ApplyExtractedNewList(result.Value.Text);
                ProcessAndPreview(consumeTrialCredit: false);
                StatusText = "Nova lista criada com sucesso.";
                AppLogger.Info("ExtractFromLink", $"Nova lista criada por link com {result.Value.LineCount} registro(s).");
            }
            else
            {
                AppendExtractedList(result.Value.Text);
                ProcessAndPreview(consumeTrialCredit: false);
                StatusText = $"Itens adicionados à lista atual: {result.Value.LineCount} registro(s).";
                AppLogger.Info("ExtractFromLink", $"Lista do link adicionada com {result.Value.LineCount} registro(s).");
            }
        }
        finally
        {
            IsExtractingFromLink = false;
        }
    }

    private bool HasCurrentListContent() =>
        !string.IsNullOrWhiteSpace(InputText)
        || !string.IsNullOrWhiteSpace(OutputText)
        || !string.IsNullOrWhiteSpace(JsonText);

    private void ApplyExtractedNewList(string extracted)
    {
        InputText = extracted;
        _currentFile = null;
        _currentSourceType = ProcessingHistorySourceTypes.Link;
        CurrentFileLabel = "Arquivo atual: (lista extraída do link)";
        ClearSearchHighlight(keepStatus: true);
        ClearValidationHighlights();
    }

    private void AppendExtractedList(string extracted)
    {
        InputText = CombineInputText(InputText, extracted);
        ClearSearchHighlight(keepStatus: true);
        ClearValidationHighlights();
    }

    internal static string CombineInputText(string current, string extracted)
    {
        var currentText = (current ?? "").TrimEnd('\r', '\n');
        var extractedText = (extracted ?? "").Trim('\r', '\n');
        if (string.IsNullOrWhiteSpace(currentText))
            return extractedText;
        if (string.IsNullOrWhiteSpace(extractedText))
            return currentText;
        return currentText + "\n" + extractedText;
    }

    // ---------------------------------------------------------------
    // Processing
    // ---------------------------------------------------------------
    private void AnalyzeAndProcessWithPreview()
    {
        if (IsProcessingBusy)
            return;

        try
        {
            IsProcessingBusy = true;
            if (!EnsureNoPendingOutputOrJsonEdits())
                return;

            var snapshot = BuildProcessingPreviewSnapshot();
            var preview = _processingPreviewService.Analyze(snapshot);
            if (preview.AnalysisResult.Status == ProcessingWorkflowStatus.ValidationFailed)
            {
                var validationIssues = preview.AnalysisResult.ValidationIssues;
                ValidationHighlightLines = validationIssues.Select(issue => issue.LineNumber).ToArray();
                if (validationIssues.Count > 0)
                    RequestScrollToLine?.Invoke(validationIssues[0].LineNumber);
            }
            else
            {
                ClearValidationHighlights();
            }

            StatusText = $"Prévia: {preview.TotalRecords} registro(s), {preview.InvalidRecords} inválido(s), {preview.WarningRecords} aviso(s).";
            var owner = Application.Current?.MainWindow;
            var confirmed = owner != null
                ? ListForge.UI.Views.ProcessingPreviewDialog.ShowDialog(owner, preview)
                : false;
            if (!confirmed)
            {
                StatusText = "Processamento aguardando confirmação.";
                return;
            }

            var result = _processingPreviewService.ExecuteConfirmed(preview);
            HandleProcessingWorkflowResult(result, preview.Snapshot.Request);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessPreview", "Falha ao analisar ou processar lista.", ex);
            GotoErrorLine(ex.Message);
            StatusText = $"Erro: {ex.Message}";
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessingBusy = false;
        }
    }

    private void ProcessAndPreview(bool consumeTrialCredit = true)
    {
        if (IsProcessingBusy)
            return;

        try
        {
            IsProcessingBusy = true;
            if (!EnsureNoPendingOutputOrJsonEdits())
                return;

            var request = new ProcessingWorkflowRequest(
                InputText,
                EditorSeparator,
                _sizeCfg,
                LabelToCaseMode(EditorCaseLabel),
                LabelToSortMode(EditorSortLabel),
                BuildJsonPieceMappingOptions(),
                consumeTrialCredit);
            var result = _processingWorkflowService.Execute(request);

            HandleProcessingWorkflowResult(result, request);
        }
        catch (Exception ex)
        {
            AppLogger.Error("ProcessList", "Falha ao processar lista.", ex);
            GotoErrorLine(ex.Message);
            StatusText = $"Erro: {ex.Message}";
            MessageBox.Show(ex.Message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessingBusy = false;
        }
    }

    private ProcessingPreviewSnapshot BuildProcessingPreviewSnapshot()
    {
        var request = new ProcessingWorkflowRequest(
            InputText,
            EditorSeparator,
            _sizeCfg,
            LabelToCaseMode(EditorCaseLabel),
            LabelToSortMode(EditorSortLabel),
            BuildJsonPieceMappingOptions(),
            ConsumeTrialCredit: false);

        return new ProcessingPreviewSnapshot(
            request,
            ActiveWorkProfileDisplayName,
            HasUnsavedWorkProfileChanges,
            AdvancedListEnabled,
            ResolveOutputDirDescription(),
            ResolveOutputNameDescription());
    }

    private string ResolveOutputDirDescription()
    {
        if (!UseDefaultOutputDir)
            return string.IsNullOrWhiteSpace(OutputDir) ? "não definida" : OutputDir.Trim();

        var currentDir = !string.IsNullOrWhiteSpace(_currentFile)
            ? Path.GetDirectoryName(_currentFile)
            : null;
        return string.IsNullOrWhiteSpace(currentDir) ? "pasta da lista atual" : currentDir;
    }

    private string ResolveOutputNameDescription()
    {
        if (UseDefaultListName)
            return CoreProcessor.SanitizeBaseFilename(string.IsNullOrWhiteSpace(DefaultListName) ? "lista" : DefaultListName);

        if (!string.IsNullOrWhiteSpace(_currentFile))
            return CoreProcessor.SanitizeBaseFilename(Path.GetFileNameWithoutExtension(_currentFile));

        return "lista";
    }

    private bool EnsureNoPendingOutputOrJsonEdits()
    {
        if (!HasPendingOutputOrJsonEdit)
            return true;

        var result = MessageBox.Show(
            "Existem alterações pendentes na saída ou no JSON. Clique em Sim para aplicar, Não para descartar ou Cancelar para continuar editando.",
            ConfigManager.AppName,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
            return ApplyOutputEdits();

        DiscardOutputEdits();
        return true;
    }

    private void HandleProcessingWorkflowResult(
        ProcessingWorkflowResult result,
        ProcessingWorkflowRequest request)
    {
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
            if (validationIssues.Count > 0)
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

        SetGeneratedTexts(result.OutputText, result.JsonPreview);
        CaptureComparisonSnapshot(request, result);
        SelectedOutputSection = "list";
        ClearValidationHighlights();

        StatusText = $"Processado: {result.Rows.Count} linha(s) | Ordenação: {EditorSortLabel} | Separador: {CoreProcessor.SeparatorLabel(EditorSeparator)!.Replace("\"", "'")}{_licenseService.ProcessingStatusSuffix}";
        TriggerForgeEffect();
    }

    private void CaptureComparisonSnapshot(
        ProcessingWorkflowRequest request,
        ProcessingWorkflowResult result)
    {
        try
        {
            _comparisonSnapshot = _listComparisonService.CreateSnapshot(
                request,
                result,
                ActiveWorkProfileDisplayName,
                AdvancedListEnabled);
            _comparisonInputRevision = _inputRevision;
            NotifyComparisonState();
        }
        catch (Exception ex)
        {
            _comparisonSnapshot = null;
            _comparisonInputRevision = -1;
            NotifyComparisonState();
            AppLogger.Error("ListComparison", "Falha ao capturar o estado da comparação.", ex);
        }
    }

    private async Task CompareInputOutputAsync()
    {
        var snapshot = _comparisonSnapshot;
        if (!CanCompareInputOutput || snapshot == null)
        {
            StatusText = _comparisonSnapshot == null
                ? "Não há dados suficientes para comparação."
                : "A entrada ou a saída foi alterada. Gere uma nova comparação.";
            return;
        }

        try
        {
            IsComparisonBusy = true;
            StatusText = "Comparando entrada e saída...";
            var result = await Task.Run(() => _listComparisonService.Compare(snapshot));

            if (!ReferenceEquals(snapshot, _comparisonSnapshot) || !IsComparisonSnapshotCurrent(snapshot))
            {
                StatusText = "A entrada ou a saída foi alterada. Gere uma nova comparação.";
                return;
            }

            var comparisonRequested = RequestComparison;
            if (comparisonRequested == null)
            {
                StatusText = "Não foi possível abrir a comparação.";
                return;
            }

            var comparisonViewModel = new ComparisonViewModel(result, EditorFontSize);
            comparisonRequested(comparisonViewModel);
            StatusText = result.Summary.HasCriticalDifferences
                ? "Comparação concluída com diferenças para revisão."
                : "Comparação concluída. Nenhuma diferença crítica foi encontrada.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("ListComparison", "Falha ao comparar entrada e saída.", ex);
            StatusText = "Não foi possível comparar a entrada e a saída.";
            MessageBox.Show(
                "Não foi possível comparar a entrada e a saída. Consulte os logs para obter detalhes técnicos.",
                ConfigManager.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsComparisonBusy = false;
        }
    }

    private void NotifyComparisonState()
    {
        Notify(nameof(CanCompareInputOutput));
        Notify(nameof(ComparisonActionTooltip));
        CommandManager.InvalidateRequerySuggested();
    }

    private void TriggerForgeEffect()
    {
        if (!ForgeModeEnabled)
            return;

        _forgeIsHot = true;
        ForgeStatusText = "Forjado com sucesso.";
        Notify(nameof(ForgeTemperatureText));

        if (ShowForgeImpact || ShowForgeHeat || ShowForgeSparks)
            ForgeEffectPulse++;
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
    private void SetGeneratedTexts(string output, string json)
    {
        _isUpdatingGeneratedText = true;
        try
        {
            OutputText = output;
            JsonText = json;
            _lastValidOutputText = output;
            _lastValidJsonText = json;
            HasPendingOutputEdit = false;
            HasPendingJsonEdit = false;
        }
        finally
        {
            _isUpdatingGeneratedText = false;
        }
    }

    private bool ApplyOutputEdits()
    {
        if (!HasPendingOutputOrJsonEdit)
            return true;

        try
        {
            var sourceTypeAfterApply = HasPendingJsonEdit
                ? ProcessingHistorySourceTypes.EditedJson
                : ProcessingHistorySourceTypes.EditedOutput;
            var existingComparisonSnapshot = _comparisonSnapshot;
            var canUpdateComparisonSnapshot = existingComparisonSnapshot != null
                && _comparisonInputRevision == _inputRevision
                && string.Equals(InputText, existingComparisonSnapshot.InputText, StringComparison.Ordinal);
            var input = HasPendingJsonEdit
                ? CoreProcessor.ExtractListTextFromJsonData(JObject.Parse(JsonText), EditorSeparator, includeHeader: true)
                : OutputText;

            var result = _processingWorkflowService.Execute(new ProcessingWorkflowRequest(
                input,
                EditorSeparator,
                _sizeCfg,
                LabelToCaseMode(EditorCaseLabel),
                LabelToSortMode(EditorSortLabel),
                BuildJsonPieceMappingOptions(),
                ConsumeTrialCredit: false));

            if (result.Status == ProcessingWorkflowStatus.ValidationFailed)
            {
                var issue = result.ValidationIssues.FirstOrDefault();
                var message = HasPendingJsonEdit
                    ? "O JSON contém erros e não pôde ser aplicado."
                    : issue != null
                        ? $"A lista de saída contém dados inválidos na linha {issue.LineNumber}."
                        : "A lista de saída contém dados inválidos.";
                StatusText = message;
                MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (result.Status != ProcessingWorkflowStatus.Success)
            {
                var message = HasPendingJsonEdit
                    ? "O JSON contém erros e não pôde ser aplicado."
                    : "A lista de saída não pôde ser aplicada.";
                StatusText = message;
                MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _rows = result.Rows;
            _lastOrders = result.Orders;
            _lastJson = result.JsonPreview;
            SetGeneratedTexts(result.OutputText, result.JsonPreview);
            _comparisonSnapshot = canUpdateComparisonSnapshot
                ? _listComparisonService.UpdateOutput(existingComparisonSnapshot!, result, outputWasManuallyEdited: true)
                : null;
            if (_comparisonSnapshot == null)
                _comparisonInputRevision = -1;
            NotifyComparisonState();
            _currentFile = null;
            _currentSourceType = sourceTypeAfterApply;
            CurrentFileLabel = sourceTypeAfterApply == ProcessingHistorySourceTypes.EditedJson
                ? "Arquivo atual: (JSON editado)"
                : "Arquivo atual: (lista de saída editada)";
            StatusText = "Alterações aplicadas.";
            return true;
        }
        catch (JsonReaderException ex)
        {
            AppLogger.Warning("ApplyJsonEdit", "JSON editado possui erro de sintaxe.", ex);
            var message = $"O JSON contém erros e não pôde ser aplicado. Linha {ex.LineNumber}, posição {ex.LinePosition}.";
            StatusText = message;
            MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.Error("ApplyOutputEdit", "Falha ao aplicar edição manual da saída.", ex);
            var message = HasPendingJsonEdit
                ? "O JSON contém erros e não pôde ser aplicado."
                : "A lista de saída contém dados inválidos.";
            StatusText = message;
            MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void DiscardOutputEdits()
    {
        SetGeneratedTexts(_lastValidOutputText, _lastValidJsonText);
        StatusText = "Alterações descartadas.";
    }

    public bool TryLeaveOutputSection(string targetSection)
    {
        if (!HasPendingOutputOrJsonEdit || string.Equals(SelectedOutputSection, targetSection, StringComparison.OrdinalIgnoreCase))
            return true;

        var result = MessageBox.Show(
            "Existem alterações pendentes. Clique em Sim para aplicar, Não para descartar ou Cancelar para continuar editando.",
            ConfigManager.AppName,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
            return ApplyOutputEdits();

        DiscardOutputEdits();
        return true;
    }

    private void AdvancedSave()
    {
        if (!AdvancedListEnabled)
        {
            MessageBox.Show("Ative Lista avançada antes de usar o Salvar avançado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ShowJsonSection)
        {
            MessageBox.Show("Ative os recursos de JSON antes de usar o Salvar avançado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputText) || _lastOrders.Count == 0)
        {
            MessageBox.Show("Processe a lista antes de usar o Salvar avançado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var json = _lastJson;
        if (string.IsNullOrWhiteSpace(json) && _lastOrders.Count > 0)
        {
            json = CoreProcessor.BuildJsonPreview(_lastOrders);
            _lastJson = json;
            JsonText = json;
        }

        var baseName = ResolveAdvancedSaveName();
        if (baseName == null)
        {
            AppLogger.Info("AdvancedSave", "Exportacao avancada cancelada na escolha do nome.");
            return;
        }

        var outputDirectory = ResolveAdvancedSaveOutputDir();
        if (outputDirectory == null)
        {
            AppLogger.Info("AdvancedSave", "Exportacao avancada cancelada na escolha da pasta.");
            return;
        }

        var mode = AdvancedSaveModeLabelToMode(AdvancedSaveModeLabel);
        var result = _advancedSaveService.Save(new AdvancedSaveRequest(
            outputDirectory,
            baseName,
            InputText,
            OutputText,
            json,
            mode));

        if (!result.Success || result.Value == null)
        {
            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var saved = result.Value;
        RegisterProcessingHistory(ResolveAdvancedSaveHistoryPath(saved), ResolveHistoryProcessedCount(preferJsonCount: true));
        StatusText = saved.Mode == AdvancedSaveMode.Zip
            ? $"Salvar avançado gerado: {Path.GetFileName(saved.ZipPath)}"
            : $"Salvar avançado gerado: {saved.FilePaths.Count} arquivo(s).";

        var message = saved.Mode == AdvancedSaveMode.Zip
            ? $"Salvar avançado concluído:\n{saved.ZipPath}"
            : "Salvar avançado concluído:\n" + string.Join("\n", saved.FilePaths);

        MessageBox.Show(message, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

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
        RegisterProcessingHistory(path, ResolveHistoryProcessedCount(preferJsonCount: false));
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
        RegisterProcessingHistory(path, ResolveHistoryProcessedCount(preferJsonCount: true));
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

    private string? ResolveAdvancedSaveName()
    {
        var suggested = string.IsNullOrWhiteSpace(_currentFile)
            ? (string.IsNullOrWhiteSpace(DefaultListName) ? "lista" : DefaultListName)
            : CoreProcessor.SanitizeBaseFilename(Path.GetFileNameWithoutExtension(_currentFile));

        var typed = ListForge.UI.Views.InputDialog.Show(
            "Informe o nome base para o Salvar avançado:", ConfigManager.AppName, suggested);
        if (typed == null) return null;

        if (string.IsNullOrWhiteSpace(typed))
        {
            MessageBox.Show("Informe um nome base para o Salvar avançado.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var baseName = CoreProcessor.SanitizeBaseFilename(typed);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            MessageBox.Show("Nome inválido.", ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        return baseName;
    }

    private string? ResolveAdvancedSaveOutputDir()
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

        var dlg = new OpenFolderDialog { Title = "Escolha a pasta do Salvar avançado" };
        if (dlg.ShowDialog() != true) return null;
        return dlg.FolderName;
    }

    private void LoadProcessingHistory()
    {
        ProcessingHistoryEntries.Clear();
        foreach (var entry in _processingHistoryService.Load())
            ProcessingHistoryEntries.Add(entry);

        SelectedHistoryEntry = ProcessingHistoryEntries.FirstOrDefault();
        NotifyProcessingHistoryState();
    }

    private void NotifyProcessingHistoryState()
    {
        Notify(nameof(HasProcessingHistoryEntries));
        Notify(nameof(ShowEmptyProcessingHistory));
        Notify(nameof(CanOpenSelectedHistoryOutput));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RegisterProcessingHistory(string? outputPath, int processedLineCount)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        var source = ProcessingHistoryService.BuildSafeSource(_currentFile, CurrentFileLabel, _currentSourceType);
        var result = _processingHistoryService.Add(new ProcessingHistoryEntry
        {
            SourceDisplayName = source.DisplayName,
            SourceType = source.SourceType,
            ProcessedLineCount = processedLineCount,
            OutputPath = outputPath,
        });

        if (!result.Success || result.Value == null)
        {
            if (result.Exception != null)
                AppLogger.Error("ProcessingHistory", result.TechnicalMessage, result.Exception);

            StatusText = "Processamento concluído, mas não foi possível atualizar o histórico.";
            return;
        }

        ProcessingHistoryEntries.Insert(0, result.Value);
        while (ProcessingHistoryEntries.Count > ProcessingHistoryService.MaxEntries)
            ProcessingHistoryEntries.RemoveAt(ProcessingHistoryEntries.Count - 1);

        SelectedHistoryEntry = result.Value;
        NotifyProcessingHistoryState();
    }

    private static string? ResolveAdvancedSaveHistoryPath(AdvancedSaveResult saved)
    {
        if (!string.IsNullOrWhiteSpace(saved.ZipPath))
            return saved.ZipPath;

        return saved.FilePaths.FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            ?? saved.FilePaths.FirstOrDefault();
    }

    private int ResolveHistoryProcessedCount(bool preferJsonCount)
    {
        if (preferJsonCount && _lastOrders.Count > 0)
            return _lastOrders.Count;

        return _rows.Count > 0 ? _rows.Count : _lastOrders.Count;
    }

    private void OpenSelectedHistoryOutput()
    {
        if (SelectedHistoryEntry == null)
            return;

        var result = _processingHistoryService.OpenOutputFolder(SelectedHistoryEntry);
        if (!result.Success)
        {
            if (result.Exception != null)
                AppLogger.Error("ProcessingHistory", result.TechnicalMessage, result.Exception, SelectedHistoryEntry.OutputPath);

            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = "Pasta da saída aberta.";
    }

    private void ClearProcessingHistory()
    {
        if (!HasProcessingHistoryEntries)
            return;

        var confirm = MessageBox.Show(
            "Deseja apagar todo o histórico de processamentos?",
            ConfigManager.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        var result = _processingHistoryService.Clear();
        if (!result.Success)
        {
            if (result.Exception != null)
                AppLogger.Error("ProcessingHistory", result.TechnicalMessage, result.Exception);

            MessageBox.Show(result.UserMessage, ConfigManager.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ProcessingHistoryEntries.Clear();
        SelectedHistoryEntry = null;
        NotifyProcessingHistoryState();
        StatusText = "Histórico limpo.";
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
        _comparisonSnapshot = null;
        _comparisonInputRevision = -1;
        NotifyComparisonState();
        _currentFile = null;
        _currentSourceType = ProcessingHistorySourceTypes.PastedText;
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
    private void SaveAdvancedListSettings()
    {
        _cfg.ShowJsonTab = AdvancedListEnabled;
        _cfg.ShowGenerateJsonButton = AdvancedListEnabled;
        _cfg.ShowCopyJsonButton = AdvancedListEnabled;
        _cfg.UseAdvancedJsonPieceMapping = AdvancedListEnabled;

        try { ConfigManager.SaveConfig(_cfg); }
        catch (Exception ex) { AppLogger.Error("AdvancedList", "Falha ao salvar Lista avançada no config.json.", ex, ConfigManager.ConfigPath); }
    }

    private void SaveForgeSettings()
    {
        if (_isLoadingConfig)
            return;

        _cfg.ForgeModeEnabled = ForgeModeEnabled;
        _cfg.ForgeAnvilEnabled = ForgeAnvilEnabled;
        _cfg.ForgeHeatEnabled = ForgeHeatEnabled;
        _cfg.ForgeSparksEnabled = ForgeSparksEnabled;
        _cfg.ForgeImpactEnabled = ForgeImpactEnabled;

        try { ConfigManager.SaveConfig(_cfg); }
        catch (Exception ex) { AppLogger.Error("ForgeMode", "Falha ao salvar Modo Forja no config.json.", ex, ConfigManager.ConfigPath); }
    }

    private void NotifyForgeState()
    {
        Notify(nameof(ShowForgePanel));
        Notify(nameof(ShowForgeHeat));
        Notify(nameof(ShowForgeSparks));
        Notify(nameof(ShowForgeImpact));
        Notify(nameof(ForgeTemperatureText));
    }

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

        _cfg.ShowJsonTab = AdvancedListEnabled;
        _cfg.ShowGenerateJsonButton = AdvancedListEnabled;
        _cfg.ShowCopyJsonButton = AdvancedListEnabled;
        _cfg.UseAdvancedJsonPieceMapping = AdvancedListEnabled;
        _cfg.AdvancedJsonPieceOrder = AdvancedJsonPieceSlots
            .Select(slot => PieceTypeMapper.NormalizeKey(slot.SelectedPieceType))
            .Where(PieceTypeMapper.IsKnownKey)
            .ToList();
        _cfg.UseDefaultOutputDir = UseDefaultOutputDir;
        _cfg.OutputDir = OutputDir.Trim();
        _cfg.UseDefaultListName = UseDefaultListName;
        _cfg.DefaultListName = DefaultListName.Trim();
        _cfg.DefaultCaseMode = LabelToCaseMode(DefaultCaseLabel);
        _cfg.DefaultInputSeparator = string.IsNullOrWhiteSpace(DefaultSeparator) ? "," : DefaultSeparator.Trim();
        _cfg.AdvancedSaveMode = AdvancedSaveModeToConfigValue(AdvancedSaveModeLabelToMode(AdvancedSaveModeLabel));
        _cfg.ThemeName = ThemeName;
        _cfg.EditorFontSize = ClampEditorFontSize(EditorFontSize);
        _cfg.ForgeModeEnabled = ForgeModeEnabled;
        _cfg.ForgeAnvilEnabled = ForgeAnvilEnabled;
        _cfg.ForgeHeatEnabled = ForgeHeatEnabled;
        _cfg.ForgeSparksEnabled = ForgeSparksEnabled;
        _cfg.ForgeImpactEnabled = ForgeImpactEnabled;
        _cfg.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
        ApplyWorkProfileSettingsToConfig(CaptureWorkProfileSettingsFromUi());
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
    }

    private JsonPieceMappingOptions BuildJsonPieceMappingOptions() =>
        new(
            HasJsonFeaturesEnabled && UseAdvancedJsonPieceMapping,
            AdvancedJsonPieceSlots
                .Select(slot => PieceTypeMapper.NormalizeKey(slot.SelectedPieceType))
                .Where(PieceTypeMapper.IsKnownKey)
                .ToList());

    private void RefreshAdvancedJsonPieceSlots(IEnumerable<string>? preferredOrder = null)
    {
        if (_sizeCfg == null)
            return;

        _isRefreshingAdvancedJsonPieceSlots = true;
        try
        {
            var currentOrder = preferredOrder?.ToList()
                ?? AdvancedJsonPieceSlots.Select(slot => slot.SelectedPieceType).ToList();

            var requiredSlots = string.IsNullOrWhiteSpace(InputText)
                ? JsonPieceMappingService.ClampSlotCount(System.Math.Max(1, currentOrder.Count))
                : _jsonPieceMappingService.EstimateRequiredSlots(InputText, EditorSeparator, _sizeCfg);

            while (AdvancedJsonPieceSlots.Count > requiredSlots)
            {
                var slot = AdvancedJsonPieceSlots[^1];
                slot.PropertyChanged -= AdvancedJsonPieceSlot_PropertyChanged;
                AdvancedJsonPieceSlots.RemoveAt(AdvancedJsonPieceSlots.Count - 1);
            }

            for (var i = 0; i < requiredSlots; i++)
            {
                var selected = i < currentOrder.Count ? PieceTypeMapper.NormalizeKey(currentOrder[i]) : "";
                if (!PieceTypeMapper.IsKnownKey(selected)) selected = "";

                if (i < AdvancedJsonPieceSlots.Count)
                {
                    AdvancedJsonPieceSlots[i].Position = i + 1;
                    AdvancedJsonPieceSlots[i].SelectedPieceType = selected;
                }
                else
                {
                    var slot = new AdvancedJsonPieceSlot(i + 1, selected);
                    slot.PropertyChanged += AdvancedJsonPieceSlot_PropertyChanged;
                    AdvancedJsonPieceSlots.Add(slot);
                }
            }
        }
        finally
        {
            _isRefreshingAdvancedJsonPieceSlots = false;
        }

        RefreshAdvancedJsonPieceSlotOptions();
    }

    private void AdvancedJsonPieceSlot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isRefreshingAdvancedJsonPieceSlots && e.PropertyName == nameof(AdvancedJsonPieceSlot.SelectedPieceType))
        {
            RefreshAdvancedJsonPieceSlotOptions();
            MarkWorkProfileChanged();
        }
    }

    private void RefreshAdvancedJsonPieceSlotOptions()
    {
        foreach (var slot in AdvancedJsonPieceSlots)
        {
            var current = PieceTypeMapper.NormalizeKey(slot.SelectedPieceType);
            var usedByOtherSlots = AdvancedJsonPieceSlots
                .Where(other => !ReferenceEquals(other, slot))
                .Select(other => PieceTypeMapper.NormalizeKey(other.SelectedPieceType))
                .Where(PieceTypeMapper.IsKnownKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            slot.SetAvailablePieceTypes(usedByOtherSlots, current);
        }
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

public class AdvancedJsonPieceSlot : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _position;
    private string _selectedPieceType = "";
    public ObservableCollection<AdvancedJsonPieceOption> AvailablePieceOptions { get; } = [];

    public AdvancedJsonPieceSlot(int position, string selectedPieceType)
    {
        _position = position;
        _selectedPieceType = selectedPieceType;
        AvailablePieceOptions.Add(new AdvancedJsonPieceOption("", "Selecionar"));
        foreach (var option in PieceTypeMapper.AvailableOptions)
            AvailablePieceOptions.Add(new AdvancedJsonPieceOption(option.Key, option.Label));
    }

    public int Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            Notify();
            Notify(nameof(PositionLabel));
        }
    }

    public string PositionLabel => $"{Position}º tamanho";

    public string SelectedPieceType
    {
        get => _selectedPieceType;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_selectedPieceType, value)) return;
            _selectedPieceType = value;
            Notify();
        }
    }

    public void SetAvailablePieceTypes(ISet<string> unavailableKeys, string currentKey)
    {
        var desiredOptions = BuildAvailablePieceOptions(unavailableKeys, currentKey).ToList();
        var desiredKeys = desiredOptions
            .Select(option => option.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = AvailablePieceOptions.Count - 1; i >= 0; i--)
        {
            if (!desiredKeys.Contains(AvailablePieceOptions[i].Key))
                AvailablePieceOptions.RemoveAt(i);
        }

        for (var desiredIndex = 0; desiredIndex < desiredOptions.Count; desiredIndex++)
        {
            var desired = desiredOptions[desiredIndex];
            var currentIndex = IndexOfOption(desired.Key);

            if (currentIndex < 0)
            {
                AvailablePieceOptions.Insert(desiredIndex, desired);
                continue;
            }

            if (currentIndex != desiredIndex)
                AvailablePieceOptions.Move(currentIndex, desiredIndex);
        }
    }

    private static IEnumerable<AdvancedJsonPieceOption> BuildAvailablePieceOptions(
        ISet<string> unavailableKeys,
        string currentKey)
    {
        yield return new AdvancedJsonPieceOption("", "Selecionar");

        foreach (var option in PieceTypeMapper.AvailableOptions)
        {
            if (string.Equals(option.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                || !unavailableKeys.Contains(option.Key))
                yield return new AdvancedJsonPieceOption(option.Key, option.Label);
        }
    }

    private int IndexOfOption(string key)
    {
        for (var i = 0; i < AvailablePieceOptions.Count; i++)
        {
            if (string.Equals(AvailablePieceOptions[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AdvancedJsonPieceOption
{
    public AdvancedJsonPieceOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    public override string ToString() => Label;
}
