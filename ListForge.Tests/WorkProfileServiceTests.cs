using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class WorkProfileServiceTests
{
    [Fact]
    public void EnsureProfiles_CreatesDefaultFromCurrentSettings()
    {
        var service = new WorkProfileService();
        var config = new AppConfig { DefaultInputSeparator = ";", DefaultCaseMode = "upper" };

        service.EnsureProfiles(config, service.CaptureFromConfig(config, "Crescente"));

        var profile = Assert.Single(config.WorkProfiles);
        Assert.Equal(WorkProfileService.DefaultProfileId, profile.Id);
        Assert.Equal("Padrão", profile.Name);
        Assert.True(profile.IsDefault);
        Assert.Equal(";", profile.Settings.DefaultInputSeparator);
        Assert.Equal("upper", profile.Settings.DefaultCaseMode);
        Assert.Equal("Crescente", profile.Settings.EditorSortMode);
        Assert.Equal(profile.Id, config.ActiveWorkProfileId);
    }

    [Fact]
    public void CreateProfile_UsesCurrentSettingsAndRejectsDuplicateNames()
    {
        var service = new WorkProfileService();
        var config = CreateConfigWithDefault(service);
        var settings = new WorkProfileSettings
        {
            DefaultInputSeparator = "|",
            DefaultCaseMode = "lower",
            EditorSortMode = "Decrescente",
            UseAdvancedJsonPieceMapping = true,
            AdvancedJsonPieceOrder = ["ShortSleeve", "Pants"],
            UseDefaultOutputDir = true,
            OutputDir = @"C:\Saidas",
        };

        var created = service.CreateProfile(config, "Futebol", settings);
        var duplicate = service.CreateProfile(config, "futebol", settings);

        Assert.True(created.Success);
        Assert.Equal("Futebol", created.Value!.Name);
        Assert.NotEqual(WorkProfileService.DefaultProfileId, created.Value.Id);
        Assert.Equal(created.Value.Id, config.ActiveWorkProfileId);
        Assert.Equal("|", created.Value.Settings.DefaultInputSeparator);
        Assert.Equal("Decrescente", created.Value.Settings.EditorSortMode);
        Assert.Equal(["ShortSleeve", "Pants"], created.Value.Settings.AdvancedJsonPieceOrder);
        Assert.False(duplicate.Success);
        Assert.Equal("WorkProfileDuplicateName", duplicate.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProfile_RejectsEmptyNames(string name)
    {
        var service = new WorkProfileService();
        var config = CreateConfigWithDefault(service);

        var result = service.CreateProfile(config, name, new WorkProfileSettings());

        Assert.False(result.Success);
        Assert.Equal("WorkProfileEmptyName", result.ErrorCode);
    }

    [Fact]
    public void SaveActiveProfile_UpdatesSettingsOnlyWhenRequested()
    {
        var service = new WorkProfileService();
        var config = CreateConfigWithDefault(service);
        var created = service.CreateProfile(config, "Pedido", new WorkProfileSettings { DefaultInputSeparator = "," });

        var changed = new WorkProfileSettings { DefaultInputSeparator = ";", DefaultCaseMode = "upper" };
        Assert.True(service.HasUnsavedChanges(config, changed));

        var saved = service.SaveActiveProfile(config, changed);

        Assert.True(saved.Success);
        Assert.False(service.HasUnsavedChanges(config, changed));
        Assert.Equal(";", created.Value!.Settings.DefaultInputSeparator);
        Assert.Equal("upper", created.Value.Settings.DefaultCaseMode);
    }

    [Fact]
    public void RenameDuplicateDeleteAndRestore_ProtectDefaultProfile()
    {
        var service = new WorkProfileService();
        var config = CreateConfigWithDefault(service);
        var created = service.CreateProfile(config, "Lista avançada", new WorkProfileSettings()).Value!;

        var renamed = service.RenameProfile(config, created.Id, "Lista produção");
        var duplicated = service.DuplicateProfile(config, created.Id);
        var deleteDefault = service.DeleteProfile(config, WorkProfileService.DefaultProfileId);
        var deleted = service.DeleteProfile(config, created.Id);
        var restored = service.RestoreDefaultProfile(config);

        Assert.True(renamed.Success);
        Assert.Equal("Lista produção", created.Name);
        Assert.True(duplicated.Success);
        Assert.NotEqual(created.Id, duplicated.Value!.Id);
        Assert.False(deleteDefault.Success);
        Assert.Equal("WorkProfileDefaultDelete", deleteDefault.ErrorCode);
        Assert.True(deleted.Success);
        Assert.True(restored.Success);
        Assert.Contains(config.WorkProfiles, p => p.Id == WorkProfileService.DefaultProfileId);
        Assert.Equal(WorkProfileService.DefaultProfileId, config.ActiveWorkProfileId);
    }

    private static AppConfig CreateConfigWithDefault(WorkProfileService service)
    {
        var config = new AppConfig();
        service.EnsureProfiles(config, service.CaptureFromConfig(config, "Original"));
        return config;
    }
}
