using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ListForge.Config;
using ListForge.Core;

namespace ListForge;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try { SetCurrentProcessExplicitAppUserModelID("NeuberJone.ListForge.1"); }
        catch { /* not critical */ }

        AppLogger.Info("Startup", "Aplicação iniciada.");

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("DispatcherUnhandledException", "Exceção não tratada no dispatcher WPF.", e.Exception);
        var message = BuildExceptionMessage(e.Exception);
        MessageBox.Show(message, ConfigManager.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            AppLogger.Error("UnhandledException", "Exceção não tratada no domínio da aplicação.", ex);
        else
            AppLogger.Error("UnhandledException", $"Exceção não tratada sem objeto Exception: {e.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("UnobservedTaskException", "Exceção não observada em tarefa assíncrona.", e.Exception);
        e.SetObserved();
    }

    private static string BuildExceptionMessage(Exception exception)
    {
        var root = exception.GetBaseException();
        return ReferenceEquals(root, exception)
            ? root.Message
            : $"{root.Message}\n\nO detalhe técnico foi registrado no log interno do ListForge.";
    }
}
