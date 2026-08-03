using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;

namespace ListForge.Services;

public sealed class WorkProfileService
{
    public const int SchemaVersion = 1;
    public const string DefaultProfileId = "default";
    public const string DefaultProfileName = "Padrão";
    public const int MaxProfileNameLength = 60;

    public void EnsureProfiles(AppConfig config, WorkProfileSettings currentSettings)
    {
        config.WorkProfilesSchemaVersion = SchemaVersion;
        config.WorkProfiles ??= [];

        NormalizeProfiles(config.WorkProfiles);

        var defaultProfile = config.WorkProfiles.FirstOrDefault(p => p.IsDefault || p.Id == DefaultProfileId);
        if (defaultProfile == null)
        {
            defaultProfile = new WorkProfile
            {
                Id = DefaultProfileId,
                Name = DefaultProfileName,
                IsDefault = true,
                Settings = NormalizeSettings(CloneSettings(currentSettings)),
            };
            config.WorkProfiles.Insert(0, defaultProfile);
        }
        else
        {
            defaultProfile.Id = DefaultProfileId;
            defaultProfile.Name = DefaultProfileName;
            defaultProfile.IsDefault = true;
            defaultProfile.Settings = NormalizeSettings(defaultProfile.Settings);
        }

        if (string.IsNullOrWhiteSpace(config.ActiveWorkProfileId)
            || config.WorkProfiles.All(p => !string.Equals(p.Id, config.ActiveWorkProfileId, StringComparison.Ordinal)))
        {
            config.ActiveWorkProfileId = defaultProfile.Id;
        }
    }

    public WorkProfile? GetActiveProfile(AppConfig config) =>
        config.WorkProfiles.FirstOrDefault(p => string.Equals(p.Id, config.ActiveWorkProfileId, StringComparison.Ordinal))
        ?? config.WorkProfiles.FirstOrDefault(p => p.IsDefault || p.Id == DefaultProfileId);

    public OperationResult<WorkProfile> CreateProfile(AppConfig config, string name, WorkProfileSettings settings)
    {
        var validation = ValidateName(config.WorkProfiles, name);
        if (!validation.Success)
            return OperationResult<WorkProfile>.Fail(validation.UserMessage, validation.TechnicalMessage, errorCode: validation.ErrorCode);

        var now = DateTimeOffset.UtcNow;
        var profile = new WorkProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Settings = NormalizeSettings(CloneSettings(settings)),
            SchemaVersion = SchemaVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        config.WorkProfiles.Add(profile);
        config.ActiveWorkProfileId = profile.Id;
        return OperationResult<WorkProfile>.Ok(profile, "Perfil criado com sucesso.");
    }

    public OperationResult RenameProfile(AppConfig config, string profileId, string name)
    {
        var profile = FindProfile(config, profileId);
        if (profile == null)
            return OperationResult.Fail("Perfil não encontrado.", "Perfil ausente.", errorCode: "WorkProfileMissing");

        if (profile.IsDefault || profile.Id == DefaultProfileId)
            return OperationResult.Fail("O perfil Padrão não pode ser renomeado.", "Tentativa de renomear perfil padrão.", errorCode: "WorkProfileDefaultRename");

        var validation = ValidateName(config.WorkProfiles.Where(p => !ReferenceEquals(p, profile)), name);
        if (!validation.Success)
            return validation;

        profile.Name = name.Trim();
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return OperationResult.Ok("Perfil renomeado com sucesso.");
    }

    public OperationResult<WorkProfile> DuplicateProfile(AppConfig config, string profileId)
    {
        var profile = FindProfile(config, profileId);
        if (profile == null)
            return OperationResult<WorkProfile>.Fail("Perfil não encontrado.", "Perfil ausente.", errorCode: "WorkProfileMissing");

        var now = DateTimeOffset.UtcNow;
        var copy = new WorkProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = BuildUniqueCopyName(config.WorkProfiles, profile.Name),
            Settings = CloneSettings(profile.Settings),
            SchemaVersion = SchemaVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        config.WorkProfiles.Add(copy);
        config.ActiveWorkProfileId = copy.Id;
        return OperationResult<WorkProfile>.Ok(copy, "Perfil duplicado com sucesso.");
    }

