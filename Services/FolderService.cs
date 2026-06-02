using ListForge.Core;

namespace ListForge.Services;

public static class FolderService
{
    public static bool OpenFolder(string path, string context, string errorMessage, Action<string>? showError = null)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(context, errorMessage, ex, path);
            showError?.Invoke(ex.Message);
            return false;
        }
    }
}
