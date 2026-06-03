using System.Reflection;
using System.Runtime.InteropServices;
using ListForge.Config;
using ListForge.Core;

namespace ListForge.Services;

public sealed class AboutService
{
    private readonly ILicenseService _licenseService;

    public AboutService()
        : this(new LocalTrialLicenseService())
    {
    }

    public AboutService(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public string ProductName => ConfigManager.AppName;
    public string Version => ResolveAppVersion();
    public string Edition => _licenseService.Edition;
    public string LicensedTo => "Não definido";
    public string Author => "Neuber Jone";
    public string Contact => "GitHub: https://github.com/NeuberJone";
    public string ConfigPath => ConfigManager.AppDir;
    public string LogsPath => ConfigManager.LogDir;
    public bool IsTrial => _licenseService.IsTrial;
    public string LicenseSummary => "Software proprietário. O uso comercial, redistribuição ou customização dependem de autorização prévia.";

    public string TrialStatus =>
        _licenseService.IsTrial
            ? $"Créditos restantes: {_licenseService.RemainingProcessings}/{_licenseService.ProcessingLimit} processamento(s)"
            : "Versão completa: sem limite de créditos Trial.";

    public AboutInfo BuildInfo() =>
        new(
            ProductName,
            Version,
            Edition,
            LicensedTo,
            _licenseService.IsTrial,
            _licenseService.IsTrial ? _licenseService.RemainingProcessings : 0,
            _licenseService.IsTrial ? _licenseService.ProcessingLimit : 0,
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
