using System.Diagnostics;

namespace ListForge.Services;

public interface IUpdateProcessLauncher
{
    bool StartInstaller(string installerPath, string arguments);
    bool OpenUrl(string url);
    bool OpenFolder(string folderPath);
}

public sealed class UpdateProcessLauncher : IUpdateProcessLauncher
{
    public bool StartInstaller(string installerPath, string arguments)
    {
        var startInfo = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = arguments,
        };

        return Process.Start(startInfo) != null;
    }

    public bool OpenUrl(string url)
    {
        var startInfo = new ProcessStartInfo(url)
        {
            UseShellExecute = true,
        };

        return Process.Start(startInfo) != null;
    }

    public bool OpenFolder(string folderPath)
    {
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"\"{folderPath}\"",
        };

        return Process.Start(startInfo) != null;
    }
}
