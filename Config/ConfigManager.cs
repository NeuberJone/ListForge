using System.IO;
using ListForge.Core;
using ListForge.Models;
using Newtonsoft.Json;

namespace ListForge.Config;

public static class ConfigManager
{
    public const string AppName = "ListForge";

#if TRIAL_BUILD
    public const bool IsTrialBuild = true;
    public const string EditionName = "Trial";
#else
    public const bool IsTrialBuild = false;
    public const string EditionName = "Completo";
#endif

    public static readonly string AppTitle = IsTrialBuild ? $"{AppName} Trial" : AppName;
    public static readonly int TrialProcessingLimit = ResolveTrialProcessingLimit();

    public static readonly string AppDir;
    public static readonly string ConfigPath;
    public static readonly string BackupDir;
    public static readonly string LogDir;
    public static readonly string SizeConfigPath;
    public static readonly string TrialStatePath;

    static ConfigManager()
    {
        AppDir = ResolveWritableAppDir();
        ConfigPath = Path.Combine(AppDir, "config.json");
        BackupDir = Path.Combine(AppDir, "backups");
        LogDir = Path.Combine(AppDir, "logs");
        SizeConfigPath = Path.Combine(AppDir, "sizes.json");
        TrialStatePath = Path.Combine(AppDir, "trial-state.json");

        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(LogDir);
    }

    private static int ResolveTrialProcessingLimit()
    {
        var raw = Environment.GetEnvironmentVariable("LISTFORGE_TRIAL_PROCESSING_LIMIT");
        return int.TryParse(raw, out var value) && value > 0 ? value : 10;
    }

    private static string ResolveWritableAppDir()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("APPDATA"),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
        };

        foreach (var root in candidates)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var dir = Path.Combine(root, AppName);
            if (CanUseDirectory(dir))
                return dir;
        }

        return Path.Combine(Path.GetTempPath(), AppName);
    }

    private static bool CanUseDirectory(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------
    // App config
    // ---------------------------------------------------------------
    public static AppConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoadConfig", "Falha ao ler config.json. Usando configuração padrão.", ex, ConfigPath);
            return new AppConfig();
        }
    }

    public static void SaveConfig(AppConfig cfg)
    {
        try
        {
            File.WriteAllText(ConfigPath,
                JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveConfig", "Falha ao salvar config.json.", ex, ConfigPath);
            throw;
        }
    }

    public static AppConfig ResetConfig()
    {
        var cfg = new AppConfig();
        SaveConfig(cfg);
        return cfg;
    }

    // ---------------------------------------------------------------
    // Size config
    // ---------------------------------------------------------------
    public static SizeConfig LoadSizeConfig()
    {
        if (!File.Exists(SizeConfigPath))
        {
            var def = SizeConfig.Default();
            SaveSizeConfig(def);
            return def;
        }

        try
        {
            var json = File.ReadAllText(SizeConfigPath);
            var raw = JsonConvert.DeserializeObject<SizeConfig>(json);
            if (raw == null) throw new Exception("null");
            var normalized = SizeHelper.Normalize(raw);
            SaveSizeConfig(normalized);
            return normalized;
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoadSizeConfig", "Falha ao ler sizes.json. Restaurando tamanhos padrão.", ex, SizeConfigPath);
            var def = SizeConfig.Default();
            SaveSizeConfig(def);
            return def;
        }
    }

    public static void SaveSizeConfig(SizeConfig cfg)
    {
        try
        {
            var normalized = SizeHelper.Normalize(cfg);
            File.WriteAllText(SizeConfigPath,
                JsonConvert.SerializeObject(normalized, Formatting.Indented));
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveSizeConfig", "Falha ao salvar sizes.json.", ex, SizeConfigPath);
            throw;
        }
    }

    public static SizeConfig ResetSizeConfig()
    {
        var cfg = SizeConfig.Default();
        SaveSizeConfig(cfg);
        return cfg;
    }

    // ---------------------------------------------------------------
    // Backup
    // ---------------------------------------------------------------
    public static string CreateBackup(string sourceFile)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"Arquivo não encontrado para backup: {sourceFile}");

        var stem = Path.GetFileNameWithoutExtension(sourceFile);
        var ext = Path.GetExtension(sourceFile);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(BackupDir, $"{stem}_{timestamp}{ext}");
        try
        {
            File.Copy(sourceFile, backupPath, overwrite: false);
        }
        catch (Exception ex)
        {
            AppLogger.Error("CreateBackup", "Falha ao criar backup de arquivo.", ex, sourceFile);
            throw;
        }
        return backupPath;
    }
}
