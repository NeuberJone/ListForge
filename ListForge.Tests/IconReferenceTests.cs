namespace ListForge.Tests;

public class IconReferenceTests
{
    [Fact]
    public void ApplicationAndInstallerReferenceBundledIcon()
    {
        var root = FindRepositoryRoot();
        var iconPath = Path.Combine(root, "Assets", "logo.ico");
        var projectText = File.ReadAllText(Path.Combine(root, "ListForge.csproj"));
        var installerText = File.ReadAllText(Path.Combine(root, "installer", "ListForge.iss"));

        Assert.True(File.Exists(iconPath));
        Assert.True(new FileInfo(iconPath).Length > 100_000);
        Assert.Contains("<ApplicationIcon>Assets\\logo.ico</ApplicationIcon>", projectText);
        Assert.Contains("SetupIconFile=..\\Assets\\logo.ico", installerText);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ListForge.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do repositório ListForge não encontrada.");
    }
}
