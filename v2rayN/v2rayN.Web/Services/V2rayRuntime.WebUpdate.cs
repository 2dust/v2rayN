using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using ServiceLib.Common;
using ServiceLib.Manager;
using ServiceLib.Models.Dto;
using ServiceLib.Services;
using v2rayN.Web.Contracts;
using v2rayN.Web.Launcher;

namespace v2rayN.Web.Services;

public sealed partial class V2rayRuntime
{
    private const string WebUpdateProgressFile = "web-update-progress.json";
    private Task? _webUpdateTask;
    private string? _latestWebUpdateVersion;

    public WebUpdateTargetView GetWebUpdateTarget()
    {
        var deployment = GetWebUpdateDeployment();
        var build = WebBuildIdentity.Current;
        var selected = Config.CheckUpdateItem.SelectedCoreTypes;
        var supportedRid = build.Rid is "linux-x64" or "linux-arm64";
        var runtimeInstallReason = GetWebUpdateRuntimeInstallReason();
        var canInstall = deployment.CanInstall && runtimeInstallReason is null;
        return new WebUpdateTargetView(
            WebUpdateTarget,
            build.Version,
            build.Commit,
            build.BuildDate,
            build.Rid,
            ToDeploymentName(deployment.Kind),
            supportedRid,
            deployment.CanCheck,
            canInstall,
            supportedRid && (selected?.Contains(WebUpdateTarget, StringComparer.Ordinal) ?? true),
            !supportedRid ? "maintenance.webUpdateUnsupported" : deployment.InstallReasonKey ?? runtimeInstallReason,
            Volatile.Read(ref _latestWebUpdateVersion) ?? _updateProgress.GetValueOrDefault(WebUpdateTarget)?.Version);
    }

    public async Task<ApiEnvelope<WebUpdateCheckView>> CheckWebUpdateAsync(
        bool? preRelease,
        bool? useProxy,
        CancellationToken cancellationToken)
    {
        var check = await CheckWebUpdateReleaseAsync(
            GetUpdatePreRelease(WebUpdateTarget, preRelease),
            useProxy ?? Config.CheckUpdateItem.UpdateViaProxy,
            cancellationToken);
        Volatile.Write(ref _latestWebUpdateVersion, check.Version);
        var deployment = GetWebUpdateDeployment();
        var view = new WebUpdateCheckView(
            check.IsUpdateAvailable,
            WebBuildIdentity.Current.Version,
            WebBuildIdentity.Current.Commit,
            WebBuildIdentity.Current.Rid,
            check.Version,
            check.Commit,
            deployment.CanInstall && GetWebUpdateRuntimeInstallReason() is null,
            deployment.InstallReasonKey ?? GetWebUpdateRuntimeInstallReason(),
            check.Error ?? check.Detail);
        if (!string.IsNullOrWhiteSpace(check.Error))
        {
            AddLog("update", $"v2rayN Web update check failed: {check.Error}");
            return ApiEnvelope<WebUpdateCheckView>.Fail("web_update_check_failed", ApiMessageKeys.WebUpdateCheckFailed, view);
        }
        return ApiEnvelope<WebUpdateCheckView>.Ok(view,
            check.IsUpdateAvailable ? ApiMessageKeys.CoreUpdateAvailable : ApiMessageKeys.CoreUpdateCurrent);
    }

    public OperationView StartWebUpdate(bool? preRelease = null, bool? useProxy = null)
    {
        if (!GetWebUpdateDeployment().CanInstall)
        {
            return OperationView.Fail("web_update_install_unsupported", GetWebUpdateDeployment().InstallReasonKey
                ?? ApiMessageKeys.CoreUpdateUnsupported);
        }
        if (GetWebUpdateRuntimeInstallReason() is { } runtimeReason)
            return OperationView.Fail("web_update_runtime_unavailable", runtimeReason);
        if (Config.CheckUpdateItem.SelectedCoreTypes is { } selected
            && !selected.Contains(WebUpdateTarget, StringComparer.Ordinal))
        {
            return OperationView.Fail("web_update_not_selected", ApiMessageKeys.CoreUpdateUnsupported);
        }

        lock (_updateTaskGate)
        {
            if (IsUpdateRunningLocked())
            {
                return OperationView.Fail("core_update_busy", ApiMessageKeys.CoreUpdateBusy);
            }
            _webUpdateTask = Task.Run(() => RunWebUpdateAsync(
                GetUpdatePreRelease(WebUpdateTarget, preRelease),
                useProxy ?? Config.CheckUpdateItem.UpdateViaProxy));
        }
        return OperationView.Ok(ApiMessageKeys.CoreUpdateStarted, new { target = WebUpdateTarget });
    }

