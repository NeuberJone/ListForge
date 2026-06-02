using System.Reflection;
using System.Runtime.InteropServices;
using ListForge.Config;
using ListForge.Core;

namespace ListForge.Services;

public sealed class AboutService
{
    public string ProductName => ConfigManager.AppName;
    public string Version => ResolveAppVersion();
    public string Edition => ConfigManager.EditionName;
    public string LicensedTo => "Não definido";
    public string Author => "Neuber Jone";
    public string Contact => "GitHub: https://github.com/NeuberJone";
    public string ConfigPath => ConfigManager.AppDir;
    public string LogsPath => ConfigManager.LogDir;
    public bool IsTrial => ConfigManager.IsTrialBuild;
    public string LicenseSummary => "Software proprietário. O uso comercial, redistribuição ou customização dependem de autorização prévia.";

    public string TrialStatus =>
        ConfigManager.IsTrialBuild
            ? $"Créditos restantes: {TrialManager.RemainingProcessings}/{TrialManager.Limit} processamento(s)"
            : "Versão completa: sem limite de créditos Trial.";

    public AboutInfo BuildInfo() =>
        new(
            ProductName,
            Version,
            Edition,
            LicensedTo,
            ConfigManager.IsTrialBuild,
            ConfigManager.IsTrialBuild ? TrialManager.RemainingProcessings : 0,
            ConfigManager.IsTrialBuild ? TrialManager.Limit : 0,
            Author,
            Contact,
            ConfigPath,
            LogsPath,
            RuntimeInformation.OSDescription);

    public string BuildSupportText() =>
        AboutInfoBuilder.BuildSupportText(BuildInfo());

    private static string ResolveAppVersion()
    {
        var version = typeof(AboutService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
            version = typeof(AboutService).Assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(version))
            return "não identificada";

        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return version.EndsWith(".0", StringComparison.Ordinal) ? version[..^2] : version;
    }
}
