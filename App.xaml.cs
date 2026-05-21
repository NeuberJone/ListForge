using System.Runtime.InteropServices;
using System.Windows;

namespace ListForge;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);

    protected override void OnStartup(StartupEventArgs e)
    {
        try { SetCurrentProcessExplicitAppUserModelID("NeuberJone.ListForge.1"); }
        catch { /* not critical */ }

        base.OnStartup(e);
    }
}