    internal CoreUpdateProgressView? GetWebUpdateProgress()
    {
        var inMemory = _updateProgress.GetValueOrDefault(WebUpdateTarget);
        if (inMemory is { IsComplete: false } || inMemory?.Phase == "completed") return inMemory;
        try
        {
            var path = Utils.GetTempPath(WebUpdateProgressFile);
            if (!File.Exists(path)) return inMemory;
            var persisted = JsonSerializer.Deserialize<WebUpdateProgressState>(File.ReadAllText(path), JsonOptions);
            if (persisted is null) return inMemory;
            var progress = new CoreUpdateProgressView(
                WebUpdateTarget,
                persisted.Phase,
                persisted.IsComplete,
                persisted.Success,
                persisted.CoreWasRunning,
                persisted.Version,
                persisted.Detail,
                false,
                persisted.RollbackSucceeded);
            // In-flight persisted progress belongs to the detached helper. Caching
            // it as local progress would prevent subsequent reads of its result.
            if (progress.IsComplete) _updateProgress[WebUpdateTarget] = progress;
            return progress;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AddLog("update", $"Could not read the native Web update progress: {exception.Message}");
            return inMemory;
        }
    }

    private async Task RunWebUpdateAsync(bool preRelease, bool useProxy)
    {
        WebUpdateStage? stage = null;
        try
        {
            await using var maintenance = await _operations.EnterExclusiveAsync(
                _operations.ShutdownToken,
                allowReadOnlyObservations: true);
            PublishWebUpdateProgress("checking", false, false, null);
            var check = await CheckWebUpdateReleaseAsync(preRelease, useProxy, maintenance.Token);
            Volatile.Write(ref _latestWebUpdateVersion, check.Version);
            if (!string.IsNullOrWhiteSpace(check.Error))
            {
                PublishWebUpdateProgress("failed", true, false, check.Error, check.Version);
                return;
            }
            if (!check.IsUpdateAvailable)
            {
                PublishWebUpdateProgress("completed", true, true, check.Detail ?? "The Web application is up to date.", check.Version);
                return;
            }

            stage = await StageWebUpdateAsync(check, useProxy, maintenance.Token, batch: false);
            await ApplyStagedWebUpdateAsync(stage, maintenance.Token, batch: false);
        }
        catch (OperationCanceledException) when (_operations.IsStopping)
        {
            PublishWebUpdateProgress("failed", true, false, "Canceled during graceful shutdown.", stage?.Manifest.Version);
        }
        catch (Exception exception)
        {
            AddLog("update", $"v2rayN Web update failed: {exception}");
            PublishWebUpdateProgress("failed", true, false, exception.Message, stage?.Manifest.Version);
        }
        finally
        {
            CleanupWebUpdateStage(stage);
            lock (_updateTaskGate) _webUpdateTask = null;
        }
    }

