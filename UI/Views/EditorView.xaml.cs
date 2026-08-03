using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using ListForge.Core;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class EditorView : UserControl
{
    private const int MaxForgeParticlesPerLayer = 72;

    private MainViewModel? _vm;
    private readonly Random _forgeRandom = new();
    private readonly Dictionary<TextBox, ForgeEditorSparkState> _forgeSparkStates = [];

    public EditorView() => InitializeComponent();

    public void SetViewModel(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;

        // ---- Input text (two-way) ----
        var inputBinding = new System.Windows.Data.Binding(nameof(vm.InputText))
        {
            Source = vm,
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
        };
        LnbInput.SetBinding(ListForge.UI.Controls.LineNumberedTextBox.TextProperty, inputBinding);

        // ---- Output texts (one-way) ----
        LnbOutput.SetBinding(ListForge.UI.Controls.LineNumberedTextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.OutputText)) { Source = vm });
        LnbJson.SetBinding(ListForge.UI.Controls.LineNumberedTextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.JsonText)) { Source = vm });

        // ---- Separator / case ----
        TxtSeparator.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.EditorSeparator))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });

        CmbCaseMode.ItemsSource = vm.CaseLabels;
        CmbCaseMode.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(vm.EditorCaseLabel))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });

        CmbSortMode.ItemsSource = vm.SortLabels;
        CmbSortMode.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(vm.EditorSortLabel))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });

        CmbBulkSock.ItemsSource = vm.SockSizeOptions;
        CmbBulkSock.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(vm.SelectedSockSize))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });

        // ---- Search fields ----
        TxtFind.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.FindText))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });
        TxtReplace.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.ReplaceText))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });
        ChkMatchCase.SetBinding(CheckBox.IsCheckedProperty,
            new System.Windows.Data.Binding(nameof(vm.FindMatchCase))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });

        // ---- File label ----
        LblCurrentFile.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.CurrentFileLabel)) { Source = vm });
        CmbWorkProfile.ItemsSource = vm.WorkProfiles;
        CmbWorkProfile.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(vm.SelectedWorkProfile))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });
        TglAdvancedList.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new System.Windows.Data.Binding(nameof(vm.AdvancedListEnabled))
            {
                Source = vm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });

        // ---- Keyboard handler ----
        LnbInput.TextKeyDown += TxtInput_KeyDown;
        RegisterForgeSparkEditor(LnbInput.InnerTextBox, ForgeInputSparks, requireEditable: false);
        RegisterForgeSparkEditor(LnbOutput.InnerTextBox, ForgeOutputSparks, requireEditable: true);
        RegisterForgeSparkEditor(LnbJson.InnerTextBox, ForgeJsonSparks, requireEditable: true);

        // ---- Visibility reactions ----
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(vm.ShowJsonSection):
                    TabJson.Visibility = vm.ShowJsonSection ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(vm.ShowCopyJsonButton):
                    BtnCopyJson.Visibility = vm.ShowCopyJsonButton ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(vm.ShowGenerateJsonButton):
                    BtnGenerateJson.Visibility = vm.ShowGenerateJsonButton ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(vm.ShowAdvancedEditorOptions):
                    RefreshAdvancedEditorOptionsVisibility(vm);
                    break;
                case nameof(vm.ShowAdvancedSaveButton):
                    RefreshAdvancedEditorOptionsVisibility(vm);
                    break;
                case nameof(vm.ForgeEffectPulse):
                    PlayForgeProcessEffect(vm);
                    break;
            }
        };

        TabJson.Visibility = vm.ShowJsonSection ? Visibility.Visible : Visibility.Collapsed;
        BtnCopyJson.Visibility = vm.ShowCopyJsonButton ? Visibility.Visible : Visibility.Collapsed;
        BtnGenerateJson.Visibility = vm.ShowGenerateJsonButton ? Visibility.Visible : Visibility.Collapsed;
        RefreshAdvancedEditorOptionsVisibility(vm);
        RefreshOutputSparkLayers();
        // ---- Search highlight ----
        vm.SearchHighlightChanged += (_, _) => ApplySearchHighlight();
    }

    private void RefreshAdvancedEditorOptionsVisibility(MainViewModel vm)
    {
        PnlBulkAppend.Visibility = Visibility.Visible;
        BtnAdvancedSave.Visibility = vm.ShowAdvancedSaveButton ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RegisterForgeSparkEditor(TextBox? textBox, Canvas canvas, bool requireEditable)
    {
        if (textBox == null)
            return;

        _forgeSparkStates[textBox] = new ForgeEditorSparkState(canvas, textBox.Text.Length, requireEditable);
        textBox.TextChanged += ForgeEditor_TextChanged;
    }

    private void ForgeEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_vm == null || !_vm.ForgeModeEnabled || !_vm.ForgeSparksEnabled)
            return;

        if (sender is not TextBox textBox || !_forgeSparkStates.TryGetValue(textBox, out var state))
            return;

        var textLength = textBox.Text.Length;
        if (textLength <= state.LastTextLength)
        {
            state.LastTextLength = textLength;
            return;
        }

        state.LastTextLength = textLength;

        if (state.RequireEditable && textBox.IsReadOnly)
            return;

        var now = DateTime.UtcNow;
        if ((now - state.LastSparkUtc).TotalMilliseconds < 80)
            return;

        state.LastSparkUtc = now;
        PulseTextBoxGlow(textBox);
        SpawnTypingSparks(textBox, state.Canvas);
    }

    private void ProcessButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null || !_vm.ForgeModeEnabled)
            return;

        PlayProcessButtonPulse();
        PulseProcessButtonGlow();
        if (_vm.ForgeSparksEnabled)
            SpawnButtonSparks(count: 4, intensity: 0.85);
    }

    private void PlayForgeProcessEffect(MainViewModel vm)
    {
        if (!vm.ForgeModeEnabled)
            return;

        if (vm.ForgeImpactEnabled)
        {
            PlayProcessButtonPulse();
            PulseProcessButtonGlow();
        }

        if (vm.ForgeHeatEnabled)
            PulseWindowHeat();

        if (vm.ForgeSparksEnabled)
            SpawnButtonSparks(count: 6, intensity: 1.0);
    }

    private void PlayProcessButtonPulse()
    {
        ProcessButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, PulseScaleAnimation(1.0, 1.095, 150));
        ProcessButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, PulseScaleAnimation(1.0, 1.095, 150));
    }

    private void PulseProcessButtonGlow()
    {
        var glow = new DropShadowEffect
        {
            Color = Color.FromRgb(255, 142, 36),
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0,
        };
        BtnProcess.Effect = glow;

        var animation = PulseOpacityAnimation(0.0, 0.72, 320);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(BtnProcess.Effect, glow))
                BtnProcess.Effect = null;
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
    }

    private void PulseWindowHeat()
    {
        ForgeWindowHeat.BeginAnimation(OpacityProperty, PulseOpacityAnimation(0.0, 1.0, 680));
    }

    private void PulseTextBoxGlow(TextBox textBox)
    {
        var glow = new DropShadowEffect
        {
            Color = Color.FromRgb(255, 135, 35),
            BlurRadius = 14,
            ShadowDepth = 0,
            Opacity = 0,
        };
        textBox.Effect = glow;

        var animation = PulseOpacityAnimation(0.0, 0.32, 210);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(textBox.Effect, glow))
                textBox.Effect = null;
        };
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
    }

    private void SpawnTypingSparks(TextBox? textBox, Canvas canvas, int count = 2, double intensity = 0.8)
    {
        if (textBox == null || canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0 || canvas.Visibility != Visibility.Visible)
            return;

        var origin = GetCaretSparkOrigin(textBox, canvas);
        for (var i = 0; i < count; i++)
            AddSpark(canvas, origin.X, origin.Y, intensity);
    }

    private void SpawnButtonSparks(int count, double intensity)
    {
        if (ForgeWindowSparks.ActualWidth <= 0 || ForgeWindowSparks.ActualHeight <= 0)
            return;

        var center = BtnProcess.TranslatePoint(new Point(BtnProcess.ActualWidth * 0.5, BtnProcess.ActualHeight * 0.5), ForgeWindowSparks);
        for (var i = 0; i < count; i++)
            AddSpark(ForgeWindowSparks, center.X, center.Y, intensity);
    }

    private Point GetCaretSparkOrigin(TextBox textBox, Canvas canvas)
    {
        var caretRect = textBox.GetRectFromCharacterIndex(textBox.CaretIndex, true);
        if (caretRect.IsEmpty)
            return new Point(72, 28);

        var caretPoint = textBox.TranslatePoint(new Point(caretRect.Right, caretRect.Top + caretRect.Height * 0.45), canvas);
        return new Point(
            Math.Clamp(caretPoint.X, 56, Math.Max(56, canvas.ActualWidth - 16)),
            Math.Clamp(caretPoint.Y, 12, Math.Max(12, canvas.ActualHeight - 16)));
    }

    private void AddSpark(Canvas canvas, double originX, double originY, double intensity)
    {
        TrimForgeLayer(canvas);

        var length = (_forgeRandom.NextDouble() * 8 + 5) * Math.Clamp(intensity, 0.7, 1.35);
        var spark = new Rectangle
        {
            Width = length,
            Height = 1.4,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(_forgeRandom.Next(0, 3) switch
            {
                0 => Color.FromRgb(255, 229, 132),
                1 => Color.FromRgb(255, 165, 54),
                _ => Color.FromRgb(239, 82, 28),
            }),
            Opacity = 0.95,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 128, 24),
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 0.55,
            },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(_forgeRandom.Next(-38, 39)),
        };

        Canvas.SetLeft(spark, originX);
        Canvas.SetTop(spark, originY);
        canvas.Children.Add(spark);

        var direction = (_forgeRandom.NextDouble() * Math.PI * 1.55) - (Math.PI * 0.75);
        var distance = (_forgeRandom.NextDouble() * 24 + 16) * Math.Clamp(intensity, 0.75, 1.35);
        var targetX = originX + Math.Cos(direction) * distance;
        var targetY = originY + Math.Sin(direction) * distance - (8 * intensity);
        var duration = TimeSpan.FromMilliseconds(_forgeRandom.Next(180, 330));

        var storyboard = new Storyboard();
        storyboard.Children.Add(SparkAnimation(spark, Canvas.LeftProperty, targetX, duration));
        storyboard.Children.Add(SparkAnimation(spark, Canvas.TopProperty, targetY, duration));
        storyboard.Children.Add(SparkAnimation(spark, OpacityProperty, 0.0, duration));
        BeginStoryboardWithCleanup(storyboard, canvas, spark, 650);
    }

    private void AddEmber(Canvas canvas, double originX, double originY, double intensity)
    {
        TrimForgeLayer(canvas);

        var size = (_forgeRandom.NextDouble() * 2.5 + 2) * Math.Clamp(intensity, 0.75, 1.25);
        var ember = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush(
                Color.FromRgb(255, 235, 170),
                _forgeRandom.Next(0, 2) == 0 ? Color.FromRgb(255, 123, 35) : Color.FromRgb(201, 52, 24)),
            Opacity = 0.58,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 110, 24),
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.45,
            },
        };

        Canvas.SetLeft(ember, originX);
        Canvas.SetTop(ember, originY);
        canvas.Children.Add(ember);

        var driftX = (_forgeRandom.NextDouble() * 30 - 15) * Math.Clamp(intensity, 0.75, 1.25);
        var driftY = -(_forgeRandom.NextDouble() * 34 + 20) * Math.Clamp(intensity, 0.75, 1.25);
        var duration = TimeSpan.FromMilliseconds(_forgeRandom.Next(360, 560));

        var storyboard = new Storyboard();
        storyboard.Children.Add(SparkAnimation(ember, Canvas.LeftProperty, originX + driftX, duration));
        storyboard.Children.Add(SparkAnimation(ember, Canvas.TopProperty, originY + driftY, duration));
        storyboard.Children.Add(SparkAnimation(ember, OpacityProperty, 0.0, duration));
        BeginStoryboardWithCleanup(storyboard, canvas, ember, 800);
    }

    private static void BeginStoryboardWithCleanup(Storyboard storyboard, Canvas canvas, UIElement element, int cleanupMilliseconds)
    {
        var removed = false;
        void Remove()
        {
            if (removed)
                return;

            removed = true;
            element.BeginAnimation(OpacityProperty, null);
            canvas.Children.Remove(element);
        }

        storyboard.Completed += (_, _) => Remove();
        storyboard.Begin();

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(cleanupMilliseconds),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Remove();
        };
        timer.Start();
    }

    private static void TrimForgeLayer(Canvas canvas)
    {
        while (canvas.Children.Count >= MaxForgeParticlesPerLayer)
            canvas.Children.RemoveAt(0);
    }

    private static DoubleAnimation SparkAnimation(
        DependencyObject target,
        DependencyProperty property,
        double to,
        TimeSpan duration)
    {
        var animation = new DoubleAnimation(to, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        return animation;
    }

    private static DoubleAnimation PulseScaleAnimation(double from, double to, double milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            AutoReverse = true,
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 },
        };

    private static DoubleAnimation PulseOpacityAnimation(double from, double to, double milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

    private void ExtractFromLinkMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu == null || !button.IsEnabled)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    // ---------------------------------------------------------------
    // Keyboard shortcuts on the input editor
    // ---------------------------------------------------------------
    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            LnbInput.InnerTextBox?.Undo();
            e.Handled = true;
        }
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _vm?.ProcessCommand.Execute(null);
            e.Handled = true;
        }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TxtFind.Focus();
            e.Handled = true;
        }
    }

    private void AdvancedListToggle_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && sender is System.Windows.Controls.Primitives.ToggleButton toggle
            && toggle.IsEnabled)
        {
            toggle.IsChecked = toggle.IsChecked != true;
            e.Handled = true;
        }
    }

    private void AppendSelected_Click(object sender, RoutedEventArgs e) =>
        AppendTokensToInput(applyToAll: false);

    private void AppendAll_Click(object sender, RoutedEventArgs e) =>
        AppendTokensToInput(applyToAll: true);

    private void AppendTokensToInput(bool applyToAll)
    {
        if (_vm == null) return;

        var tokens = new System.Collections.Generic.List<string>();
        if (ChkApplySock.IsChecked == true)
            tokens.Add((_vm.SelectedSockSize ?? "").Trim());
        if (ChkApplySize.IsChecked == true)
            tokens.Add((TxtBulkSize.Text ?? "").Trim());

        tokens = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
        {
            MessageBox.Show("Marque e informe um tamanho, um meião, ou os dois para adicionar.", "ListForge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sep = ListProcessor.NormalizeSeparator(_vm.EditorSeparator);
        var textBox = LnbInput.InnerTextBox;
        var originalText = _vm.InputText ?? "";
        var lines = originalText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();
        if (lines.Count == 0) lines.Add("");

        var firstLine = 0;
        var lastLine = lines.Count - 1;

        if (!applyToAll && textBox != null)
        {
            firstLine = textBox.GetLineIndexFromCharacterIndex(textBox.SelectionStart);
            var selectionEnd = textBox.SelectionLength > 0
                ? Math.Max(textBox.SelectionStart, textBox.SelectionStart + textBox.SelectionLength - 1)
                : textBox.SelectionStart;
            lastLine = textBox.GetLineIndexFromCharacterIndex(selectionEnd);
        }

        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);

        var changed = 0;
        for (var i = firstLine; i <= lastLine; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            lines[i] = lines[i].TrimEnd() + sep + string.Join(sep, tokens);
            changed++;
        }

        if (changed == 0)
        {
            _vm.StatusText = "Nenhuma linha com conteudo para alterar.";
            return;
        }

        _vm.InputText = string.Join("\n", lines);
        _vm.ClearSearchHighlight(keepStatus: true);
        var label = string.Join(" + ", tokens);
        _vm.StatusText = applyToAll
            ? $"{label} adicionado em {changed} linha(s)."
            : $"{label} adicionado na selecao atual.";

        if (textBox != null)
        {
            LnbInput.Focus();
            LnbInput.ScrollToLine(firstLine);
        }
    }

    // ---------------------------------------------------------------
    // Output tab switching
    // ---------------------------------------------------------------
    private void OutputTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputTabs.SelectedItem is not TabItem tab) return;
        var tag = tab.Tag as string ?? "list";

        if (_vm != null && !_vm.TryLeaveOutputSection(tag))
        {
            foreach (var item in OutputTabs.Items.OfType<TabItem>())
            {
                if (string.Equals(item.Tag as string, _vm.SelectedOutputSection, StringComparison.OrdinalIgnoreCase))
                {
                    OutputTabs.SelectedItem = item;
                    break;
                }
            }
            return;
        }

        LnbOutput.Visibility = tag == "list" ? Visibility.Visible : Visibility.Collapsed;
        LnbJson.Visibility = tag == "json" ? Visibility.Visible : Visibility.Collapsed;
        RefreshOutputSparkLayers();

        if (_vm != null) _vm.SelectedOutputSection = tag;
    }

    private void RefreshOutputSparkLayers()
    {
        var isJson = OutputTabs.SelectedItem is TabItem tab
            && string.Equals(tab.Tag as string, "json", StringComparison.OrdinalIgnoreCase);

        ForgeOutputSparks.Visibility = isJson ? Visibility.Collapsed : Visibility.Visible;
        ForgeJsonSparks.Visibility = isJson ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------
    // Scroll to error line
    // ---------------------------------------------------------------
    public void ScrollToLine(int lineNumber)
    {
        if (lineNumber < 1) return;
        LnbInput.ScrollToLine(lineNumber - 1);
    }

    // ---------------------------------------------------------------
    // Search highlight — select the current match in the input editor
    // ---------------------------------------------------------------
    private void ApplySearchHighlight()
    {
        if (_vm == null) return;

        var matches = _vm.SearchMatches;
        var idx = _vm.SearchCurrentIdx;

        if (matches.Count == 0 || idx < 0 || idx >= matches.Count)
        {
            if (!string.IsNullOrEmpty(_vm.FindText) && matches.Count == 0)
                _vm.StatusText = $"Não encontrado: \"{_vm.FindText}\"";
            return;
        }

        var (start, length) = matches[idx];
        LnbInput.Focus();
        LnbInput.Select(start, length);
        LnbInput.ScrollToLine(LnbInput.GetLineIndexFromCharacterIndex(start));

        _vm.StatusText = $"Resultado {idx + 1} de {matches.Count}   \"{_vm.FindText}\"";
    }

    private sealed class ForgeEditorSparkState(Canvas canvas, int lastTextLength, bool requireEditable)
    {
        public Canvas Canvas { get; } = canvas;
        public int LastTextLength { get; set; } = lastTextLength;
        public bool RequireEditable { get; } = requireEditable;
        public DateTime LastSparkUtc { get; set; } = DateTime.MinValue;
    }
}
