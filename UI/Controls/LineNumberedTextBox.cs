using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ListForge.UI.Controls;

/// <summary>
/// A TextBox wrapped with a synchronized line-number gutter.
/// Exposes the inner TextBox as InnerTextBox so code-behind can bind/access it.
/// </summary>
public class LineNumberedTextBox : Control
{
    static LineNumberedTextBox()
    {
        FontSizeProperty.OverrideMetadata(
            typeof(LineNumberedTextBox),
            new FrameworkPropertyMetadata(
                13.0,
                FrameworkPropertyMetadataOptions.Inherits,
                OnFontSizeChanged,
                CoerceFontSize));
    }

    // ---------------------------------------------------------------
    // Parts
    // ---------------------------------------------------------------
    private TextBox? _textBox;
    private Canvas? _gutter;
    private ScrollViewer? _scroller;

    private const double GutterWidth = 48;
    private const double LineHeight = 18.0; // approximation; recalculated at runtime

    // ---------------------------------------------------------------
    // Dependency properties — mirror essential TextBox props
    // ---------------------------------------------------------------
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(LineNumberedTextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(LineNumberedTextBox),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    public static readonly DependencyProperty UndoLimitProperty =
        DependencyProperty.Register(nameof(UndoLimit), typeof(int), typeof(LineNumberedTextBox),
            new PropertyMetadata(200));

    public static readonly DependencyProperty HighlightedLineNumbersProperty =
        DependencyProperty.Register(nameof(HighlightedLineNumbers), typeof(IEnumerable<int>), typeof(LineNumberedTextBox),
            new PropertyMetadata(Array.Empty<int>(), OnHighlightedLineNumbersChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public int UndoLimit
    {
        get => (int)GetValue(UndoLimitProperty);
        set => SetValue(UndoLimitProperty, value);
    }

    public IEnumerable<int> HighlightedLineNumbers
    {
        get => (IEnumerable<int>)GetValue(HighlightedLineNumbersProperty);
        set => SetValue(HighlightedLineNumbersProperty, value);
    }

    // ---------------------------------------------------------------
    // Events forwarded from inner TextBox
    // ---------------------------------------------------------------
    public event KeyEventHandler? TextKeyDown;

    // ---------------------------------------------------------------
    // Public access to inner TextBox for code-behind that needs Select/ScrollToLine
    // ---------------------------------------------------------------
    public TextBox? InnerTextBox => _textBox;

    // ---------------------------------------------------------------
    // Build visual tree manually (no XAML template dependency)
    // ---------------------------------------------------------------
    private readonly Grid _root = new();
    private bool _suppressUpdate;

    public LineNumberedTextBox()
    {
        PreviewMouseWheel += OnPreviewMouseWheel;

        // Columns: gutter | separator | editor
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GutterWidth) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Gutter background
        var gutterBorder = new Border
        {
            Background = Brushes.Transparent,
        };
        gutterBorder.SetResourceReference(Border.BackgroundProperty, "EditorBg");
        Grid.SetColumn(gutterBorder, 0);
        _root.Children.Add(gutterBorder);

        // Gutter canvas for line numbers
        _gutter = new Canvas { ClipToBounds = true };
        _gutter.SetResourceReference(Canvas.BackgroundProperty, "EditorBg");
        Grid.SetColumn(_gutter, 0);
        _root.Children.Add(_gutter);

        // Separator line
        var sep = new Border { Width = 1 };
        sep.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
        Grid.SetColumn(sep, 1);
        _root.Children.Add(sep);

        // The actual TextBox
        _textBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            UndoLimit = 200,
        };
        _textBox.SetResourceReference(TextBox.BackgroundProperty, "EditorBg");
        _textBox.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");
        _textBox.SetResourceReference(TextBox.CaretBrushProperty, "TextBrush");
        _textBox.SetResourceReference(TextBox.SelectionBrushProperty, "SelectionBrush");
        _textBox.SetResourceReference(TextBox.FontFamilyProperty, "MonoFont");
        _textBox.FontSize = FontSize;

        _textBox.TextChanged += OnInnerTextChanged;
        _textBox.SizeChanged += (_, _) => UpdateGutter();
        _textBox.KeyDown += (s, e) => TextKeyDown?.Invoke(s, e);
        _textBox.PreviewMouseWheel += OnPreviewMouseWheel;

        Grid.SetColumn(_textBox, 2);
        _root.Children.Add(_textBox);

        // Hook into the TextBox scroll viewer after template is applied
        _textBox.Loaded += (_, _) =>
        {
            _scroller = GetScrollViewer(_textBox);
            if (_scroller != null)
                _scroller.ScrollChanged += (_, _) => UpdateGutter();
            UpdateGutter();
        };

        AddVisualChild(_root);
        AddLogicalChild(_root);
    }

    // ---------------------------------------------------------------
    // Layout overrides
    // ---------------------------------------------------------------
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        return _root.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    // ---------------------------------------------------------------
    // Property callbacks
    // ---------------------------------------------------------------
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (LineNumberedTextBox)d;
        if (self._textBox == null || self._suppressUpdate) return;
        self._suppressUpdate = true;
        var caret = self._textBox.CaretIndex;
        self._textBox.Text = (string)(e.NewValue ?? "");
        self._textBox.CaretIndex = Math.Min(caret, self._textBox.Text.Length);
        self._suppressUpdate = false;
        self.UpdateGutter();
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (LineNumberedTextBox)d;
        if (self._textBox != null)
            self._textBox.IsReadOnly = (bool)e.NewValue;
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (LineNumberedTextBox)d;
        if (self._textBox != null)
            self._textBox.FontSize = (double)e.NewValue;
        self.UpdateGutter();
    }