    private async Task<WebUpdateReleaseCheck> CheckWebUpdateReleaseAsync(
        bool allowPrerelease,
        bool useProxy,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = WebBuildIdentity.Current.Repository;
            var downloader = new DownloadService();
            var releaseJson = await downloader.TryDownloadString(
                WebReleaseChannel.BuildReleaseIndexUrl(repository),
                useProxy,
                $"v2rayN.Web/{WebBuildIdentity.Current.Version}",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(releaseJson) || releaseJson.Length > 8 * 1024 * 1024)
                return WebUpdateReleaseCheck.Failed("The GitHub Web release index could not be downloaded or exceeded its size limit.");

            var candidates = ParseWebReleaseCandidates(releaseJson, allowPrerelease);
            if (candidates.Count == 0)
                return WebUpdateReleaseCheck.Failed("No v2rayN releases with Web update packages were found for this channel.");
            candidates.Sort((left, right) => new ServiceLib.Models.Dto.SemanticVersion(right.Version)
                .CompareTo(new ServiceLib.Models.Dto.SemanticVersion(left.Version)));
            var selected = candidates[0];
            if (!WebReleaseChannel.IsTrustedAssetUrl(selected.ManifestUrl, repository, selected.Tag, WebReleaseChannel.ManifestAssetName))
                return WebUpdateReleaseCheck.Failed("The Web release manifest URL did not match the trusted release channel.");

            var manifestJson = await downloader.TryDownloadString(
                selected.ManifestUrl,
                useProxy,
                $"v2rayN.Web/{WebBuildIdentity.Current.Version}",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(manifestJson) || manifestJson.Length > 1024 * 1024)
                return WebUpdateReleaseCheck.Failed("The Web release manifest could not be downloaded or exceeded its size limit.");
            var manifest = WebUpdatePackageStager.ParseManifest(manifestJson);
            if (manifest.Version != selected.Version)
                return WebUpdateReleaseCheck.Failed("The Web release manifest version does not match its release tag.");

            var currentRid = WebBuildIdentity.Current.Rid;
            var package = manifest.Packages.FirstOrDefault(item => item.Rid == currentRid);
            if (package is null)
            {
                return new WebUpdateReleaseCheck(false, manifest.Version, manifest.Commit, manifest, null,
                    "This v2rayN Web release does not include a package for the current runtime identifier.",
                    "No package matches runtime " + currentRid + ".");
            }
            var expectedAsset = WebUpdatePackageStager.AppOnlyAssetName(package.Rid);
            if (string.IsNullOrEmpty(expectedAsset)
                || package.Asset != expectedAsset
                || !selected.Assets.TryGetValue(package.Asset, out var releaseAsset)
                || releaseAsset.Url != package.Url
                || releaseAsset.Size != package.Size
                || !WebReleaseChannel.IsTrustedAssetUrl(package.Url, repository, selected.Tag, package.Asset))
            {
                return WebUpdateReleaseCheck.Failed("The Web package entry does not match a trusted asset attached to this release.");
            }

            var currentVersion = WebBuildIdentity.Current.Version;
            var updateAvailable = WebUpdatePackageStager.IsUpdateAvailable(
                currentVersion, manifest.Version, allowPrerelease, WebBuildIdentity.Current.Commit, manifest.Commit);
            var detail = updateAvailable
                ? "A newer v2rayN Web release is available."
                : new ServiceLib.Models.Dto.SemanticVersion(currentVersion)
                    .CompareTo(new ServiceLib.Models.Dto.SemanticVersion(manifest.Version)) > 0
                    ? "This Web build is newer than the latest release in the selected channel."
                    : "The v2rayN Web build is up to date.";
            return new WebUpdateReleaseCheck(updateAvailable, manifest.Version, manifest.Commit, manifest, package, null, detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException or ArgumentException)
        {
            return WebUpdateReleaseCheck.Failed(exception.Message);
        }
    }

