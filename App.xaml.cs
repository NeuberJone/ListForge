using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace ListForge;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try { SetCurrentProcessExplicitAppUserModelID("NeuberJone.ListForge.1"); }
        catch { /* not critical */ }

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var message = BuildExceptionMessage(e.Exception);
        MessageBox.Show(message, "ListForge", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string BuildExceptionMessage(Exception exception)
    {
        var root = exception.GetBaseException();
        if (ReferenceEquals(root, exception))
            return root.Message;

        var sb = new StringBuilder();
        sb.AppendLine(root.Message);
        sb.AppendLine();
        sb.AppendLine($"Detalhe técnico: {exception.GetType().Name}");
        return sb.ToString().Trim();
    }
}
