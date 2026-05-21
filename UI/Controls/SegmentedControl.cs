using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ListForge.UI.Controls;

public delegate void SegmentedSelectionChangedHandler(object sender, string key);

public class SegmentedItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public class SegmentedControl : Control
{
    // ---------------------------------------------------------------
    // Dependency properties
    // ---------------------------------------------------------------
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IList<SegmentedItem>),
            typeof(SegmentedControl), new PropertyMetadata(null, OnItemsChanged));

    public static readonly DependencyProperty SelectedKeyProperty =
        DependencyProperty.Register(nameof(SelectedKey), typeof(string),
            typeof(SegmentedControl), new PropertyMetadata(null, OnSelectedKeyChanged));

    public IList<SegmentedItem>? ItemsSource
    {
        get => (IList<SegmentedItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    // ---------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------
    public event SegmentedSelectionChangedHandler? SelectionChanged;

    // ---------------------------------------------------------------
    // Internal layout
    // ---------------------------------------------------------------
    private readonly UniformGrid _grid = new() { Rows = 1 };
    private readonly Dictionary<string, Border> _buttons = [];
    private readonly ContentControl _presenter;

    public SegmentedControl()
    {
        _presenter = new ContentControl { Content = _grid };
        AddVisualChild(_presenter);
        AddLogicalChild(_presenter);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        return _presenter;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _presenter.Measure(availableSize);
        return _presenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _presenter.Arrange(new Rect(finalSize));
        return finalSize;
    }

    // ---------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------
    private void Rebuild()
    {
        _grid.Children.Clear();
        _buttons.Clear();

        var items = ItemsSource;
        if (items == null || items.Count == 0) return;

        _grid.Columns = items.Count;

        foreach (var item in items)
        {
            var btn = CreateButton(item);
            _buttons[item.Key] = btn;
            _grid.Children.Add(btn);
        }

        RefreshStyles();
    }

    private Border CreateButton(SegmentedItem item)
    {
        var tb = new TextBlock
        {
            Text = item.Label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = TryFindResource("AppFont") as FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = 13,
        };

        var border = new Border
        {
            Child = tb,
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 6, 0),
            BorderThickness = new Thickness(1),
            Tag = item.Key,
        };

        border.MouseLeftButtonDown += (_, _) => Select(item.Key);
        border.MouseEnter += (_, _) => OnHover(item.Key, true);
        border.MouseLeave += (_, _) => OnHover(item.Key, false);

        return border;
    }

    private void OnHover(string key, bool hovering)
    {
        if (key == SelectedKey || !_buttons.TryGetValue(key, out var btn)) return;

        var hoverBg = TryFindResource("PanelHover") as Brush ?? Brushes.DimGray;
        var altBg = TryFindResource("PanelAlt") as Brush ?? Brushes.DarkSlateGray;
        var textBrush = TryFindResource("TextBrush") as Brush ?? Brushes.White;
        var mutedBrush = TryFindResource("TextMutedBrush") as Brush ?? Brushes.Gray;

        btn.Background = hovering ? hoverBg : altBg;
        if (btn.Child is TextBlock tb)
            tb.Foreground = hovering ? textBrush : mutedBrush;
    }

    private void RefreshStyles()
    {
        var primaryBg = TryFindResource("PrimaryBrush") as Brush ?? Brushes.DodgerBlue;
        var altBg = TryFindResource("PanelAlt") as Brush ?? Brushes.DarkSlateGray;
        var borderBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
        var textBrush = TryFindResource("TextBrush") as Brush ?? Brushes.White;
        var mutedBrush = TryFindResource("TextMutedBrush") as Brush ?? Brushes.Gray;

        foreach (var (key, btn) in _buttons)
        {
            var selected = key == SelectedKey;
            btn.Background = selected ? primaryBg : altBg;
            btn.BorderBrush = borderBrush;
            if (btn.Child is TextBlock tb)
                tb.Foreground = selected ? Brushes.White : mutedBrush;
        }
    }

    public void Select(string key, bool invoke = true)
    {
        SetValue(SelectedKeyProperty, key);
        RefreshStyles();
        if (invoke) SelectionChanged?.Invoke(this, key);
    }

    // ---------------------------------------------------------------
    // Property change callbacks
    // ---------------------------------------------------------------
    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SegmentedControl)d).Rebuild();

    private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SegmentedControl)d).RefreshStyles();
}
