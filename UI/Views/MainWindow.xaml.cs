using System.Windows;
using System.Windows.Controls;
using ListForge.Config;
using ListForge.Core;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _isApplyingTheme;

    public MainWindow()
    {
        InitializeComponent();
        Title = ConfigManager.AppTitle;
        _vm = new MainViewModel();
        DataContext = _vm;

        LogoTitle.Text = ConfigManager.AppTitle;
        LogoSubtitle.Text = ConfigManager.IsTrialBuild
            ? $"Versão Trial - limite de {ConfigManager.TrialProcessingLimit} processamentos"
            : "Organização e transformação de listas";

        EditorViewControl.SetViewModel(_vm);
        HistoryViewControl.SetViewModel(_vm);
        SettingsViewControl.SetViewModel(_vm);
        AboutViewControl.SetViewModel(_vm);

        StatusLabel.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(_vm.StatusText)) { Source = _vm });
        SidebarAdvancedJsonPieceSlots.SetBinding(ItemsControl.ItemsSourceProperty,
            new System.Windows.Data.Binding(nameof(_vm.AdvancedJsonPieceSlots)) { Source = _vm });

        _vm.RequestThemeChange += themeName => ApplyTheme(themeName);
        _vm.RequestScrollToLine += lineNo => EditorViewControl.ScrollToLine(lineNo);
        _vm.RequestShutdown += Close;
        _vm.RequestComparison += ShowComparison;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.AdvancedListEnabled)
                || e.PropertyName == nameof(_vm.ShowJsonSection)
                || e.PropertyName == nameof(_vm.ShowAdvancedJsonOptions))
                RefreshAdvancedListPanelVisibility();
        };

        // Apply theme saved in config on startup
        ApplyTheme(_vm.ThemeName);

        ShowScreen("editor");
        Loaded += async (_, _) => await _vm.CheckForUpdatesOnStartupAsync();
    }

    // ---------------------------------------------------------------
    // Navigation
    // ---------------------------------------------------------------
    private string _currentScreen = "editor";

    private void ShowScreen(string key)
    {
        _currentScreen = key;

        EditorViewControl.Visibility = key == "editor" ? Visibility.Visible : Visibility.Collapsed;
        HistoryViewControl.Visibility = key == "history" ? Visibility.Visible : Visibility.Collapsed;
        SettingsViewControl.Visibility = key == "settings" ? Visibility.Visible : Visibility.Collapsed;
        ManualViewControl.Visibility = key == "manual" ? Visibility.Visible : Visibility.Collapsed;
        AboutViewControl.Visibility = key == "about" ? Visibility.Visible : Visibility.Collapsed;

        if (key == "about")
        {
            _vm.RefreshAboutInfo();
            AppLogger.Info("About", "Tela Sobre aberta.");
        }

        TopbarTitle.Text = key switch
        {
            "history" => "Histórico",
            "settings" => "Configurações",
            "manual" => "Manual",
            "about" => "Sobre",
            _ => "Editor",
        };

        BtnNavEditor.Style = (Style)FindResource(key == "editor" ? "SidebarButtonActive" : "SidebarButton");
        BtnNavHistory.Style = (Style)FindResource(key == "history" ? "SidebarButtonActive" : "SidebarButton");
        BtnNavSettings.Style = (Style)FindResource(key == "settings" ? "SidebarButtonActive" : "SidebarButton");
        BtnNavManual.Style = (Style)FindResource(key == "manual" ? "SidebarButtonActive" : "SidebarButton");
        BtnNavAbout.Style = (Style)FindResource(key == "about" ? "SidebarButtonActive" : "SidebarButton");
        RefreshAdvancedListPanelVisibility();
    }

    private void RefreshAdvancedListPanelVisibility()
    {
        AdvancedListPanel.Visibility = _currentScreen == "editor" && _vm.ShowAdvancedJsonOptions
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void NavEditor_Click(object sender, RoutedEventArgs e) => ShowScreen("editor");
    private void NavHistory_Click(object sender, RoutedEventArgs e) => ShowScreen("history");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowScreen("settings");
    private void NavManual_Click(object sender, RoutedEventArgs e) => ShowScreen("manual");
    private void NavAbout_Click(object sender, RoutedEventArgs e) => ShowScreen("about");

    private void ShowComparison(ComparisonViewModel viewModel)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ShowComparison(viewModel));
            return;
        }

        AppLogger.Info("ListComparison", "Abrindo janela de comparação.");
        ComparisonWindow.ShowDialog(this, viewModel);
        AppLogger.Info("ListComparison", "Janela de comparação fechada.");
    }

    // ---------------------------------------------------------------
    // Theme
    // ---------------------------------------------------------------
    private void ApplyTheme(string themeName)
    {
        if (_isApplyingTheme) return;
        _isApplyingTheme = true;

        var themeFile = themeName switch
        {
            "SISBolt" or "SisBolt Dark" => "UI/Themes/SisBoltTheme.xaml",
            "ListForge Light" => "UI/Themes/LightTheme.xaml",
            _ => "UI/Themes/DarkTheme.xaml",
        };

        var themeUri = new Uri(
            $"pack://application:,,,/{themeFile}",
            UriKind.Absolute);

        var segUri = new Uri(
            "pack://application:,,,/UI/Controls/SegmentedControl.xaml",
            UriKind.Absolute);
        var toggleUri = new Uri(
            "pack://application:,,,/UI/Controls/AnimatedToggleSwitch.xaml",
            UriKind.Absolute);

        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        mergedDicts.Clear();
        mergedDicts.Add(new ResourceDictionary { Source = themeUri });
        mergedDicts.Add(new ResourceDictionary { Source = segUri });
        mergedDicts.Add(new ResourceDictionary { Source = toggleUri });

        RefreshThemeResources();
        _isApplyingTheme = false;
    }

    private void RefreshThemeResources()
    {
        SetResourceReference(BackgroundProperty, "AppBg");
        RootGrid.SetResourceReference(BackgroundProperty, "AppBg");
        SidebarPanel.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "SidebarBg");
        SidebarPanel.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SidebarBorderBrush");
        TopbarPanel.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "TopbarBg");
        TopbarPanel.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush");
        StatusbarPanel.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "TopbarBg");
        StatusbarPanel.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush");
        LogoTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
        LogoSubtitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextMutedBrush");
        AdvancedListTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
        AdvancedListSubtitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextMutedBrush");
        TopbarTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
        StatusLabel.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextMutedBrush");

        RecreateViews();
        ShowScreen(_currentScreen);
    }

    private void RecreateViews()
    {
        ViewsHost.Children.Clear();

        EditorViewControl = new EditorView();
        HistoryViewControl = new HistoryView();
        SettingsViewControl = new SettingsView();
        ManualViewControl = new ManualView();
        AboutViewControl = new AboutView();

        EditorViewControl.SetViewModel(_vm);
        HistoryViewControl.SetViewModel(_vm);
        SettingsViewControl.SetViewModel(_vm);
        AboutViewControl.SetViewModel(_vm);

        ViewsHost.Children.Add(EditorViewControl);
        ViewsHost.Children.Add(HistoryViewControl);
        ViewsHost.Children.Add(SettingsViewControl);
        ViewsHost.Children.Add(ManualViewControl);
        ViewsHost.Children.Add(AboutViewControl);
    }
}
