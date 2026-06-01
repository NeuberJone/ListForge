namespace ListForge.Models;

public sealed class AppConfig
{
    public bool ShowJsonTab { get; set; } = false;
    public bool ShowGenerateJsonButton { get; set; } = false;
    public bool ShowCopyJsonButton { get; set; } = false;
    public bool UseDefaultOutputDir { get; set; } = false;
    public string OutputDir { get; set; } = "";
    public bool UseDefaultListName { get; set; } = false;
    public string DefaultListName { get; set; } = "lista";
    public string DefaultCaseMode { get; set; } = "original";
    public string DefaultInputSeparator { get; set; } = ",";
    public string ThemeName { get; set; } = "ListForge Dark";
    public double EditorFontSize { get; set; } = 13;
    public string LastOpenedFile { get; set; } = "";
}
