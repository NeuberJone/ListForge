using System.Windows;
using System.Windows.Controls;

namespace ListForge.UI.Views;

public class InputDialog : Window
{
    private readonly TextBox _textBox;
    public string Result { get; private set; } = "";

    public InputDialog(string prompt, string title = "ListForge", string defaultValue = "")
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)Application.Current.Resources["AppBg"];

        var stack = new StackPanel { Margin = new Thickness(18) };

        stack.Children.Add(new TextBlock
        {
            Text = prompt,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        _textBox = new TextBox
        {
            Text = defaultValue,
            Style = (Style)Application.Current.Resources["FormEntry"],
            Margin = new Thickness(0, 0, 0, 14),
        };
        _textBox.SelectAll();
        stack.Children.Add(_textBox);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var btnOk = new Button
        {
            Content = "OK",
            Style = (Style)Application.Current.Resources["AccentButton"],
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };
        btnOk.Click += (_, _) => { Result = _textBox.Text; DialogResult = true; };

        var btnCancel = new Button
        {
            Content = "Cancelar",
            Style = (Style)Application.Current.Resources["StdButton"],
            Width = 80,
            IsCancel = true,
        };
        btnCancel.Click += (_, _) => { DialogResult = false; };

        btnRow.Children.Add(btnOk);
        btnRow.Children.Add(btnCancel);
        stack.Children.Add(btnRow);

        Content = stack;
        Loaded += (_, _) => _textBox.Focus();
    }

    public static string? Show(string prompt, string title = "ListForge", string defaultValue = "")
    {
        var dlg = new InputDialog(prompt, title, defaultValue)
        {
            Owner = Application.Current.MainWindow,
        };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }
}
