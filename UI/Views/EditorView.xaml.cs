using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ListForge.Core;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class EditorView : UserControl
{
    private MainViewModel? _vm;

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

        // ---- Keyboard handler ----
        LnbInput.TextKeyDown += TxtInput_KeyDown;

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
            }
        };

        TabJson.Visibility      = vm.ShowJsonSection      ? Visibility.Visible : Visibility.Collapsed;
        BtnCopyJson.Visibility  = vm.ShowCopyJsonButton   ? Visibility.Visible : Visibility.Collapsed;
        BtnGenerateJson.Visibility = vm.ShowGenerateJsonButton ? Visibility.Visible : Visibility.Collapsed;

        // ---- Search highlight ----
        vm.SearchHighlightChanged += (_, _) => ApplySearchHighlight();
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

        LnbOutput.Visibility = tag == "list" ? Visibility.Visible : Visibility.Collapsed;
        LnbJson.Visibility   = tag == "json" ? Visibility.Visible : Visibility.Collapsed;

        if (_vm != null) _vm.SelectedOutputSection = tag;
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
        var idx     = _vm.SearchCurrentIdx;

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
}
