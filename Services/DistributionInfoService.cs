using System.Reflection;
using ListForge.Config;
using ListForge.Models;

namespace ListForge.Services;

public sealed class DistributionInfoService
{
    public const string MetadataKey = "ListForgeDistribution";

    private readonly Assembly _assembly;
    private readonly DistributionKind? _overrideKind;
    private readonly bool? _overrideTrial;

    public DistributionInfoService()
        : this(typeof(DistributionInfoService).Assembly)
    {
    }

    public DistributionInfoService(
        Assembly assembly,
        DistributionKind? overrideKind = null,
        bool? overrideTrial = null)
    {
        _assembly = assembly;
        _overrideKind = overrideKind;
        _overrideTrial = overrideTrial;
    }

    public DistributionInfo GetCurrentDistribution()
    {
        var isTrial = _overrideTrial ?? ConfigManager.IsTrialBuild;
        var kind = _overrideKind ?? ResolveKindFromMetadata(_assembly);

        if (isTrial)
            kind = DistributionKind.TrialPortableOneFile;

        return FromKind(kind, isTrial);
    }

    public static DistributionInfo FromKind(DistributionKind kind, bool isTrial = false)
    {
        if (isTrial)
            kind = DistributionKind.TrialPortableOneFile;

        return kind switch
        {
            DistributionKind.Installed => new(kind, "Completo instalado", false, true),
            DistributionKind.PortableOneFile => new(kind, "Completo portátil", false, false),
            DistributionKind.TrialPortableOneFile => new(kind, "Trial portátil", true, false),
            _ => new(DistributionKind.Development, "Desenvolvimento", false, false),
        };
    }

    internal static DistributionKind ResolveKindFromMetadata(Assembly assembly)
    {
        var value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => string.Equals(attr.Key, MetadataKey, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return ParseKind(value);
    }

    internal static DistributionKind ParseKind(string? value) =>
        value?.Trim() switch
        {
            "Installed" => DistributionKind.Installed,
            "PortableOneFile" => DistributionKind.PortableOneFile,
            "TrialPortableOneFile" => DistributionKind.TrialPortableOneFile,
            _ => DistributionKind.Development,
        };
}
