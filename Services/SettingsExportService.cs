using System.Text;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ListForge.Services;

public sealed record SettingsExportSnapshot(AppConfig Config, SizeConfig Sizes, string ApplicationVersion);

public sealed record SettingsImportResult(AppConfig Config, SizeConfig Sizes, string SourceApplicationVersion);

public sealed record SettingsExportDocument(
    int SchemaVersion,
    string Application,
    string ApplicationVersion,
    DateTimeOffset ExportedAt,
    object Settings);

public sealed class SettingsExportService
{
    public const int SchemaVersion = 1;
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
    };

    public OperationResult<string> ExportToFile(string path, SettingsExportSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, BuildJson(snapshot), new UTF8Encoding(false));
            AppLogger.Info("SettingsExport", $"Configurações exportadas: {path}");
            return OperationResult<string>.Ok(path, "Configurações exportadas com sucesso.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsExport", "Falha ao exportar configurações.", ex, path);
            return OperationResult<string>.Fail(
                "Não foi possível exportar as configurações.",
                "Falha ao exportar configurações.",
                ex,
                "SettingsExportFailed");
        }
    }

    public string BuildJson(SettingsExportSnapshot snapshot)
    {
        var document = BuildDocument(snapshot);
        return JsonConvert.SerializeObject(document, SerializerSettings);
    }

    public OperationResult<SettingsImportResult> ImportFromFile(
        string path,
        AppConfig currentConfig,
        SizeConfig currentSizes)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var root = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
            if (root == null)
            {
                return OperationResult<SettingsImportResult>.Fail(
                    "O arquivo de configurações não pôde ser lido.",
                    "Arquivo de configurações vazio ou inválido.",
                    errorCode: "SettingsImportInvalidJson");
            }

            var schemaVersion = ReadInt(root, "schemaVersion", 0);
            var application = ReadString(root, "application", "");
            if (schemaVersion != SchemaVersion || !string.Equals(application, ConfigManager.AppName, StringComparison.Ordinal))
            {
                return OperationResult<SettingsImportResult>.Fail(
                    "O arquivo selecionado não é uma exportação compatível do ListForge.",
                    $"schemaVersion={schemaVersion}; application={application}",
                    errorCode: "SettingsImportUnsupportedDocument");
            }

            var settings = ReadObject(root, "settings");
            if (settings == null)
            {
                return OperationResult<SettingsImportResult>.Fail(
                    "O arquivo selecionado não contém configurações importáveis.",
                    "Nó settings ausente.",
                    errorCode: "SettingsImportMissingSettings");
            }

            var importedConfig = CloneConfig(currentConfig);
            ApplySafeSettings(settings, importedConfig);

            var importedSizes = currentSizes;
            var sizesToken = ReadToken(settings, "sizes");
            if (sizesToken != null && sizesToken.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                importedSizes = SizeHelper.Normalize(sizesToken.ToObject<SizeConfig>() ?? currentSizes);

            var sourceVersion = ReadString(root, "applicationVersion", "");
            AppLogger.Info("SettingsImport", $"Configurações importadas: {path}");
            return OperationResult<SettingsImportResult>.Ok(
                new SettingsImportResult(importedConfig, importedSizes, sourceVersion),
                "Configurações importadas com sucesso.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("SettingsImport", "Falha ao importar configurações.", ex, path);
            return OperationResult<SettingsImportResult>.Fail(
                "Não foi possível importar as configurações.",
                "Falha ao importar configurações.",
                ex,
                "SettingsImportFailed");
        }
    }

    public SettingsExportDocument BuildDocument(SettingsExportSnapshot snapshot) =>
        new(
            SchemaVersion,
            ConfigManager.AppName,
            snapshot.ApplicationVersion,
            DateTimeOffset.Now,
            BuildSafeSettings(snapshot));

    private static object BuildSafeSettings(SettingsExportSnapshot snapshot)
    {
        var config = snapshot.Config;
        return new
        {
            Display = new
            {
                config.ThemeName,
                config.EditorFontSize,
                config.ShowJsonTab,
                config.ShowGenerateJsonButton,
                config.ShowCopyJsonButton,
            },
            Forge = new
            {
                config.ForgeModeEnabled,
                config.ForgeAnvilEnabled,
                config.ForgeHeatEnabled,
                config.ForgeSparksEnabled,
                config.ForgeImpactEnabled,
            },
            Processing = new
            {
                config.DefaultCaseMode,
                config.DefaultInputSeparator,
                config.UseAdvancedJsonPieceMapping,
                config.AdvancedJsonPieceOrder,
                config.AdvancedSaveMode,
                config.WorkProfilesSchemaVersion,
                config.ActiveWorkProfileId,
                config.WorkProfiles,
            },
            Output = new
            {
                config.UseDefaultOutputDir,
                config.UseDefaultListName,
                config.DefaultListName,
                OutputDirExported = false,
            },
            Updates = new
            {
                config.CheckUpdatesOnStartup,
            },
            Sizes = SizeHelper.Normalize(snapshot.Sizes),
        };
    }

    public static string BuildDefaultFileName(string applicationVersion, DateTimeOffset? now = null)
    {
        var stamp = (now ?? DateTimeOffset.Now).ToString("yyyy-MM-dd-HHmmss");
        return $"ListForge-Configuracoes-{applicationVersion}-{stamp}.json";
    }

    private static AppConfig CloneConfig(AppConfig source) =>
        JsonConvert.DeserializeObject<AppConfig>(JsonConvert.SerializeObject(source)) ?? new AppConfig();

    private static void ApplySafeSettings(Newtonsoft.Json.Linq.JObject settings, AppConfig config)
    {
        var display = ReadObject(settings, "display");
        if (display != null)
        {
            config.ThemeName = NormalizeThemeName(ReadString(display, "themeName", config.ThemeName));
            config.EditorFontSize = ClampFontSize(ReadDouble(display, "editorFontSize", config.EditorFontSize));
            config.ShowJsonTab = ReadBool(display, "showJsonTab", config.ShowJsonTab);
            config.ShowGenerateJsonButton = ReadBool(display, "showGenerateJsonButton", config.ShowGenerateJsonButton);
            config.ShowCopyJsonButton = ReadBool(display, "showCopyJsonButton", config.ShowCopyJsonButton);
        }

        var forge = ReadObject(settings, "forge");
        if (forge != null)
        {
            config.ForgeModeEnabled = ReadBool(forge, "forgeModeEnabled", config.ForgeModeEnabled);
            config.ForgeAnvilEnabled = ReadBool(forge, "forgeAnvilEnabled", config.ForgeAnvilEnabled);
            config.ForgeHeatEnabled = ReadBool(forge, "forgeHeatEnabled", config.ForgeHeatEnabled);
            config.ForgeSparksEnabled = ReadBool(forge, "forgeSparksEnabled", config.ForgeSparksEnabled);
            config.ForgeImpactEnabled = ReadBool(forge, "forgeImpactEnabled", config.ForgeImpactEnabled);
        }

        var processing = ReadObject(settings, "processing");
        if (processing != null)
        {
            config.DefaultCaseMode = NormalizeCaseMode(ReadString(processing, "defaultCaseMode", config.DefaultCaseMode));
            var separator = ReadString(processing, "defaultInputSeparator", config.DefaultInputSeparator);
            config.DefaultInputSeparator = string.IsNullOrWhiteSpace(separator) ? "," : separator.Trim();
            config.UseAdvancedJsonPieceMapping = ReadBool(processing, "useAdvancedJsonPieceMapping", config.UseAdvancedJsonPieceMapping);
            config.AdvancedJsonPieceOrder = ReadStringList(processing, "advancedJsonPieceOrder")
                .Select(PieceTypeMapper.NormalizeKey)
                .Where(PieceTypeMapper.IsKnownKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            config.AdvancedSaveMode = NormalizeAdvancedSaveMode(ReadString(processing, "advancedSaveMode", config.AdvancedSaveMode));

            config.WorkProfilesSchemaVersion = ReadInt(processing, "workProfilesSchemaVersion", config.WorkProfilesSchemaVersion);
            config.ActiveWorkProfileId = ReadString(processing, "activeWorkProfileId", config.ActiveWorkProfileId);
            config.WorkProfiles = ReadWorkProfiles(processing, "workProfiles");
        }

        var output = ReadObject(settings, "output");
        if (output != null)
        {
            config.UseDefaultOutputDir = ReadBool(output, "useDefaultOutputDir", config.UseDefaultOutputDir);
            config.UseDefaultListName = ReadBool(output, "useDefaultListName", config.UseDefaultListName);
            var listName = ReadString(output, "defaultListName", config.DefaultListName).Trim();
            config.DefaultListName = string.IsNullOrWhiteSpace(listName) ? "lista" : listName;
        }

        var updates = ReadObject(settings, "updates");
        if (updates != null)
            config.CheckUpdatesOnStartup = ReadBool(updates, "checkUpdatesOnStartup", config.CheckUpdatesOnStartup);

        config.LastOpenedFile = "";
    }

    private static Newtonsoft.Json.Linq.JToken? ReadToken(Newtonsoft.Json.Linq.JObject obj, string name) =>
        obj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static Newtonsoft.Json.Linq.JObject? ReadObject(Newtonsoft.Json.Linq.JObject obj, string name) =>
        ReadToken(obj, name) as Newtonsoft.Json.Linq.JObject;

    private static string ReadString(Newtonsoft.Json.Linq.JObject obj, string name, string fallback) =>
        ReadToken(obj, name)?.Type == Newtonsoft.Json.Linq.JTokenType.String
            ? ReadToken(obj, name)!.ToObject<string>() ?? fallback
            : fallback;

    private static bool ReadBool(Newtonsoft.Json.Linq.JObject obj, string name, bool fallback) =>
        ReadToken(obj, name)?.Type == Newtonsoft.Json.Linq.JTokenType.Boolean
            ? ReadToken(obj, name)!.ToObject<bool>()
            : fallback;

    private static int ReadInt(Newtonsoft.Json.Linq.JObject obj, string name, int fallback) =>
        ReadToken(obj, name)?.Type == Newtonsoft.Json.Linq.JTokenType.Integer
            ? ReadToken(obj, name)!.ToObject<int>()
            : fallback;

    private static double ReadDouble(Newtonsoft.Json.Linq.JObject obj, string name, double fallback)
    {
        var token = ReadToken(obj, name);
        return token?.Type is Newtonsoft.Json.Linq.JTokenType.Float or Newtonsoft.Json.Linq.JTokenType.Integer
            ? token.ToObject<double>()
            : fallback;
    }

    private static List<string> ReadStringList(Newtonsoft.Json.Linq.JObject obj, string name)
    {
        var token = ReadToken(obj, name);
        return token is Newtonsoft.Json.Linq.JArray arr
            ? arr.Values<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList()
            : [];
    }

    private static List<WorkProfile> ReadWorkProfiles(Newtonsoft.Json.Linq.JObject obj, string name)
    {
        var token = ReadToken(obj, name);
        return token is Newtonsoft.Json.Linq.JArray arr
            ? arr.ToObject<List<WorkProfile>>() ?? []
            : [];
    }

    private static double ClampFontSize(double value) => Math.Clamp(value, 8, 32);

    private static string NormalizeCaseMode(string? value) =>
        string.Equals(value, "upper", StringComparison.OrdinalIgnoreCase)
            ? "upper"
            : string.Equals(value, "lower", StringComparison.OrdinalIgnoreCase)
                ? "lower"
                : "original";

    private static string NormalizeAdvancedSaveMode(string? value) =>
        string.Equals(value, "Zip", StringComparison.OrdinalIgnoreCase) ? "Zip" : "LooseFiles";

    private static string NormalizeThemeName(string? themeName) => themeName switch
    {
        "SISBolt" or "SisBolt Dark" => "SISBolt",
        "ListForge Light" => "ListForge Light",
        _ => "ListForge Dark",
    };
}