    /// <summary>
    /// Extracts official release candidates that carry a Web update manifest from a GitHub
    /// "list releases" response. Legacy <c>web-v*</c> tags are intentionally ignored.
    /// </summary>
    internal static List<WebReleaseCandidate> ParseWebReleaseCandidates(string releaseJson, bool allowPrerelease)
    {
        using var document = JsonDocument.Parse(releaseJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The GitHub Web release index has an invalid format.");

        var candidates = new List<WebReleaseCandidate>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var isPrerelease = release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean();
            if (!WebUpdatePackageStager.ShouldConsiderRelease(isPrerelease, allowPrerelease)) continue;
            var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            if (!WebReleaseChannel.TryParseReleaseTag(tag, out var version)) continue;
            var assets = new Dictionary<string, (string Url, long Size)>(StringComparer.Ordinal);
            if (release.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
                    var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                        ? parsedSize : 0;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url)) assets[name] = (url, size);
                }
            }
            if (assets.TryGetValue(WebReleaseChannel.ManifestAssetName, out var manifestAsset))
                candidates.Add(new WebReleaseCandidate(tag!, version, isPrerelease, manifestAsset.Url, assets));
        }
        return candidates;
    }

    private async Task<WebUpdateStage> StageWebUpdateAsync(
        WebUpdateReleaseCheck check,
        bool useProxy,
        CancellationToken cancellationToken,
        bool batch)
    {
        if (check.Manifest is null || check.Package is null)
            throw new InvalidDataException("The v2rayN Web update manifest or runtime package was not checked.");
        var deployment = GetWebUpdateDeployment();
        if (!deployment.CanInstall) throw new InvalidOperationException(deployment.InstallReasonKey ?? "Web update installation is unavailable.");

        var id = Guid.NewGuid().ToString("N");
        var archivePath = Utils.GetTempPath($"web-update-{id}.zip");
        var packageDirectory = Utils.GetTempPath($"web-update-stage-{id}");
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current Web executable path is unavailable.");
        var installDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))
            ?? throw new InvalidOperationException("The current Web executable has no install directory.");
        var installParent = Path.GetDirectoryName(installDirectory)
            ?? throw new InvalidOperationException("The current Web install directory has no parent.");
        var candidateDirectory = Path.Combine(installParent, $".v2rayn-web-candidate-{id}");
        var backupDirectory = Path.Combine(installParent, $".v2rayn-web-backup-{id}");

        try
        {
            PublishWebUpdateProgress("downloading", false, false, null, check.Manifest.Version, batch);
            var downloader = new DownloadService();
            var downloadFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            downloader.Error += (_, args) => downloadFailure.TrySetResult(args.GetException());
            downloader.UpdateCompleted += (_, progress) =>
                PublishWebUpdateProgress("downloading", false, false, progress.Msg, check.Manifest.Version, batch);
            await downloader.DownloadFileAsync(new FileDownloadRequest
            {
                FileUrl = check.Package.Url,
                FilePath = archivePath,
                DisplayFileName = Path.GetFileName(archivePath),
            }, useProxy, cancellationToken);
            if (downloadFailure.Task.IsCompletedSuccessfully)
                throw new IOException("The v2rayN Web package download failed.", await downloadFailure.Task);
            if (!File.Exists(archivePath)) throw new IOException("The v2rayN Web package download did not produce an archive.");

            PublishWebUpdateProgress("verifying", false, false, null, check.Manifest.Version, batch);
            var identity = await WebUpdatePackageStager.VerifyAndExtractAsync(
                archivePath, packageDirectory, check.Manifest, check.Package, cancellationToken);
            if (identity.Rid != WebBuildIdentity.Current.Rid)
                throw new InvalidDataException("The staged v2rayN Web package has the wrong runtime identifier.");

            PublishWebUpdateProgress("staged", false, false, "The app-only package passed SHA-256, RID, identity, and archive checks.",
                check.Manifest.Version, batch);
            CopyVerifiedWebApp(packageDirectory, candidateDirectory);
            return new WebUpdateStage(archivePath, packageDirectory, candidateDirectory, backupDirectory,
                installDirectory, check.Manifest, check.Package);
        }
        catch
        {
            CleanupWebUpdateStageFiles(archivePath, packageDirectory, candidateDirectory);
            throw;
        }
    }

    private async Task ApplyStagedWebUpdateAsync(WebUpdateStage stage, CancellationToken cancellationToken, bool batch)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = CurrentCoreRuntime;
        var wasRunning = HasTrackedCoreProcesses
            && current.State is CoreRuntimeState.Running or CoreRuntimeState.Faulted;
        var runtimeIntent = new RuntimeRestartIntent(wasRunning, wasRunning ? current.ProfileId : null)
        {
            Reason = "Web application update",
        };
        var runtimeIntentPath = WebUpdateRuntimeStatePath;
        var progressPath = Utils.GetTempPath(WebUpdateProgressFile);
        var planPath = Utils.GetTempPath($"web-update-plan-{Guid.NewGuid():N}.json");
        var hostArguments = GetCurrentHostArguments();
        var plan = new NativeWebUpdatePlan(
            stage.InstallDirectory,
            stage.CandidateDirectory,
            stage.BackupDirectory,
            Path.Combine(Utils.StartupPath(), "v2rayN.Web.instance.lock"),
            GetCurrentHealthUri(hostArguments).ToString(),
            hostArguments,
            stage.Manifest.Version,
            stage.Manifest.Commit,
            stage.Package.Rid,
            WebBuildIdentity.Current.Version,
            runtimeIntentPath,
            progressPath,
            wasRunning);

        await WriteRuntimeIntentAsync(runtimeIntentPath, runtimeIntent);
        try
        {
            await WriteNativeWebUpdatePlanAsync(planPath, plan);
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The native Web executable path is unavailable.");
            var setsid = LinuxXdgBrowserOpener.FindExecutable("setsid")
                ?? throw new InvalidOperationException("The setsid executable is required for a native Web update.");
            var startInfo = new ProcessStartInfo
            {
                FileName = setsid,
                WorkingDirectory = stage.InstallDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(executablePath);
            startInfo.ArgumentList.Add("--apply-web-update");
            startInfo.ArgumentList.Add(planPath);
            using var helper = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The native Web update helper could not be started.");

            stage.TransferredToHelper = true;
            PublishWebUpdateProgress("stopping", false, false,
                "All network operations and package verification completed before Web/Core shutdown.",
                stage.Manifest.Version, batch);
            AddLog("update", $"Handing off the v2rayN Web update to helper process {helper.Id}.");
            _lifetime.StopApplication();
        }
        catch
        {
            await DeleteRuntimeIntentAsync(runtimeIntentPath);
            TryDeleteFile(planPath);
            throw;
        }
    }

    private WebUpdateDeployment GetWebUpdateDeployment()
    {
        var isContainer = File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("container"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
        var isSystemd = WebStopper.IsManagedBySystemd(Environment.ProcessId)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVOCATION_ID"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JOURNAL_STREAM"));
        var executable = Environment.ProcessPath;
        var nativeSingleFile = !string.IsNullOrWhiteSpace(executable)
            && Path.GetFileName(executable).Equals("v2rayN.Web", StringComparison.Ordinal)
            && File.Exists(executable)
            && AppContext.BaseDirectory == Path.GetDirectoryName(Path.GetFullPath(executable)) + Path.DirectorySeparatorChar;
        var installDirectory = nativeSingleFile && executable is not null ? Path.GetDirectoryName(executable)! : string.Empty;
        var writable = nativeSingleFile && !isSystemd && IsWritableInstallDirectory(installDirectory, executable!);
        return WebUpdateDeploymentPolicy.Evaluate(OperatingSystem.IsLinux(), isContainer, isSystemd, nativeSingleFile, writable);
    }

    private bool CanInstallWebUpdate() =>
        WebBuildIdentity.Current.Rid is "linux-x64" or "linux-arm64"
        && GetWebUpdateDeployment().CanInstall
        && GetWebUpdateRuntimeInstallReason() is null;

    private string? GetWebUpdateRuntimeInstallReason()
    {
        var snapshot = CurrentCoreRuntime;
        var activeChild = HasTrackedCoreProcesses;
        if (snapshot.State is CoreRuntimeState.Starting or CoreRuntimeState.Stopping or CoreRuntimeState.Restarting)
            return ApiMessageKeys.CoreRuntimeBusy;
        if (snapshot.State == CoreRuntimeState.Faulted && activeChild)
            return "maintenance.webUpdateRuntimeFaulted";
        if (snapshot.State == CoreRuntimeState.Running && !activeChild
            || snapshot.State == CoreRuntimeState.Stopped && activeChild)
            return "maintenance.webUpdateRuntimeFaulted";
        return null;
    }

    private static bool IsWritableInstallDirectory(string installDirectory, string executablePath)
    {
        try
        {
            if (new DirectoryInfo(installDirectory).LinkTarget is not null
                || new FileInfo(executablePath).LinkTarget is not null)
                return false;
            var probe = Path.Combine(installDirectory, $".v2rayn-web-write-probe-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private string GetWebUpdateInstallReasonKey() => GetWebUpdateDeployment().InstallReasonKey
        ?? GetWebUpdateRuntimeInstallReason()
        ?? "maintenance.webUpdateUnsupported";

    private static string ToDeploymentName(WebDeploymentKind kind) => kind switch
    {
        WebDeploymentKind.NativeWritable => "native-writable",
        WebDeploymentKind.SystemdManaged => "systemd-managed",
        WebDeploymentKind.Container => "container",
        WebDeploymentKind.ReadOnly => "native-read-only",
        _ => "unsupported",
    };

    private static async Task WriteNativeWebUpdatePlanAsync(string planPath, NativeWebUpdatePlan plan)
    {
        var temporary = planPath + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(plan, JsonOptions));
        File.Move(temporary, planPath);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(planPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string[] GetCurrentHostArguments()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        return WebLaunchOptions.Parse(args, OperatingSystem.IsLinux(), daemonEnvironment: false, containerEnvironment: false).HostArguments;
    }

    private Uri GetCurrentHealthUri(string[] hostArguments)
    {
        var url = _configuration[Microsoft.AspNetCore.Hosting.WebHostDefaults.ServerUrlsKey]
            ?? hostArguments.Select((argument, index) => (argument, index))
                .Where(item => item.argument == "--urls" && item.index + 1 < hostArguments.Length)
                .Select(item => hostArguments[item.index + 1]).FirstOrDefault()
            ?? hostArguments.FirstOrDefault(argument => argument.StartsWith("--urls=", StringComparison.Ordinal))?[7..]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? "http://127.0.0.1:5080";
        foreach (var item in url.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = item.Replace("*", "127.0.0.1", StringComparison.Ordinal)
                .Replace("+", "127.0.0.1", StringComparison.Ordinal);
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttp)
                return new UriBuilder(uri) { Host = "127.0.0.1", Path = "/api/health" }.Uri;
        }
        return new Uri("http://127.0.0.1:5080/api/health");
    }

    private static void CopyVerifiedWebApp(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        File.Copy(Path.Combine(source, "v2rayN.Web"), Path.Combine(destination, "v2rayN.Web"));
        File.Copy(Path.Combine(source, "v2rayN.Web.build.json"), Path.Combine(destination, "v2rayN.Web.build.json"));
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(Path.Combine(destination, "v2rayN.Web"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static void CleanupWebUpdateStage(WebUpdateStage? stage)
    {
        if (stage is null) return;
        CleanupWebUpdateStageFiles(stage.ArchivePath, stage.PackageDirectory,
            stage.TransferredToHelper ? null : stage.CandidateDirectory);
    }

    private static void CleanupWebUpdateStageFiles(string archive, string packageDirectory, string? candidateDirectory)
    {
        TryDeleteFile(archive);
        TryDeleteDirectory(packageDirectory);
        if (candidateDirectory is not null) TryDeleteDirectory(candidateDirectory);
    }

    private void PublishWebUpdateProgress(
        string phase,
        bool complete,
        bool success,
        string? detail,
        string? version = null,
        bool batch = false)
    {
        var progress = new CoreUpdateProgressView(WebUpdateTarget, phase, complete, success,
            HasTrackedCoreProcesses, version, detail, batch);
        _updateProgress[WebUpdateTarget] = progress;
        _events.Publish("core-update-progress", progress);
        AddLog("update", $"v2rayN Web update phase: {phase}");
        try
        {
            var path = Utils.GetTempPath(WebUpdateProgressFile);
            var temporary = path + $".{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(progress, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AddLog("update", $"Could not persist Web update progress: {exception.Message}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal sealed record WebUpdateReleaseCheck(
        bool IsUpdateAvailable,
        string? Version,
        string? Commit,
        WebUpdateManifest? Manifest,
        WebUpdatePackage? Package,
        string? Error,
        string? Detail)
    {
        public static WebUpdateReleaseCheck Failed(string detail) => new(false, null, null, null, null, detail, detail);
    }

    internal sealed record WebUpdateStage(
        string ArchivePath,
        string PackageDirectory,
        string CandidateDirectory,
        string BackupDirectory,
        string InstallDirectory,
        WebUpdateManifest Manifest,
        WebUpdatePackage Package)
    {
        public bool TransferredToHelper { get; set; }
    }

    internal sealed record WebReleaseCandidate(
        string Tag,
        string Version,
        bool IsPrerelease,
        string ManifestUrl,
        IReadOnlyDictionary<string, (string Url, long Size)> Assets);
}