    private static void OnHighlightedLineNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (LineNumberedTextBox)d;
        self.UpdateGutter();
    }

    private static object CoerceFontSize(DependencyObject d, object baseValue)
    {
        var size = baseValue is double value ? value : 13;
        return Math.Clamp(double.IsNaN(size) ? 13 : size, 8, 32);
    }

    private void OnInnerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUpdate) return;
        _suppressUpdate = true;
        SetValue(TextProperty, _textBox!.Text);
        _suppressUpdate = false;
        UpdateGutter();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        var delta = e.Delta > 0 ? 1 : -1;
        SetCurrentValue(FontSizeProperty, Math.Clamp(FontSize + delta, 8, 32));
        e.Handled = true;
    }

    // ---------------------------------------------------------------
    // Gutter rendering
    // ---------------------------------------------------------------
    private void UpdateGutter()
    {
        if (_gutter == null || _textBox == null) return;

        _gutter.Children.Clear();

        var lineCount = _textBox.LineCount;
        if (lineCount <= 0) return;

        int firstLine;
        int lastLine;
        try
        {
            firstLine = Math.Max(0, _textBox.GetFirstVisibleLineIndex());
            lastLine = Math.Min(lineCount - 1, _textBox.GetLastVisibleLineIndex());
        }
        catch
        {
            firstLine = 0;
            lastLine = lineCount - 1;
        }

        var mutedBrush = (TryFindResource("TextMutedBrush") as Brush)
            ?? new SolidColorBrush(Color.FromRgb(0x8E, 0xA3, 0xC7));
        var highlightBrush = (TryFindResource("WarningBrush") as Brush)
            ?? new SolidColorBrush(Color.FromRgb(0xF5, 0xB8, 0x4B));
        var highlightedLines = new HashSet<int>(HighlightedLineNumbers ?? Array.Empty<int>());

        for (int i = firstLine; i <= lastLine; i++)
        {
            double lineTop;
            try
            {
                var charIdx = _textBox.GetCharacterIndexFromLineIndex(i);
                lineTop = _textBox.GetRectFromCharacterIndex(charIdx).Top;
            }
            catch
            {
                continue;
            }

            var tb = new TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = highlightedLines.Contains(i + 1) ? highlightBrush : mutedBrush,
                FontFamily = _textBox.FontFamily,
                FontSize = _textBox.FontSize,
                Width = GutterWidth - 8,
                TextAlignment = TextAlignment.Right,
                FontWeight = highlightedLines.Contains(i + 1) ? FontWeights.Bold : FontWeights.Normal,
            };

            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, lineTop);
            _gutter.Children.Add(tb);
        }
    }

    // ---------------------------------------------------------------
    // Helper: extract internal ScrollViewer from TextBox
    // ---------------------------------------------------------------
    private static ScrollViewer? GetScrollViewer(DependencyObject obj)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is ScrollViewer sv) return sv;
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    // ---------------------------------------------------------------
    // Public helpers (used by EditorView code-behind)
    // ---------------------------------------------------------------
    public void Select(int start, int length) => _textBox?.Select(start, length);

    public void ScrollToLine(int lineIndex)
    {
        if (_textBox == null) return;
        try
        {
            var idx = _textBox.GetCharacterIndexFromLineIndex(
                Math.Min(lineIndex, _textBox.LineCount - 1));
            _textBox.CaretIndex = idx;
            _textBox.ScrollToLine(lineIndex);
        }
        catch { }
    }

    public int GetLineIndexFromCharacterIndex(int charIndex)
    {
        try { return _textBox?.GetLineIndexFromCharacterIndex(charIndex) ?? 0; }
        catch { return 0; }
    }

    public new void Focus() => _textBox?.Focus();
}
