using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ListForge.Config;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using ListForge.ViewModels;

namespace ListForge.Tests;

public sealed class ResilientUpdateDiscoveryTests
{
    private const string ConfiguredUrl = "https://configured.example.com/update.json";
    private const string OfficialUrl = "https://official.example.com/update.json";
    private const string GitHubUrl = "https://api.github.example.com/releases/latest";

    [Fact]
    public async Task ConfiguredManifestFails_OfficialManifestSucceeds_StopsBeforeGitHub()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => Response(HttpStatusCode.InternalServerError),
            [OfficialUrl] = () => JsonResponse(ManifestJson("2.1.42")),
            [GitHubUrl] = () => JsonResponse(GitHubReleaseJson("2.1.42")),
        });
        using var service = Service(handler);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(UpdateSource.OfficialManifest, result.Value!.Release!.Source);
        Assert.Equal(1, handler.RequestCount(ConfiguredUrl));
        Assert.Equal(1, handler.RequestCount(OfficialUrl));
        Assert.Equal(0, handler.RequestCount(GitHubUrl));
    }

    [Fact]
    public async Task ManifestsFail_GitHubSucceeds_ReportsFallbackSource()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => JsonResponse("{ invalid"),
            [OfficialUrl] = () => Response(HttpStatusCode.ServiceUnavailable),
            [GitHubUrl] = () => JsonResponse(GitHubReleaseJson("2.1.42")),
        });
        using var service = Service(handler);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(UpdateSource.GitHub, result.Value!.Release!.Source);
        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Value.Availability);
    }

    [Fact]
    public async Task ConfiguredManifestSucceeds_DoesNotConsultFallbacks()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => JsonResponse(ManifestJson("2.1.42")),
            [OfficialUrl] = () => throw new InvalidOperationException("Official source should not be called."),
            [GitHubUrl] = () => throw new InvalidOperationException("GitHub should not be called."),
        });
        using var service = Service(handler);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(UpdateSource.ConfiguredManifest, result.Value!.Release!.Source);
        Assert.Equal(0, handler.RequestCount(OfficialUrl));
        Assert.Equal(0, handler.RequestCount(GitHubUrl));
    }

    [Fact]
    public async Task AllSourcesFail_ReturnsFriendlyAggregateFailure()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => Response(HttpStatusCode.NotFound),
            [OfficialUrl] = () => JsonResponse(""),
            [GitHubUrl] = () => Response(HttpStatusCode.InternalServerError),
        });
        using var service = Service(handler);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.False(result.Success);
        Assert.Equal("AllUpdateSourcesFailed", result.ErrorCode);
        Assert.DoesNotContain("500", result.UserMessage);
        Assert.Equal(1, handler.RequestCount(GitHubUrl));
    }

    [Fact]
    public async Task DuplicateConfiguredAndOfficialUrl_IsConsultedOnlyOnce()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [OfficialUrl] = () => Response(HttpStatusCode.ServiceUnavailable),
            [GitHubUrl] = () => Response(HttpStatusCode.ServiceUnavailable),
        });
        using var client = new HttpClient(handler);
        using var service = new GitHubUpdateService(client, OfficialUrl, OfficialUrl, GitHubUrl);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.False(result.Success);
        Assert.Equal(1, handler.RequestCount(OfficialUrl));
        Assert.Equal(1, handler.RequestCount(GitHubUrl));
    }

    [Fact]
    public async Task PortableManifestAsset_IsSelectedForPortableDistribution()
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => JsonResponse(ManifestJson("2.1.42")),
        });
        using var client = new HttpClient(handler);
        using var service = new GitHubUpdateService(client, ConfiguredUrl, ConfiguredUrl, ConfiguredUrl);

        var result = await service.CheckForUpdatesAsync(
            new Version(2, 1, 41),
            DistributionInfoService.FromKind(DistributionKind.PortableOneFile));

        Assert.True(result.Success);
        Assert.Equal("ListForge-v2.1.42.exe", result.Value!.Release!.GetAssetFor(DistributionKind.PortableOneFile)!.Name);
    }

    [Fact]
    public void LegacyManifest_RemainsValidAndHasNoPortableAsset()
    {
        var result = GitHubUpdateService.ParseRelease(ManifestJson("2.1.42", includePortableAndTrial: false));

        Assert.True(result.Success);
        Assert.NotNull(result.Value!.InstallerAsset);
        Assert.Null(result.Value.PortableAsset);
        Assert.Null(result.Value.TrialAsset);
    }

    [Fact]
    public void Manifest_WithInvalidSize_IsRejected()
    {
        var json = ManifestJson("2.1.42").Replace("\"size\": 10", "\"size\": 0", StringComparison.Ordinal);

        var result = GitHubUpdateService.ParseRelease(json);

        Assert.False(result.Success);
        Assert.Equal("AssetInvalid", result.ErrorCode);
    }

    [Theory]
    [InlineData("2.1.10", "2.1.9", true)]
    [InlineData("2.2.0", "2.1.99", true)]
    [InlineData("v3.0.0", "2.99.99", true)]
    [InlineData("2.1.41", "2.1.41", false)]
    public async Task VersionComparison_IsSemantic(string remote, string current, bool updateExpected)
    {
        var handler = new RouteHandler(new Dictionary<string, Func<HttpResponseMessage>>
        {
            [ConfiguredUrl] = () => JsonResponse(ManifestJson(remote.TrimStart('v'))),
        });
        using var client = new HttpClient(handler);
        using var service = new GitHubUpdateService(client, ConfiguredUrl, ConfiguredUrl, ConfiguredUrl);

        var result = await service.CheckForUpdatesAsync(
            Version.Parse(current),
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(updateExpected, result.Value!.Availability == UpdateAvailability.UpdateAvailable);
    }

    private static GitHubUpdateService Service(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        return new GitHubUpdateService(client, ConfiguredUrl, OfficialUrl, GitHubUrl);
    }

    private static HttpResponseMessage Response(HttpStatusCode status) => new(status);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string ManifestJson(string version, bool includePortableAndTrial = true)
    {
        var optionalAssets = includePortableAndTrial
            ? $$"""
              ,
              "portable": {
                "name": "ListForge-v{{version}}.exe",
                "url": "https://cdn.example.com/ListForge-v{{version}}.exe",
                "size": 10,
                "sha256": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
              },
              "trial": {
                "name": "ListForge-Trial-v{{version}}.exe",
                "url": "https://cdn.example.com/ListForge-Trial-v{{version}}.exe",
                "size": 10,
                "sha256": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
              }
              """
            : "";

        return $$"""
        {
          "version": "{{version}}",
          "tagName": "v{{version}}",
          "releaseUrl": "https://github.com/NeuberJone/ListForge/releases/tag/v{{version}}",
          "installer": {
            "name": "ListForge-Setup-{{version}}.exe",
            "url": "https://cdn.example.com/ListForge-Setup-{{version}}.exe",
            "size": 10,
            "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
          }{{optionalAssets}}
        }
        """;
    }

    private static string GitHubReleaseJson(string version) => $$"""
    {
      "tag_name": "v{{version}}",
      "draft": false,
      "prerelease": false,
      "html_url": "https://github.com/NeuberJone/ListForge/releases/tag/v{{version}}",
      "assets": [
        {
          "name": "ListForge-Setup-{{version}}.exe",
          "browser_download_url": "https://github.com/installer.exe",
          "size": 10,
          "digest": "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        }
      ]
    }
    """;

    private sealed class RouteHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, Func<HttpResponseMessage>> _routes;
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public RouteHandler(IReadOnlyDictionary<string, Func<HttpResponseMessage>> routes) =>
            _routes = routes;

        public int RequestCount(string url) => _counts.GetValueOrDefault(url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            _counts[url] = RequestCount(url) + 1;
            return Task.FromResult(_routes.TryGetValue(url, out var response)
                ? response()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

public sealed class ResilientUpdatePackageTests
{
    [Theory]
    [InlineData(DistributionKind.PortableOneFile, "ListForge-v2.1.42.exe")]
    [InlineData(DistributionKind.TrialPortableOneFile, "ListForge-Trial-v2.1.42.exe")]
    public async Task Download_SelectsExpectedAssetForDistribution(DistributionKind kind, string expectedName)
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        var release = Release(bytes);
        using var service = environment.CreateService(new BytesHandler(bytes));

        var result = await service.DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(kind, kind == DistributionKind.TrialPortableOneFile));

        Assert.True(result.Success);
        Assert.Equal(expectedName, Path.GetFileName(result.Value!.FilePath));
        Assert.True(File.Exists(result.Value.FilePath));
        Assert.False(environment.Launcher.StartedInstaller);
    }

    [Fact]
    public async Task ExistingValidDownload_IsReusedWithoutNewRequest()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        var release = Release(bytes);
        var folder = Path.Combine(environment.Root, "v2.1.42");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "ListForge-Setup-2.1.42.exe"), bytes);
        var handler = new CountingBytesHandler(bytes);
        using var service = environment.CreateService(handler);

        var result = await service.DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(0, handler.Requests);
        Assert.Contains("já está baixada", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingInvalidDownload_IsReplacedByValidatedFile()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        var release = Release(bytes);
        var folder = Path.Combine(environment.Root, "v2.1.42");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "ListForge-Setup-2.1.42.exe");
        File.WriteAllText(path, "invalid");
        var handler = new CountingBytesHandler(bytes);
        using var service = environment.CreateService(handler);

        var result = await service.DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.True(result.Success);
        Assert.Equal(1, handler.Requests);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Progress_ReportsBytesAndPercentage()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes(new string('x', 200_000));
        var release = Release(bytes);
        using var service = environment.CreateService(new BytesHandler(bytes));
        var reports = new List<UpdateDownloadProgressInfo>();
        var progress = new InlineProgress<UpdateDownloadProgressInfo>(reports.Add);

        var result = await service.DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(DistributionKind.Installed),
            progress);

        Assert.True(result.Success);
        Assert.NotEmpty(reports);
        Assert.Equal(bytes.Length, reports[^1].DownloadedBytes);
        Assert.Equal(100, reports[^1].Percentage);
    }

    [Fact]
    public async Task WrongSize_RemovesPartialAndDoesNotCreateFinalFile()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        var release = Release(bytes) with
        {
            InstallerAsset = Release(bytes).InstallerAsset with { SizeBytes = bytes.Length + 1 },
        };
        using var service = environment.CreateService(new BytesHandler(bytes));

        var result = await service.DownloadAndValidateAsync(
            release,
            DistributionInfoService.FromKind(DistributionKind.Installed));

        Assert.False(result.Success);
        Assert.Equal("AssetSizeMismatch", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(environment.Root, "*.partial", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(environment.Root, "*.exe", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TamperedPreparedPackage_IsNotStarted()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        using var service = environment.CreateService(new BytesHandler(bytes));
        var download = await service.DownloadAndValidateAsync(
            Release(bytes),
            DistributionInfoService.FromKind(DistributionKind.Installed));
        File.WriteAllText(download.Value!.FilePath, "tampered");

        var result = service.StartInstaller(download.Value);

        Assert.False(result.Success);
        Assert.False(environment.Launcher.StartedInstaller);
    }

    [Fact]
    public async Task PortablePackage_CannotStartInstalledEditionInstaller()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        using var service = environment.CreateService(new BytesHandler(bytes));
        var download = await service.DownloadAndValidateAsync(
            Release(bytes),
            DistributionInfoService.FromKind(DistributionKind.PortableOneFile));

        var result = service.StartInstaller(download.Value!);

        Assert.False(result.Success);
        Assert.Equal("InstallerNotAllowed", result.ErrorCode);
        Assert.False(environment.Launcher.StartedInstaller);
    }

    [Fact]
    public async Task OpenFolder_UsesFolderContainingValidatedPackage()
    {
        using var environment = PackageEnvironment.Create();
        var bytes = Encoding.UTF8.GetBytes("package-42");
        using var service = environment.CreateService(new BytesHandler(bytes));
        var download = await service.DownloadAndValidateAsync(
            Release(bytes),
            DistributionInfoService.FromKind(DistributionKind.PortableOneFile));

        var result = service.OpenDownloadFolder(download.Value!);

        Assert.True(result.Success);
        Assert.Equal(Path.GetDirectoryName(download.Value!.FilePath), environment.Launcher.OpenedFolder);
    }

    private static UpdateReleaseInfo Release(byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new UpdateReleaseInfo(
            new Version(2, 1, 42),
            "v2.1.42",
            "https://github.com/NeuberJone/ListForge/releases/tag/v2.1.42",
            "Notas",
            new UpdateAssetInfo("ListForge-Setup-2.1.42.exe", "https://cdn.example.com/installer", bytes.Length, hash),
            null,
            new UpdateAssetInfo("ListForge-v2.1.42.exe", "https://cdn.example.com/portable", bytes.Length, hash),
            new UpdateAssetInfo("ListForge-Trial-v2.1.42.exe", "https://cdn.example.com/trial", bytes.Length, hash));
    }

    private sealed class PackageEnvironment : IDisposable
    {
        private PackageEnvironment(string root)
        {
            Root = root;
            Launcher = new FakeLauncher();
        }

        public string Root { get; }
        public FakeLauncher Launcher { get; }

        public static PackageEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-ResilientUpdateTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PackageEnvironment(root);
        }

        public UpdateInstallerService CreateService(HttpMessageHandler handler) =>
            new(new HttpClient(handler), Launcher, Root);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // The operating system may finish releasing a temporary file after the test.
            }
        }
    }

    private sealed class FakeLauncher : IUpdateProcessLauncher
    {
        public bool StartedInstaller { get; private set; }
        public string? OpenedFolder { get; private set; }

        public bool StartInstaller(string installerPath, string arguments)
        {
            StartedInstaller = true;
            return true;
        }

        public bool OpenUrl(string url) => true;

        public bool OpenFolder(string folderPath)
        {
            OpenedFolder = folderPath;
            return true;
        }
    }

    private class BytesHandler : HttpMessageHandler
    {
        protected readonly byte[] Bytes;

        public BytesHandler(byte[] bytes) => Bytes = bytes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Bytes),
            });
    }

    private sealed class CountingBytesHandler : BytesHandler
    {
        public CountingBytesHandler(byte[] bytes)
            : base(bytes)
        {
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report) => _report = report;

        public void Report(T value) => _report(value);
    }
}

