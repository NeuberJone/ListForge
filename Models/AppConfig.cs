using Newtonsoft.Json;

namespace ListForge.Models;

public sealed class AppConfig
{
    public bool ShowJsonTab { get; set; } = false;
    public bool ShowGenerateJsonButton { get; set; } = false;
    public bool ShowCopyJsonButton { get; set; } = false;
    public bool UseAdvancedJsonPieceMapping { get; set; } = false;
    public List<string> AdvancedJsonPieceOrder { get; set; } = [];
    public string AdvancedSaveMode { get; set; } = "LooseFiles";
    public bool UseDefaultOutputDir { get; set; } = false;
    public string OutputDir { get; set; } = "";
    public bool UseDefaultListName { get; set; } = false;
    public string DefaultListName { get; set; } = "lista";
    public string DefaultCaseMode { get; set; } = "original";
    public string DefaultInputSeparator { get; set; } = ",";
    public string ThemeName { get; set; } = "ListForge Dark";
    public double EditorFontSize { get; set; } = 13;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string LastUpdateAvailability { get; set; } = "";
    public string LastAvailableUpdateVersion { get; set; } = "";
    public string LastAvailableUpdateTagName { get; set; } = "";
    public string LastAvailableUpdateReleaseUrl { get; set; } = "";
    public string LastAvailableUpdateNotes { get; set; } = "";
    public string LastAvailableUpdateInstallerName { get; set; } = "";
    public string LastAvailableUpdateInstallerUrl { get; set; } = "";
    public long LastAvailableUpdateInstallerSizeBytes { get; set; }
    public string LastAvailableUpdateInstallerSha256 { get; set; } = "";
    public string LastAvailableUpdateChecksumsName { get; set; } = "";
    public string LastAvailableUpdateChecksumsUrl { get; set; } = "";
    public long LastAvailableUpdateChecksumsSizeBytes { get; set; }
    public string LastAvailableUpdateChecksumsSha256 { get; set; } = "";
    [JsonIgnore]
    public string LastOpenedFile { get; set; } = "";
}
