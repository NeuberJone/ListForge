using System.IO;
using ListForge.Config;
using ListForge.Core;
using Newtonsoft.Json;

namespace ListForge.Tests;

public class TrialManagerTests
{
    [Fact]
    public void NewTrialInstallationStartsWithConfiguredLimit()
    {
        using var env = TrialTestEnvironment.Create(limit: 3);

        Assert.True(TrialManager.IsTrial);
        Assert.Equal(3, TrialManager.RemainingProcessings);
        Assert.True(TrialManager.HasCredits);
    }

    [Fact]
    public void TrialCreditConsumptionPersistsInInternalStorage()
    {
        using var env = TrialTestEnvironment.Create(limit: 3);

        TrialManager.ConsumeSuccessfulProcessing();
        TrialManager.ConsumeSuccessfulProcessing();

        Assert.Equal(1, TrialManager.RemainingProcessings);
        Assert.True(File.Exists(ConfigManager.TrialStatePath));
        Assert.DoesNotContain("UsedProcessings", File.ReadAllText(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void TrialBlocksProcessingWhenLimitIsExhausted()
    {
        using var env = TrialTestEnvironment.Create(limit: 1);

        TrialManager.ConsumeSuccessfulProcessing();

        Assert.False(TrialManager.HasCredits);
        Assert.Throws<InvalidOperationException>(() => TrialManager.ConsumeSuccessfulProcessing());
    }

    [Fact]
    public void LegacyTrialStateIsMigratedPreservingUsedProcessings()
    {
        using var env = TrialTestEnvironment.Create(limit: 5);
        File.WriteAllText(
            ConfigManager.LegacyTrialStatePath,
            JsonConvert.SerializeObject(new { UsedProcessings = 2 }, Formatting.Indented));

        Assert.Equal(3, TrialManager.RemainingProcessings);
        Assert.True(File.Exists(ConfigManager.TrialStatePath));
        Assert.False(File.Exists(ConfigManager.LegacyTrialStatePath));
    }

    [Fact]
    public void CorruptedInternalStateDoesNotResetTrialCredits()
    {
        using var env = TrialTestEnvironment.Create(limit: 4);
        Directory.CreateDirectory(ConfigManager.InternalStateDir);
        File.WriteAllText(ConfigManager.TrialStatePath, "invalid state");

        Assert.False(TrialManager.HasCredits);
        Assert.Equal(0, TrialManager.RemainingProcessings);
    }

    [Fact]
    public void TrialLogsDoNotIncludeSensitiveStatePath()
    {
        using var env = TrialTestEnvironment.Create(limit: 4);
        Directory.CreateDirectory(ConfigManager.InternalStateDir);
        File.WriteAllText(ConfigManager.TrialStatePath, "invalid state");

        _ = TrialManager.HasCredits;

        var logText = File.ReadAllText(Directory.GetFiles(env.LogDir, "listforge-*.log").Single());
        Assert.DoesNotContain(ConfigManager.TrialStatePath, logText);
        Assert.DoesNotContain(ConfigManager.InternalStateDir, logText);
        Assert.DoesNotContain(Path.GetFileName(ConfigManager.TrialStatePath), logText);
    }

    [Fact]
    public void CompleteEditionDoesNotConsumeTrialCreditsOrCreateState()
    {
        using var env = TrialTestEnvironment.Create(isTrial: false, limit: 1);

        TrialManager.ConsumeSuccessfulProcessing();

        Assert.Equal(int.MaxValue, TrialManager.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    private sealed class TrialTestEnvironment : IDisposable
    {
        private readonly string _root;

        private TrialTestEnvironment(string root, string logDir)
        {
            _root = root;
            LogDir = logDir;
        }

        public string LogDir { get; }

        public static TrialTestEnvironment Create(bool isTrial = true, int limit = 10)
        {
            var root = Path.Combine(Path.GetTempPath(), $"listforge-trial-test-{Guid.NewGuid():N}");
            var appDir = Path.Combine(root, "app");
            var stateDir = Path.Combine(root, "state");
            var logDir = Path.Combine(root, "logs");

            ConfigManager.SetDirectoriesForTesting(appDir, stateDir);
            AppLogger.SetLogDirectoryForTesting(logDir);
            TrialManager.SetTrialModeForTesting(isTrial, limit);

            return new TrialTestEnvironment(root, logDir);
        }

        public void Dispose()
        {
            TrialManager.SetTrialModeForTesting(null);
            AppLogger.SetLogDirectoryForTesting(null);

            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
