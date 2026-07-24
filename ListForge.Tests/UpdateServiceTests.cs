using System.Net;
using System.Net.Http;
using System.Text;
using ListForge.Config;
using ListForge.Models;
using ListForge.Services;

namespace ListForge.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdates_RemoteVersionGreater_ReturnsUpdateAvailable()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-Setup-2.1.29.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.True(result.Success);
        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Value!.Availability);
        Assert.Equal("2.1.29", GitHubUpdateService.ToThreePartVersion(result.Value.Release!.Version));
    }

    [Fact]
    public async Task CheckForUpdates_StaticManifest_ReturnsUpdateAvailable()
    {
        using var service = ServiceWithJson(ManifestJson("2.1.29"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.True(result.Success);
        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Value!.Availability);
        Assert.Equal("ListForge-Setup-2.1.29.exe", result.Value.Release!.InstallerAsset.Name);
        Assert.Equal("https://updates.example.com/ListForge-Setup-2.1.29.exe", result.Value.Release.InstallerAsset.DownloadUrl);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", result.Value.Release.InstallerAsset.Sha256);
        Assert.NotNull(result.Value.Release.ChecksumsAsset);
    }

    [Fact]
    public async Task CheckForUpdates_SameVersion_ReturnsUpToDate()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-Setup-2.1.29.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 29));

        Assert.True(result.Success);
        Assert.Equal(UpdateAvailability.UpToDate, result.Value!.Availability);
    }

    [Fact]
    public async Task CheckForUpdates_RemoteVersionLower_DoesNotOfferDowngrade()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.28", "ListForge-Setup-2.1.28.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 29));

        Assert.True(result.Success);
        Assert.Equal(UpdateAvailability.RemoteOlder, result.Value!.Availability);
    }

    [Fact]
    public void TryParseReleaseVersion_NormalizesVPrefix()
    {
        var ok = GitHubUpdateService.TryParseReleaseVersion("v2.1.29", out var version);

        Assert.True(ok);
        Assert.Equal(new Version(2, 1, 29), version);
    }

    [Fact]
    public async Task CheckForUpdates_InvalidTag_ReturnsControlledError()
    {
        using var service = ServiceWithJson(ReleaseJson("release-latest", "ListForge-Setup-2.1.29.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InvalidTag", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_InvalidApiUrl_ReturnsControlledError()
    {
        using var client = new HttpClient(new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        using var service = new GitHubUpdateService(client, "http://example.com/latest");

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InvalidUpdateApiUrl", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_HttpError_ReturnsFriendlyError()
    {
        using var client = new HttpClient(new StaticHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var service = new GitHubUpdateService(client);

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("HttpError", result.ErrorCode);
        Assert.DoesNotContain("Forbidden", result.UserMessage);
    }

    [Fact]
    public async Task CheckForUpdates_Timeout_ReturnsTimeoutError()
    {
        using var client = new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout")));
        var service = new GitHubUpdateService(client);

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("Timeout", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_InvalidJson_ReturnsControlledError()
    {
        using var service = ServiceWithJson("{ invalid json");

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InvalidJson", result.ErrorCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CheckForUpdates_DraftOrPrerelease_IsIgnored(bool draft, bool prerelease)
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-Setup-2.1.29.exe", draft: draft, prerelease: prerelease));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("ReleaseNotStable", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_AssetFromAnotherVersion_IsRejected()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-Setup-2.1.28.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InstallerAssetMissing", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_MissingInstaller_IsRejected()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-v2.1.29.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InstallerAssetMissing", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_NonHttpsInstallerUrl_IsRejected()
    {
        using var service = ServiceWithJson(ReleaseJson("v2.1.29", "ListForge-Setup-2.1.29.exe", downloadUrl: "http://example.com/ListForge-Setup-2.1.29.exe"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("InstallerUrlNotHttps", result.ErrorCode);
    }

    [Fact]
    public async Task CheckForUpdates_NonHttpsChecksumUrl_IsRejected()
    {
        using var service = ServiceWithJson(ReleaseJson(
            "v2.1.29",
            "ListForge-Setup-2.1.29.exe",
            checksumUrl: "http://example.com/SHA256SUMS.txt"));

        var result = await service.CheckForUpdatesAsync(new Version(2, 1, 28));

        Assert.False(result.Success);
        Assert.Equal("ChecksumsUrlNotHttps", result.ErrorCode);
    }

    private static GitHubUpdateService ServiceWithJson(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        var client = new HttpClient(new StaticHandler(response));
        return new GitHubUpdateService(client);
    }

    private static string ReleaseJson(
        string tag,
        string assetName,
        string downloadUrl = "https://example.com/ListForge-Setup.exe",
        bool draft = false,
        bool prerelease = false,
        string? digest = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        string checksumUrl = "https://example.com/SHA256SUMS.txt")
    {
        return $$"""
        {
          "tag_name": "{{tag}}",
          "draft": {{draft.ToString().ToLowerInvariant()}},
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "html_url": "https://github.com/NeuberJone/ListForge/releases/tag/{{tag}}",
          "body": "Notas da Release",
          "assets": [
            {
              "name": "{{assetName}}",
              "browser_download_url": "{{downloadUrl}}",
              "size": 10,
              "digest": "{{digest}}"
            },
            {
              "name": "SHA256SUMS.txt",
              "browser_download_url": "{{checksumUrl}}",
              "size": 100
            }
          ]
        }
        """;
    }

    private static string ManifestJson(string version)
    {
        return $$"""
        {
          "version": "{{version}}",
          "tagName": "v{{version}}",
          "releaseUrl": "https://updates.example.com/",
          "notes": "Notas da Release",
          "installer": {
            "name": "ListForge-Setup-{{version}}.exe",
            "url": "https://updates.example.com/ListForge-Setup-{{version}}.exe",
            "size": 10,
            "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
          },
          "checksums": {
            "name": "SHA256SUMS.txt",
            "url": "https://updates.example.com/SHA256SUMS.txt",
            "size": 100
          }
        }
        """;
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StaticHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }
}

public class UpdateInstallerServiceTests
{
    [Fact]
    public async Task DownloadAndValidateInstaller_WithCorrectHash_PreparesInstaller()
    {
        using var env = UpdateTestEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("installer");
        var hash = Sha256(bytes);
        using var service = env.CreateInstallerService(new MapHandler(new()
        {
            ["https://example.com/ListForge-Setup-2.1.29.exe"] = bytes,
        }));

        var result = await service.DownloadAndValidateInstallerAsync(Release(hash, bytes.Length));

        Assert.True(result.Success);
        Assert.True(File.Exists(result.Value!.InstallerPath));
        Assert.EndsWith("ListForge-Setup-2.1.29.exe", result.Value.InstallerPath);
        Assert.False(File.Exists(result.Value.InstallerPath + ".partial"));
    }

    [Fact]
    public async Task DownloadAndValidateInstaller_WithChecksumAsset_UsesSha256Sums()
    {
        using var env = UpdateTestEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("installer");
        var hash = Sha256(bytes);
        var sums = $"{hash} Installer\\ListForge-Setup-2.1.29.exe";
        using var service = env.CreateInstallerService(new MapHandler(new()
        {
            ["https://example.com/ListForge-Setup-2.1.29.exe"] = bytes,
            ["https://example.com/SHA256SUMS.txt"] = Encoding.UTF8.GetBytes(sums),
        }));

        var result = await service.DownloadAndValidateInstallerAsync(Release(null, bytes.Length, includeChecksums: true));

        Assert.True(result.Success);
        Assert.True(File.Exists(result.Value!.InstallerPath));
    }

    [Fact]
    public async Task DownloadAndValidateInstaller_WithIncorrectHash_DeletesPartialAndFails()
    {
        using var env = UpdateTestEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("installer");
        using var service = env.CreateInstallerService(new MapHandler(new()
        {
            ["https://example.com/ListForge-Setup-2.1.29.exe"] = bytes,
        }));

        var result = await service.DownloadAndValidateInstallerAsync(Release(new string('B', 64), bytes.Length));

        Assert.False(result.Success);
        Assert.Equal("InstallerHashMismatch", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(env.Root, "*.partial", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(env.Root, "*.exe", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DownloadAndValidateInstaller_WithoutAnyChecksum_DoesNotPrepareInstaller()
    {
        using var env = UpdateTestEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("installer");
        using var service = env.CreateInstallerService(new MapHandler(new()
        {
            ["https://example.com/ListForge-Setup-2.1.29.exe"] = bytes,
        }));

        var result = await service.DownloadAndValidateInstallerAsync(Release(null, bytes.Length));

        Assert.False(result.Success);
        Assert.Equal("ChecksumMissing", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(env.Root, "*.exe", SearchOption.AllDirectories));
    }

    [Fact]
    public void StartInstaller_DoesNotExecutePartialFile()
    {
        using var env = UpdateTestEnvironment.Create();
        var partial = Path.Combine(env.Root, "ListForge-Setup-2.1.29.exe.partial");
        File.WriteAllText(partial, "partial");
        using var service = env.CreateInstallerService(new MapHandler([]));

        var result = service.StartInstaller(partial);

        Assert.False(result.Success);
        Assert.Equal("InstallerNotReady", result.ErrorCode);
        Assert.False(env.Launcher.StartedInstaller);
    }

    [Fact]
    public void StartInstaller_UsesExpectedInstallerArguments()
    {
        using var env = UpdateTestEnvironment.Create();
        var installer = Path.Combine(env.Root, "ListForge-Setup-2.1.29.exe");
        File.WriteAllText(installer, "installer");
        using var service = env.CreateInstallerService(new MapHandler([]));

        var result = service.StartInstaller(installer);

        Assert.True(result.Success);
        Assert.True(env.Launcher.StartedInstaller);
        Assert.Equal(installer, env.Launcher.InstallerPath);
        Assert.Equal(UpdateInstallerService.InstallerArguments, env.Launcher.Arguments);
    }

    [Fact]
    public async Task DownloadAndValidateInstaller_WhenCanceled_DoesNotLeavePartial()
    {
        using var env = UpdateTestEnvironment.Create();
        using var service = env.CreateInstallerService(new CancelingHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.DownloadAndValidateInstallerAsync(Release(new string('A', 64), 9), cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.Equal("DownloadCanceled", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(env.Root, "*.partial", SearchOption.AllDirectories));
    }

    [Fact]
    public void DistributionKinds_ControlInstallerEligibility()
    {
        Assert.True(DistributionInfoService.FromKind(DistributionKind.Installed).CanRunInstallerUpdate);
        Assert.False(DistributionInfoService.FromKind(DistributionKind.PortableOneFile).CanRunInstallerUpdate);
        Assert.False(DistributionInfoService.FromKind(DistributionKind.Development).CanRunInstallerUpdate);
        Assert.False(DistributionInfoService.FromKind(DistributionKind.TrialPortableOneFile, isTrial: true).CanRunInstallerUpdate);
    }

    [Fact]
    public void InstallerScript_PreservesInPlaceIdentity()
    {
        var iss = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "installer", "ListForge.iss"));

        Assert.Contains("AppId={{C54F2F4E-31D3-4F33-80E1-CB7679E02AA7}", iss);
        Assert.Contains("UsePreviousAppDir=yes", iss);
        Assert.Contains("CloseApplications=yes", iss);
        Assert.Contains("RestartApplications=no", iss);
        Assert.Contains(@"DefaultDirName={autopf}\{#MyAppName}", iss);
        Assert.DoesNotContain("ListForge-v", iss);
    }

    [Fact]
    public void AppConfig_DefaultAndLegacyConfig_EnableStartupChecks()
    {
        using var env = UpdateTestEnvironment.Create();
        ConfigManager.SetDirectoriesForTesting(Path.Combine(env.Root, "app"), Path.Combine(env.Root, "state"));

        Assert.True(ConfigManager.LoadConfig().CheckUpdatesOnStartup);
        Assert.Null(ConfigManager.LoadConfig().LastUpdateCheckUtc);

        File.WriteAllText(ConfigManager.ConfigPath, "{}");
        Assert.True(ConfigManager.LoadConfig().CheckUpdatesOnStartup);
        Assert.Null(ConfigManager.LoadConfig().LastUpdateCheckUtc);
    }

    [Fact]
    public void UpdateCheckPolicy_AutomaticCheckRunsAtMostOncePerDay()
    {
        var now = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        Assert.True(UpdateCheckPolicy.ShouldRunAutomaticCheck(null, now));
        Assert.False(UpdateCheckPolicy.ShouldRunAutomaticCheck(now.AddHours(-23), now));
        Assert.True(UpdateCheckPolicy.ShouldRunAutomaticCheck(now.AddHours(-24), now));
    }

    [Fact]
    public void UpdateCheckPolicy_ManualCheckRequiresOneMinuteBetweenAttempts()
    {
        var now = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        Assert.True(UpdateCheckPolicy.ShouldRunManualCheck(null, now));
        Assert.False(UpdateCheckPolicy.ShouldRunManualCheck(now.AddSeconds(-59), now));
        Assert.True(UpdateCheckPolicy.ShouldRunManualCheck(now.AddMinutes(-1), now));
    }

    [Fact]
    public void AppConfig_PersistsLastUpdateCheckUtc()
    {
        using var env = UpdateTestEnvironment.Create();
        ConfigManager.SetDirectoriesForTesting(Path.Combine(env.Root, "app"), Path.Combine(env.Root, "state"));
        var checkedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        ConfigManager.SaveConfig(new AppConfig { LastUpdateCheckUtc = checkedAt });

        Assert.Equal(checkedAt, ConfigManager.LoadConfig().LastUpdateCheckUtc);
    }

    [Fact]
    public void AsyncRelayCommand_ExecuteReturnsBeforeTaskCompletes()
    {
        var started = false;
        var completed = false;
        var releaseCommand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var completedSignal = new ManualResetEventSlim(false);
        var command = new ListForge.ViewModels.AsyncRelayCommand(async () =>
        {
            started = true;
            await releaseCommand.Task;
            completed = true;
            completedSignal.Set();
        });

        command.Execute(null);

        Assert.True(started);
        Assert.False(completed);

        releaseCommand.SetResult();
        Assert.True(completedSignal.Wait(TimeSpan.FromSeconds(2)));
    }

    private static UpdateReleaseInfo Release(string? hash, long size, bool includeChecksums = false)
    {
        var installer = new UpdateAssetInfo(
            "ListForge-Setup-2.1.29.exe",
            "https://example.com/ListForge-Setup-2.1.29.exe",
            size,
            hash);
        var checksums = includeChecksums
            ? new UpdateAssetInfo("SHA256SUMS.txt", "https://example.com/SHA256SUMS.txt", 100, null)
            : null;
        return new UpdateReleaseInfo(
            new Version(2, 1, 29),
            "v2.1.29",
            "https://github.com/NeuberJone/ListForge/releases/tag/v2.1.29",
            "Notas",
            installer,
            checksums);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    private sealed class UpdateTestEnvironment : IDisposable
    {
        private UpdateTestEnvironment(string root)
        {
            Root = root;
            Launcher = new FakeLauncher();
        }

        public string Root { get; }
        public FakeLauncher Launcher { get; }

        public static UpdateTestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-UpdateTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new UpdateTestEnvironment(root);
        }

        public UpdateInstallerService CreateInstallerService(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler);
            return new UpdateInstallerService(client, Launcher, Root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Temporary test directories can be left for the OS to clean up.
            }
        }
    }

    private sealed class FakeLauncher : IUpdateProcessLauncher
    {
        public bool StartedInstaller { get; private set; }
        public string? InstallerPath { get; private set; }
        public string? Arguments { get; private set; }

        public bool StartInstaller(string installerPath, string arguments)
        {
            StartedInstaller = true;
            InstallerPath = installerPath;
            Arguments = arguments;
            return true;
        }

        public bool OpenUrl(string url) => true;
    }

    private sealed class MapHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses;

        public MapHandler(Dictionary<string, byte[]> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_responses.TryGetValue(request.RequestUri!.ToString(), out var bytes))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
            });
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("installer")),
            });
        }
    }
}
