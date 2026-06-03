using ListForge.Config;
using ListForge.Core;
using ListForge.Services;

namespace ListForge.Tests;

public class LocalTrialLicenseServiceTests
{
    [Fact]
    public void CompleteEditionDoesNotConsumeCreditsOrCreateTrialState()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: false, limit: 1);
        var service = new LocalTrialLicenseService();

        service.ConsumeSuccessfulProcessing();

        Assert.False(service.IsTrial);
        Assert.Equal("Completo", service.Edition);
        Assert.True(service.CanProcess);
        Assert.Equal(int.MaxValue, service.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void TrialEditionReportsLimitAndRemainingCredits()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: true, limit: 3);
        var service = new LocalTrialLicenseService();

        Assert.True(service.IsTrial);
        Assert.Equal("Trial", service.Edition);
        Assert.Equal(3, service.ProcessingLimit);
        Assert.Equal(3, service.RemainingProcessings);
        Assert.True(service.CanProcess);
        Assert.Contains("Trial: 3/3", service.ProcessingStatusSuffix);
    }

    [Fact]
    public void SuccessfulProcessingConsumesCreditInTrial()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: true, limit: 2);
        var service = new LocalTrialLicenseService();

        service.ConsumeSuccessfulProcessing();

        Assert.Equal(1, service.RemainingProcessings);
        Assert.True(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void ValidationErrorDoesNotConsumeCreditWhenServiceIsNotCalled()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: true, limit: 2);
        var service = new LocalTrialLicenseService();

        _ = service.CanProcess;

        Assert.Equal(2, service.RemainingProcessings);
        Assert.False(File.Exists(ConfigManager.TrialStatePath));
    }

    [Fact]
    public void TrialBlocksWhenLimitIsExhausted()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: true, limit: 1);
        var service = new LocalTrialLicenseService();

        service.ConsumeSuccessfulProcessing();

        Assert.False(service.CanProcess);
        Assert.Equal(0, service.RemainingProcessings);
        Assert.Throws<InvalidOperationException>(() => service.ConsumeSuccessfulProcessing());
    }

    [Fact]
    public void InvalidStateDoesNotResetCredits()
    {
        using var env = LicenseTestEnvironment.Create(isTrial: true, limit: 4);
        Directory.CreateDirectory(ConfigManager.InternalStateDir);
        File.WriteAllText(ConfigManager.TrialStatePath, "invalid state");
        var service = new LocalTrialLicenseService();

        Assert.False(service.CanProcess);
        Assert.Equal(0, service.RemainingProcessings);
    }

    private sealed class LicenseTestEnvironment : IDisposable
    {
        private readonly string _root;

        private LicenseTestEnvironment(string root)
        {
            _root = root;
        }

        public static LicenseTestEnvironment Create(bool isTrial, int limit)
        {
            var root = Path.Combine(Path.GetTempPath(), $"listforge-license-test-{Guid.NewGuid():N}");
            var appDir = Path.Combine(root, "app");
            var stateDir = Path.Combine(root, "state");
            var logDir = Path.Combine(root, "logs");

            ConfigManager.SetDirectoriesForTesting(appDir, stateDir);
            AppLogger.SetLogDirectoryForTesting(logDir);
            TrialManager.SetTrialModeForTesting(isTrial, limit);

            return new LicenseTestEnvironment(root);
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