public sealed class UpdateViewModelStateTests
{
    [Fact]
    public void InitialState_IsIdleWithNoSessionCheck()
    {
        using var environment = ViewModelEnvironment.Create();
        var vm = environment.CreateViewModel();

        Assert.Equal(UpdateStatus.Idle, vm.CurrentUpdateStatus);
        Assert.Equal("Nenhuma nesta sessão", vm.LastUpdateCheckSessionText);
        Assert.True(vm.IsCheckUpdateActionVisible);
    }

    [Fact]
    public async Task Check_TransitionsThroughCheckingToUpdateAvailable()
    {
        using var environment = ViewModelEnvironment.Create();
        var completion = new TaskCompletionSource<OperationResult<UpdateCheckInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Discovery.Handler = (_, _, _) => completion.Task;
        var vm = environment.CreateViewModel();

        var checkTask = vm.CheckForUpdatesAsync(isAutomatic: false);
        Assert.Equal(UpdateStatus.Checking, vm.CurrentUpdateStatus);
        completion.SetResult(UpdateAvailableResult());
        await checkTask;

        Assert.Equal(UpdateStatus.UpdateAvailable, vm.CurrentUpdateStatus);
        Assert.True(vm.IsDownloadUpdateActionVisible);
        Assert.Equal("2.1.42", vm.AvailableUpdateVersionText);
    }

