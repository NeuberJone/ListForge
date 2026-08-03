using Newtonsoft.Json;

namespace ListForge.Models;

public sealed class WorkProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public WorkProfileSettings Settings { get; set; } = new();
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string DisplayName { get; set; } = "";

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
}

public sealed class WorkProfileSettings
{
    public string DefaultInputSeparator { get; set; } = ",";
    public string DefaultCaseMode { get; set; } = "original";
    public string EditorSortMode { get; set; } = "Original";
    public bool UseAdvancedJsonPieceMapping { get; set; }
    public List<string> AdvancedJsonPieceOrder { get; set; } = [];
    public string AdvancedSaveMode { get; set; } = "LooseFiles";
    public bool UseDefaultOutputDir { get; set; }
    public string OutputDir { get; set; } = "";
    public bool UseDefaultListName { get; set; }
    public string DefaultListName { get; set; } = "lista";
}
