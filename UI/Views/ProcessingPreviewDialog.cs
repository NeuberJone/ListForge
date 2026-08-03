using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ListForge.Models;

namespace ListForge.UI.Views;

public sealed class ProcessingPreviewDialog : Window
{
    private readonly Button _processButton;

    private ProcessingPreviewDialog(ProcessingPreview preview)
    {
        Title = "Prévia do processamento";
        Width = 680;
        MinHeight = 520;
        MaxHeight = 720;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ResourceBrush("AppBg");

        var root = new DockPanel { Margin = new Thickness(18) };

        var title = new TextBlock
        {
            Text = "Prévia do processamento",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = ResourceBrush("TextBrush"),
            Margin = new Thickness(0, 0, 0, 12),
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        _processButton = new Button
        {
            Content = "Processar agora",
            Style = ResourceStyle("AccentButton"),
            MinWidth = 140,
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = preview.CanProcess,
            IsDefault = preview.CanProcess,
        };
        _processButton.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(_processButton);

        var cancelButton = new Button
        {
            Content = "Voltar",
            Style = ResourceStyle("StdButton"),
            MinWidth = 100,
            IsCancel = true,
        };
        cancelButton.Click += (_, _) => { DialogResult = false; };
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent(preview),
        };
        root.Children.Add(scroll);
        Content = root;
    }

    public static bool ShowDialog(Window owner, ProcessingPreview preview)
    {
        var dialog = new ProcessingPreviewDialog(preview)
        {
            Owner = owner,
        };
        return dialog.ShowDialog() == true;
    }

    private StackPanel BuildContent(ProcessingPreview preview)
    {
        var content = new StackPanel();
        content.Children.Add(BuildSection("Resumo", [
            $"{preview.TotalRecords} registro(s) analisado(s)",
            $"{preview.ValidRecords} registro(s) válido(s)",
            $"{preview.WarningRecords} registro(s) com possível problema",
            $"{preview.InvalidRecords} registro(s) inválido(s)",
            $"Perfil de trabalho: {preview.Snapshot.ActiveWorkProfileName}",
            $"Lista avançada: {(preview.Snapshot.AdvancedListEnabled ? "ativa" : "inativa")}",
        ]));

        content.Children.Add(BuildSection("Tamanhos",
            preview.Sizes.Count == 0
                ? ["Nenhum tamanho processável encontrado."]
                : preview.Sizes.Select(item => $"{item.Size}: {item.Count}").ToArray()));

        content.Children.Add(BuildSection("Tipos de peça",
            preview.PieceTypes.Count == 0
                ? ["Nenhum tipo de peça processável encontrado."]
                : preview.PieceTypes.Select(item => $"{item.PieceType}: {item.Count}").ToArray()));

        content.Children.Add(BuildSection("Saída", [
            $"Pasta: {preview.Snapshot.OutputDirectoryDescription}",
            $"Arquivo: {preview.Snapshot.OutputFileDescription}",
            "Formato: Lista organizada e Prévia JSON no editor; arquivos são gerados apenas nas ações de salvar.",
        ]));

        if (preview.Warnings.Count > 0)
            content.Children.Add(BuildSection("Avisos", preview.Warnings));

        if (preview.Issues.Count > 0)
        {
            content.Children.Add(BuildSection("Detalhes dos problemas",
                preview.Issues
                    .Take(30)
                    .Select(issue => $"Linha {issue.LineNumber} — {SeverityLabel(issue.Severity)}: {issue.Message}. {issue.SuggestedAction}")
                    .ToArray()));
        }

        if (!preview.CanProcess)
        {
            content.Children.Add(BuildSection("Confirmação", [
                "Nenhum registro válido foi encontrado para processamento ou existem registros inválidos.",
                "Volte para corrigir a entrada antes de confirmar.",
            ]));
        }

        return content;
    }

    private Border BuildSection(string title, IReadOnlyList<string> lines)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = ResourceBrush("TextBrush"),
            Margin = new Thickness(0, 0, 0, 6),
        });

        foreach (var line in lines)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "• " + line,
                Foreground = ResourceBrush("TextMutedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3),
            });
        }

        return new Border
        {
            Style = ResourceStyle("Card"),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(14, 10, 14, 10),
            Child = panel,
        };
    }

    private static string SeverityLabel(ProcessingIssueSeverity severity) =>
        severity == ProcessingIssueSeverity.Warning ? "Possível problema" : "Inválido";

    private static Brush ResourceBrush(string key) =>
        (Brush)Application.Current.Resources[key];

    private static Style ResourceStyle(string key) =>
        (Style)Application.Current.Resources[key];
}