    public OperationResult DeleteProfile(AppConfig config, string profileId)
    {
        var profile = FindProfile(config, profileId);
        if (profile == null)
            return OperationResult.Fail("Perfil não encontrado.", "Perfil ausente.", errorCode: "WorkProfileMissing");

        if (profile.IsDefault || profile.Id == DefaultProfileId)
            return OperationResult.Fail("O perfil Padrão não pode ser excluído.", "Tentativa de excluir perfil padrão.", errorCode: "WorkProfileDefaultDelete");

        config.WorkProfiles.Remove(profile);
        if (string.Equals(config.ActiveWorkProfileId, profile.Id, StringComparison.Ordinal))
            config.ActiveWorkProfileId = DefaultProfileId;

        return OperationResult.Ok("Perfil excluído com sucesso.");
    }

    public OperationResult SaveActiveProfile(AppConfig config, WorkProfileSettings settings)
    {
        var profile = GetActiveProfile(config);
        if (profile == null)
            return OperationResult.Fail("Perfil ativo não encontrado.", "Perfil ativo ausente.", errorCode: "WorkProfileMissing");

        profile.Settings = NormalizeSettings(CloneSettings(settings));
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        config.ActiveWorkProfileId = profile.Id;
        return OperationResult.Ok("Alterações salvas no perfil.");
    }

    public OperationResult RestoreDefaultProfile(AppConfig config)
    {
        var defaultProfile = config.WorkProfiles.FirstOrDefault(p => p.IsDefault || p.Id == DefaultProfileId);
        if (defaultProfile == null)
            return OperationResult.Fail("Perfil Padrão não encontrado.", "Perfil padrão ausente.", errorCode: "WorkProfileDefaultMissing");

        defaultProfile.Settings = CaptureOfficialDefaultSettings();
        defaultProfile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        config.ActiveWorkProfileId = defaultProfile.Id;
        return OperationResult.Ok("Perfil Padrão restaurado.");
    }

    public bool HasUnsavedChanges(AppConfig config, WorkProfileSettings currentSettings)
    {
        var active = GetActiveProfile(config);
        return active != null && !SettingsEqual(active.Settings, currentSettings);
    }

    public WorkProfileSettings CaptureFromConfig(AppConfig config, string editorSortMode) =>
        NormalizeSettings(new WorkProfileSettings
        {
            DefaultInputSeparator = config.DefaultInputSeparator,
            DefaultCaseMode = config.DefaultCaseMode,
            EditorSortMode = editorSortMode,
            UseAdvancedJsonPieceMapping = config.UseAdvancedJsonPieceMapping,
            AdvancedJsonPieceOrder = config.AdvancedJsonPieceOrder.ToList(),
            AdvancedSaveMode = config.AdvancedSaveMode,
            UseDefaultOutputDir = config.UseDefaultOutputDir,
            OutputDir = config.OutputDir,
            UseDefaultListName = config.UseDefaultListName,
            DefaultListName = config.DefaultListName,
        });

    public static WorkProfileSettings CaptureOfficialDefaultSettings() =>
        new()
        {
            DefaultInputSeparator = ",",
            DefaultCaseMode = "original",
            EditorSortMode = "Original",
            UseAdvancedJsonPieceMapping = false,
            AdvancedJsonPieceOrder = [],
            AdvancedSaveMode = "LooseFiles",
            UseDefaultOutputDir = false,
            OutputDir = "",
            UseDefaultListName = false,
            DefaultListName = "lista",
        };

