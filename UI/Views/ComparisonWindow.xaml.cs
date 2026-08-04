using System.Windows.Documents;
using ListForge.Models;
using ListForge.UI.Controls;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class ComparisonWindow : Window
{
    private const double CompactLayoutWidth = 1000;
    private readonly ComparisonViewModel _viewModel;

    public ComparisonWindow(ComparisonViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.SelectionRequested += HighlightSelectedRecord;

        Loaded += (_, _) =>
        {
            ApplyStatusTheme();
            UpdateResponsiveLayout(ActualWidth);
            HighlightSelectedRecord(_viewModel.SelectedItem);
        };
        SizeChanged += (_, args) => UpdateResponsiveLayout(args.NewSize.Width);
        Closed += (_, _) => _viewModel.SelectionRequested -= HighlightSelectedRecord;
    }

    public static bool? ShowDialog(Window owner, ComparisonViewModel viewModel)
    {
        var window = new ComparisonWindow(viewModel) { Owner = owner };
        return window.ShowDialog();
    }

    private void UpdateResponsiveLayout(double width)
    {
        var compact = width < CompactLayoutWidth;
        WideComparisonGrid.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactComparisonTabs.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyStatusTheme()
    {
        var backgroundKey = _viewModel.Summary.HasCriticalDifferences ? "AlertBgBrush" : "PanelAlt";
        var borderKey = _viewModel.Summary.HasCriticalDifferences ? "AlertBorderBrush" : "BorderBrush";
        var foregroundKey = _viewModel.Summary.HasCriticalDifferences ? "AlertTextBrush" : "TextBrush";

        ComparisonStatusBorder.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        ComparisonStatusBorder.SetResourceReference(Border.BorderBrushProperty, borderKey);
        ComparisonStatusBorder.SetResourceReference(TextElement.ForegroundProperty, foregroundKey);
        ComparisonStatusBorder.BorderThickness = new Thickness(1);
    }

    private void ComparisonItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        HighlightSelectedRecord(_viewModel.SelectedItem);

    private void HighlightSelectedRecord(ComparisonItem? item)
    {
        if (item == null)
            return;

        HighlightLine(WideInputText, item.InputLineNumber);
        HighlightLine(CompactInputText, item.InputLineNumber);
        HighlightLine(WideOutputText, item.OutputLineNumber);
        HighlightLine(CompactOutputText, item.OutputLineNumber);
        ComparisonItemsGrid.ScrollIntoView(item);
    }

    private static void HighlightLine(LineNumberedTextBox editor, int? lineNumber)
    {
        editor.HighlightedLineNumbers = lineNumber.HasValue ? [lineNumber.Value] : [];
        if (lineNumber.HasValue)
            editor.ScrollToLine(Math.Max(0, lineNumber.Value - 1));
    }

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_viewModel.BuildClipboardReport());
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