    [Fact]
    public async Task Check_UpToDate_ClearsDownloadAction()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Discovery.Result = OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
            new Version(2, 1, 41),
            UpdateAvailability.UpToDate,
            Release(),
            "O ListForge está atualizado."));
        var vm = environment.CreateViewModel();

        await vm.CheckForUpdatesAsync(isAutomatic: false);

        Assert.Equal(UpdateStatus.UpToDate, vm.CurrentUpdateStatus);
        Assert.False(vm.HasAvailableUpdate);
        Assert.False(vm.IsDownloadUpdateActionVisible);
    }

    [Fact]
    public async Task Check_AllSourcesFail_UsesOfflineStateWithoutThrowing()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Discovery.Result = OperationResult<UpdateCheckInfo>.Fail(
            "Falha amigável.",
            "Falha técnica.",
            errorCode: "AllUpdateSourcesFailed");
        var vm = environment.CreateViewModel();

        await vm.CheckForUpdatesAsync(isAutomatic: false);

        Assert.Equal(UpdateStatus.Offline, vm.CurrentUpdateStatus);
        Assert.Equal("Tentar novamente", vm.CheckUpdatesActionText);
        Assert.DoesNotContain("técnica", vm.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutomaticCheckFailure_UsesSilentStatus()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Discovery.Result = OperationResult<UpdateCheckInfo>.Fail(
            "Falha amigável.",
            "Falha técnica.",
            errorCode: "AllUpdateSourcesFailed");
        var vm = environment.CreateViewModel();

        await vm.CheckForUpdatesAsync(isAutomatic: true);

        Assert.Equal("Não foi possível verificar atualizações automaticamente.", vm.UpdateStatusText);
        Assert.Equal(UpdateStatus.Offline, vm.CurrentUpdateStatus);
    }

    [Fact]
    public async Task Download_DoesNotRunAnotherCheck_AndBecomesReadyToInstall()
    {
        using var environment = ViewModelEnvironment.Create();
        var vm = environment.CreateViewModel();
        await vm.CheckForUpdatesAsync(isAutomatic: false);
        var checksBeforeDownload = environment.Discovery.Calls;

        await vm.DownloadAvailableUpdateAsync();

        Assert.Equal(checksBeforeDownload, environment.Discovery.Calls);
        Assert.Equal(1, environment.Package.DownloadCalls);
        Assert.Equal(UpdateStatus.ReadyToInstall, vm.CurrentUpdateStatus);
        Assert.True(vm.IsInstallUpdateActionVisible);
    }

    [Fact]
    public async Task PortableDownload_BecomesDownloadedAndOffersFolder()
    {
        using var environment = ViewModelEnvironment.Create();
        var vm = environment.CreateViewModel(DistributionKind.PortableOneFile);
        await vm.CheckForUpdatesAsync(isAutomatic: false);

        await vm.DownloadAvailableUpdateAsync();

        Assert.Equal(UpdateStatus.Downloaded, vm.CurrentUpdateStatus);
        Assert.True(vm.IsOpenUpdateFolderActionVisible);
        Assert.False(vm.IsInstallUpdateActionVisible);
    }

    [Fact]
    public async Task DownloadFailure_PreservesReleaseAndEnablesDownloadRetry()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Package.DownloadResult = OperationResult<PreparedUpdatePackage>.Fail(
            "Não foi possível baixar a atualização.",
            "Falha simulada.",
            errorCode: "UpdateDownloadFailed");
        var vm = environment.CreateViewModel();
        await vm.CheckForUpdatesAsync(isAutomatic: false);

        await vm.DownloadAvailableUpdateAsync();

        Assert.Equal(UpdateStatus.Failed, vm.CurrentUpdateStatus);
        Assert.True(vm.HasAvailableUpdate);
        Assert.True(vm.IsDownloadUpdateActionVisible);
        Assert.Equal("Tentar baixar novamente", vm.DownloadUpdateActionText);
    }

    [Fact]
    public async Task CancelDownload_ReturnsToAvailableStateAndKeepsRelease()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Package.DownloadHandler = async (_, _, _, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Cancellation was expected.");
            }
            catch (OperationCanceledException ex)
            {
                return OperationResult<PreparedUpdatePackage>.Fail(
                    "Download cancelado.",
                    "Cancelado.",
                    ex,
                    "DownloadCanceled");
            }
        };
        var vm = environment.CreateViewModel();
        await vm.CheckForUpdatesAsync(isAutomatic: false);

        var downloadTask = vm.DownloadAvailableUpdateAsync();
        Assert.Equal(UpdateStatus.Downloading, vm.CurrentUpdateStatus);
        vm.CancelUpdateDownloadCommand.Execute(null);
        await downloadTask;

        Assert.Equal(UpdateStatus.UpdateAvailable, vm.CurrentUpdateStatus);
        Assert.True(vm.HasAvailableUpdate);
    }

    [Fact]
    public async Task ConcurrentChecks_AreBlocked()
    {
        using var environment = ViewModelEnvironment.Create();
        var completion = new TaskCompletionSource<OperationResult<UpdateCheckInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Discovery.Handler = (_, _, _) => completion.Task;
        var vm = environment.CreateViewModel();

        var first = vm.CheckForUpdatesAsync(isAutomatic: false);
        var second = vm.CheckForUpdatesAsync(isAutomatic: false);
        Assert.Equal(1, environment.Discovery.Calls);
        completion.SetResult(UpdateAvailableResult());
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task InstallSuccess_RequestsShutdownOnlyAfterLauncherConfirms()
    {
        using var environment = ViewModelEnvironment.Create();
        var vm = environment.CreateViewModel();
        await vm.CheckForUpdatesAsync(isAutomatic: false);
        await vm.DownloadAvailableUpdateAsync();
        var shutdownRequested = false;
        vm.RequestShutdown += () => shutdownRequested = true;

        await vm.InstallPreparedUpdateAsync();

        Assert.True(shutdownRequested);
        Assert.Equal(1, environment.Package.StartCalls);
        Assert.Equal(UpdateStatus.Installing, vm.CurrentUpdateStatus);
    }

    [Fact]
    public async Task InstallFailure_KeepsApplicationReadyForRetry()
    {
        using var environment = ViewModelEnvironment.Create();
        environment.Package.StartResult = OperationResult.Fail(
            "Não foi possível iniciar o instalador. O ListForge continuará aberto.",
            "Falha simulada.",
            errorCode: "InstallerStartFailed");
        var vm = environment.CreateViewModel();
        await vm.CheckForUpdatesAsync(isAutomatic: false);
        await vm.DownloadAvailableUpdateAsync();
        var shutdownRequested = false;
        vm.RequestShutdown += () => shutdownRequested = true;

        await vm.InstallPreparedUpdateAsync();

        Assert.False(shutdownRequested);
        Assert.Equal(UpdateStatus.ReadyToInstall, vm.CurrentUpdateStatus);
        Assert.True(vm.IsInstallUpdateActionVisible);
    }

    [Fact]
    public async Task StartupCheckDisabled_DoesNotCallDiscovery()
    {
        using var environment = ViewModelEnvironment.Create();
        var vm = environment.CreateViewModel();
        vm.CheckUpdatesOnStartup = false;

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.Equal(0, environment.Discovery.Calls);
        Assert.Equal(UpdateStatus.Idle, vm.CurrentUpdateStatus);
    }

    private static OperationResult<UpdateCheckInfo> UpdateAvailableResult() =>
        OperationResult<UpdateCheckInfo>.Ok(new UpdateCheckInfo(
            new Version(2, 1, 41),
            UpdateAvailability.UpdateAvailable,
            Release(),
            "Uma nova versão está disponível."));

    private static UpdateReleaseInfo Release()
    {
        var installer = new UpdateAssetInfo(
            "ListForge-Setup-2.1.42.exe",
            "https://cdn.example.com/installer",
            10,
            new string('A', 64));
        return new UpdateReleaseInfo(
            new Version(2, 1, 42),
            "v2.1.42",
            "https://github.com/NeuberJone/ListForge/releases/tag/v2.1.42",
            "Notas",
            installer,
            null,
            new UpdateAssetInfo("ListForge-v2.1.42.exe", "https://cdn.example.com/portable", 10, new string('B', 64)),
            new UpdateAssetInfo("ListForge-Trial-v2.1.42.exe", "https://cdn.example.com/trial", 10, new string('C', 64)));
    }

    private sealed class ViewModelEnvironment : IDisposable
    {
        private ViewModelEnvironment(string root)
        {
            Root = root;
            Discovery = new FakeDiscoveryService { Result = UpdateAvailableResult() };
            Package = new FakePackageService();
            ConfigManager.SetDirectoriesForTesting(Path.Combine(root, "app"), Path.Combine(root, "state"));
        }

        public string Root { get; }
        public FakeDiscoveryService Discovery { get; }
        public FakePackageService Package { get; }

        public static ViewModelEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ListForge-UpdateViewModelTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new ViewModelEnvironment(root);
        }

        public MainViewModel CreateViewModel(DistributionKind kind = DistributionKind.Installed)
        {
            var distribution = DistributionInfoService.FromKind(
                kind,
                kind == DistributionKind.TrialPortableOneFile);
            Package.Distribution = distribution;
            return new MainViewModel(Discovery, Package, distribution);
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
                // Temporary files may still be released by the test host.
            }
        }
    }

    private sealed class FakeDiscoveryService : IUpdateDiscoveryService
    {
        public int Calls { get; private set; }
        public OperationResult<UpdateCheckInfo> Result { get; set; } = null!;
        public Func<Version, DistributionInfo, CancellationToken, Task<OperationResult<UpdateCheckInfo>>>? Handler { get; set; }

        public Task<OperationResult<UpdateCheckInfo>> CheckForUpdatesAsync(
            Version currentVersion,
            DistributionInfo distribution,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Handler?.Invoke(currentVersion, distribution, cancellationToken)
                ?? Task.FromResult(Result);
        }
    }

    private sealed class FakePackageService : IUpdatePackageService
    {
        public int DownloadCalls { get; private set; }
        public int StartCalls { get; private set; }
        public DistributionInfo Distribution { get; set; } = DistributionInfoService.FromKind(DistributionKind.Installed);
        public OperationResult<PreparedUpdatePackage>? DownloadResult { get; set; }
        public OperationResult StartResult { get; set; } = OperationResult.Ok();
        public Func<UpdateReleaseInfo, DistributionInfo, IProgress<UpdateDownloadProgressInfo>?, CancellationToken, Task<OperationResult<PreparedUpdatePackage>>>? DownloadHandler { get; set; }

        public Task<OperationResult<PreparedUpdatePackage>> DownloadAndValidateAsync(
            UpdateReleaseInfo release,
            DistributionInfo distribution,
            IProgress<UpdateDownloadProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DownloadCalls++;
            if (DownloadHandler != null)
                return DownloadHandler(release, distribution, progress, cancellationToken);
            if (DownloadResult != null)
                return Task.FromResult(DownloadResult);

            var asset = release.GetAssetFor(distribution.Kind)!;
            return Task.FromResult(OperationResult<PreparedUpdatePackage>.Ok(new PreparedUpdatePackage(
                Path.Combine(Path.GetTempPath(), asset.Name),
                release,
                asset,
                distribution.Kind,
                asset.Sha256!)));
        }

        public OperationResult ValidatePreparedPackage(PreparedUpdatePackage package) => OperationResult.Ok();

        public OperationResult StartInstaller(PreparedUpdatePackage package)
        {
            StartCalls++;
            return StartResult;
        }

        public OperationResult OpenReleasePage(UpdateReleaseInfo release) => OperationResult.Ok();

        public OperationResult OpenDownloadFolder(PreparedUpdatePackage package) => OperationResult.Ok();
    }
}