    private static WorkProfile? FindProfile(AppConfig config, string profileId) =>
        config.WorkProfiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.Ordinal));

    private static OperationResult ValidateName(IEnumerable<WorkProfile> profiles, string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return OperationResult.Fail("Informe um nome para o perfil.", "Nome vazio.", errorCode: "WorkProfileEmptyName");

        if (trimmed.Length > MaxProfileNameLength)
            return OperationResult.Fail($"Use no máximo {MaxProfileNameLength} caracteres no nome do perfil.", "Nome longo demais.", errorCode: "WorkProfileNameTooLong");

        if (trimmed.Any(char.IsControl))
            return OperationResult.Fail("O nome do perfil contém caracteres inválidos.", "Nome contém caractere de controle.", errorCode: "WorkProfileInvalidName");

        if (profiles.Any(p => string.Equals(p.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
            return OperationResult.Fail("Já existe um perfil com esse nome.", "Nome duplicado.", errorCode: "WorkProfileDuplicateName");

        return OperationResult.Ok();
    }

    private static void NormalizeProfiles(List<WorkProfile> profiles)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in profiles.ToList())
        {
            profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id.Trim();
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Perfil" : profile.Name.Trim();
            profile.Settings = NormalizeSettings(profile.Settings);
            profile.SchemaVersion = SchemaVersion;

            if (!seenIds.Add(profile.Id))
                profiles.Remove(profile);
        }
    }

    private static WorkProfileSettings NormalizeSettings(WorkProfileSettings? settings)
    {
        settings ??= new WorkProfileSettings();
        settings.DefaultInputSeparator = string.IsNullOrWhiteSpace(settings.DefaultInputSeparator)
            ? ","
            : settings.DefaultInputSeparator.Trim();
        settings.DefaultCaseMode = NormalizeCaseMode(settings.DefaultCaseMode);
        settings.EditorSortMode = NormalizeSortLabel(settings.EditorSortMode);
        settings.AdvancedJsonPieceOrder = settings.AdvancedJsonPieceOrder
            .Select(PieceTypeMapper.NormalizeKey)
            .Where(PieceTypeMapper.IsKnownKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.AdvancedSaveMode = string.Equals(settings.AdvancedSaveMode, "Zip", StringComparison.OrdinalIgnoreCase)
            ? "Zip"
            : "LooseFiles";
        settings.OutputDir = settings.OutputDir.Trim();
        settings.DefaultListName = string.IsNullOrWhiteSpace(settings.DefaultListName) ? "lista" : settings.DefaultListName.Trim();
        return settings;
    }

    private static string NormalizeCaseMode(string? value) =>
        string.Equals(value, "upper", StringComparison.OrdinalIgnoreCase)
            ? "upper"
            : string.Equals(value, "lower", StringComparison.OrdinalIgnoreCase)
                ? "lower"
                : "original";

    private static string NormalizeSortLabel(string? value) =>
        string.Equals(value, "Crescente", StringComparison.OrdinalIgnoreCase)
            ? "Crescente"
            : string.Equals(value, "Decrescente", StringComparison.OrdinalIgnoreCase)
                ? "Decrescente"
                : "Original";

    private static string BuildUniqueCopyName(IEnumerable<WorkProfile> profiles, string baseName)
    {
        var root = $"{baseName} - Cópia";
        var names = profiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains(root))
            return root;

        for (var i = 2; ; i++)
        {
            var candidate = $"{root} {i}";
            if (!names.Contains(candidate))
                return candidate;
        }
    }

    private static WorkProfileSettings CloneSettings(WorkProfileSettings settings) =>
        JsonConvert.DeserializeObject<WorkProfileSettings>(JsonConvert.SerializeObject(settings)) ?? new WorkProfileSettings();

    private static bool SettingsEqual(WorkProfileSettings left, WorkProfileSettings right) =>
        JsonConvert.SerializeObject(NormalizeSettings(CloneSettings(left))) ==
        JsonConvert.SerializeObject(NormalizeSettings(CloneSettings(right)));
}
