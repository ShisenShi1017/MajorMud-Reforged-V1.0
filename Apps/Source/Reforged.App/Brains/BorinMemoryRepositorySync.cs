using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Reforged.App.Services;

namespace Reforged.App.Brains;

internal sealed record BorinMemoryRepositorySyncMetadata(
    string CleanupId,
    DateTimeOffset Timestamp,
    string Server,
    string World,
    string Character,
    string SessionId,
    string Goal,
    string Room,
    int CharacterLevel,
    long ExperienceCurrent,
    int VerifiedObservationCount,
    IReadOnlyList<string> VerifiedLessons,
    string Reflection,
    string Summary);

internal sealed record BorinMemoryRepositorySyncResult(
    bool Succeeded,
    bool Changed,
    string State,
    string Detail,
    string CommitSha)
{
    public static BorinMemoryRepositorySyncResult Disabled(string detail) =>
        new(true, false, "Disabled", detail, string.Empty);

    public static BorinMemoryRepositorySyncResult Failed(string detail) =>
        new(false, false, "Failed", detail, string.Empty);
}

/// <summary>
/// Synchronizes Borin's bounded, durable memory to a dedicated private Git repository.
/// This component intentionally does not synchronize source code, credentials, raw terminal
/// journals, backup snapshots, model files, or generated Librarium/knowledge indexes.
/// Git authentication is delegated to the user's normal Git credential helper/SSH setup;
/// tokens and passwords are never accepted in the player configuration.
/// </summary>
internal sealed class BorinMemoryRepositorySync
{
    private const string RepositoryMaintenanceFileName = "maintenance.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<string> RootDurableFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "borin-memory.json",
        "borin-episodic-memory.json",
        "borin-social-memory.json",
        "borin-character-growth.json",
        "borin-initiative.json",
        "borin-living-character.json",
        "borin-voice.json",
        "borin-temporal-continuity.json",
        "borin-situational-awareness.json",
        "borin-natural-initiative.json",
        "borin-memory-consolidation.json",
        "borin-gameplay-learning.json",
        "borin-conversation-repair.json",
        "borin-group-conversation.json",
    };

    private static readonly HashSet<string> DurableKnowledgeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "borin-verified-learning.json",
        "borin-academy.json",
        "borin-major-mud-mastery.json",
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BorinMemoryRepositorySyncOutboxStore _outbox = new();
    private readonly IAppLogger _logger;
    private string _repositoryRoot = string.Empty;
    private string _memoryRoot = string.Empty;
    private string _checkoutRoot = string.Empty;
    private string _statePath = string.Empty;
    private BorinPlayerSettings _settings = new();

    public BorinMemoryRepositorySync(IAppLogger logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string CheckoutRoot => _checkoutRoot;

    public Task ConfigureAsync(string repositoryRoot, BorinPlayerSettings settings, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _memoryRoot = Path.Combine(_repositoryRoot, "AI", "Memory");
        _statePath = Path.Combine(_memoryRoot, ".borin-memory-sync-state.json");
        _settings = settings.Normalize();
        _checkoutRoot = ResolveCheckoutRoot(_repositoryRoot, _settings.MemoryRepositoryLocalPath);
        _outbox.Configure(_memoryRoot);
        return Task.CompletedTask;
    }

    public async Task<BorinMemoryRepositorySyncResult> PullAndRestoreAsync(CancellationToken token = default)
    {
        if (!_settings.MemoryRepositorySyncEnabled)
            return BorinMemoryRepositorySyncResult.Disabled("Private memory repository sync is disabled.");
        if (!_settings.MemoryRepositoryPullOnStartup)
            return BorinMemoryRepositorySyncResult.Disabled("Startup memory pull is disabled.");

        await _gate.WaitAsync(token);
        try
        {
            var validation = ValidateConfiguration();
            if (validation is not null)
                return BorinMemoryRepositorySyncResult.Failed(validation);

            var checkout = await RefreshCheckoutFromRemoteAsync(token);
            if (!checkout.Succeeded)
                return checkout;

            var restored = await RestoreRemoteMemoryWithoutOverwritingUnsyncedLocalChangesAsync(token);
            var head = await ReadHeadAsync(token);
            return new BorinMemoryRepositorySyncResult(
                true,
                restored > 0,
                "Pulled",
                restored == 0
                    ? "The private Borin memory repository is current; no local durable-memory files needed restoration."
                    : $"Pulled the private Borin memory repository and restored {restored} safe durable-memory file(s).",
                head);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Error("Borin private memory pull failed", exception);
            return BorinMemoryRepositorySyncResult.Failed($"Private memory pull failed safely: {exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StageCleanupSnapshotAsync(
        BorinMemoryRepositorySyncMetadata metadata,
        CancellationToken token = default)
    {
        if (!_settings.MemoryRepositorySyncEnabled || !_settings.MemoryRepositoryPushAfterCleanup)
            return;
        await _outbox.StageAsync(metadata, token);
    }

    public Task<BorinMemoryRepositorySyncOutboxStatus> GetPendingCleanupStatusAsync(CancellationToken token = default) =>
        _outbox.GetStatusAsync(token);

    public async Task<BorinMemoryRepositorySyncResult> PushCleanupSnapshotAsync(
        BorinMemoryRepositorySyncMetadata metadata,
        CancellationToken token = default)
    {
        if (!_settings.MemoryRepositorySyncEnabled)
            return BorinMemoryRepositorySyncResult.Disabled("Private memory repository sync is disabled.");
        if (!_settings.MemoryRepositoryPushAfterCleanup)
            return BorinMemoryRepositorySyncResult.Disabled("Cleanup-time memory push is disabled.");

        await _outbox.StageAsync(metadata, token);
        return await PushPendingCleanupSnapshotsAsync(token);
    }

    public async Task<BorinMemoryRepositorySyncResult> PushPendingCleanupSnapshotsAsync(CancellationToken token = default)
    {
        if (!_settings.MemoryRepositorySyncEnabled)
            return BorinMemoryRepositorySyncResult.Disabled("Private memory repository sync is disabled.");
        if (!_settings.MemoryRepositoryPushAfterCleanup)
            return BorinMemoryRepositorySyncResult.Disabled("Cleanup-time memory push is disabled.");

        await _gate.WaitAsync(token);
        try
        {
            var validation = ValidateConfiguration();
            if (validation is not null)
                return BorinMemoryRepositorySyncResult.Failed(validation);

            var pending = await _outbox.LoadPendingAsync(token);
            if (pending.Count == 0)
            {
                return new BorinMemoryRepositorySyncResult(
                    true,
                    false,
                    "NoPending",
                    "There are no confirmed cleanup memory snapshots waiting to be pushed.",
                    await ReadHeadAsync(token));
            }

            var checkout = await RefreshCheckoutFromRemoteAsync(token);
            if (!checkout.Succeeded)
                return checkout;

            // Pull remote memory into files that have not changed locally since the last sync.
            // Newer local durable memory is never overwritten by Git recovery.
            await RestoreRemoteMemoryWithoutOverwritingUnsyncedLocalChangesAsync(token);

            var captured = CaptureDurableMemoryIntoCheckout();
            var latest = pending.OrderBy(item => item.Timestamp).Last();
            await WriteManifestAsync(captured, latest, token);
            foreach (var item in pending)
                await WriteCleanupSummaryAsync(item, token);
            PruneCleanupSummaries();

            var identity = await EnsureRepositoryIdentityAsync(token);
            if (!identity.Succeeded)
                return identity;

            // First compare only durable memory/session content. Maintenance counters must not
            // manufacture a duplicate commit when an earlier push succeeded but Borin crashed
            // before clearing the local outbox.
            var add = await RunGitAsync(_checkoutRoot, ["add", "--all", "--", "memory", "manifest.json", "sessions"], token);
            if (!add.Succeeded)
                return BorinMemoryRepositorySyncResult.Failed($"Git could not stage Borin's durable memory: {SummarizeGitOutput(add.Output)}");

            var status = await RunGitAsync(_checkoutRoot, ["status", "--porcelain", "--", "memory", "manifest.json", "sessions"], token);
            if (!status.Succeeded)
                return BorinMemoryRepositorySyncResult.Failed($"Git could not inspect Borin's durable memory snapshot: {SummarizeGitOutput(status.Output)}");

            if (string.IsNullOrWhiteSpace(status.Output))
            {
                var unchangedHead = await ReadHeadAsync(token);
                await SaveSyncStateAsync(captured, unchangedHead, token);
                await _outbox.RemoveAsync(pending.Select(item => item.CleanupId), token);
                return new BorinMemoryRepositorySyncResult(
                    true,
                    false,
                    "NoChanges",
                    $"Borin's durable memory already matches the private repository; cleared {pending.Count} recovered pending cleanup snapshot(s) without creating a duplicate commit.",
                    unchangedHead);
            }

            var maintenance = await LoadRepositoryMaintenanceStateAsync(token);
            maintenance = maintenance with
            {
                UpdatedAt = DateTimeOffset.UtcNow,
                SuccessfulSnapshotsSinceHistoryCompaction = maintenance.SuccessfulSnapshotsSinceHistoryCompaction + 1,
            };
            await WriteRepositoryMaintenanceStateAsync(maintenance, token);
            var addMaintenance = await RunGitAsync(_checkoutRoot, ["add", "--", RepositoryMaintenanceFileName], token);
            if (!addMaintenance.Succeeded)
                return BorinMemoryRepositorySyncResult.Failed($"Git could not stage Borin's bounded history-maintenance state: {SummarizeGitOutput(addMaintenance.Output)}");

            var commitMessage = BuildCommitMessage(latest, pending.Count);
            var commit = await RunGitAsync(_checkoutRoot, ["commit", "-m", commitMessage], token);
            if (!commit.Succeeded)
                return BorinMemoryRepositorySyncResult.Failed($"Git could not commit Borin's durable memory: {SummarizeGitOutput(commit.Output)}");

            var push = await RunGitAsync(
                _checkoutRoot,
                ["push", "origin", $"HEAD:refs/heads/{_settings.MemoryRepositoryBranch}"],
                token);
            if (!push.Succeeded)
            {
                return BorinMemoryRepositorySyncResult.Failed(
                    $"Git committed the cleanup memory locally but could not push it. The durable outbox will retry without losing the snapshot: {SummarizeGitOutput(push.Output)}");
            }

            var head = await ReadHeadAsync(token);
            var historyCompacted = false;
            var compactionDetail = string.Empty;
            if (ShouldCompactHistory(
                    maintenance.SuccessfulSnapshotsSinceHistoryCompaction,
                    _settings.MemoryRepositoryHistoryCompactionEnabled,
                    _settings.MemoryRepositoryHistoryCompactionIntervalCommits))
            {
                var compacted = await CompactRemoteHistoryAsync(head, maintenance, token);
                if (compacted.Succeeded)
                {
                    head = compacted.CommitSha;
                    historyCompacted = true;
                    compactionDetail = " Remote Git history was compacted to the latest complete Borin memory snapshot.";
                }
                else
                {
                    compactionDetail = $" Memory was pushed, but history compaction was deferred safely: {compacted.Detail}";
                }
            }

            await SaveSyncStateAsync(captured, head, token);
            await _outbox.RemoveAsync(pending.Select(item => item.CleanupId), token);
            _ = RunGitAsync(_checkoutRoot, ["gc", "--auto"], CancellationToken.None);
            return new BorinMemoryRepositorySyncResult(
                true,
                true,
                historyCompacted ? "PushedAndCompacted" : "Pushed",
                $"Borin committed and pushed {captured.Count} bounded durable-memory file(s) plus {pending.Count} pending cleanup summary snapshot(s).{compactionDetail}",
                head);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Error("Borin private memory push failed", exception);
            return BorinMemoryRepositorySyncResult.Failed($"Private memory push failed safely; the local outbox is preserved for retry: {exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static bool IsDurableMemoryRelativePath(string relativePath)
    {
        var normalized = NormalizeRelative(relativePath);
        if (normalized.Length == 0 || normalized.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Backups/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Backups/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!normalized.Contains('/'))
            return RootDurableFiles.Contains(normalized);

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0].Equals("Knowledge", StringComparison.OrdinalIgnoreCase))
            return DurableKnowledgeFiles.Contains(parts[1]);

        if (parts.Length == 2 && parts[0].Equals("Player", StringComparison.OrdinalIgnoreCase))
            return parts[1].Equals("borin-newhaven-club-assignment.json", StringComparison.OrdinalIgnoreCase);

        if (parts.Length >= 3 && parts[0].Equals("Gameplay", StringComparison.OrdinalIgnoreCase))
            return parts[^1].Equals("borin-reflection-journal.json", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    internal static bool ShouldCompactHistory(int successfulSnapshotsSinceCompaction, bool enabled, int intervalCommits) =>
        enabled && successfulSnapshotsSinceCompaction >= Math.Clamp(intervalCommits, 10, 500);

    internal static bool IsCredentialFreeGitHubRemote(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return false;
        var value = remoteUrl.Trim();
        if (Regex.IsMatch(value, @"^git@github\.com:[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\.git)?$", RegexOptions.CultureInvariant))
            return true;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;
        if (!(uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;
        if (uri.UserInfo.Length > 0 && !uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            return false;
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;
        if (uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase) &&
            uri.UserInfo.Length > 0 && !uri.UserInfo.Equals("git", StringComparison.OrdinalIgnoreCase))
            return false;
        return uri.AbsolutePath.Count(character => character == '/') >= 2;
    }

    private string? ValidateConfiguration()
    {
        if (_repositoryRoot.Length == 0 || _memoryRoot.Length == 0 || _checkoutRoot.Length == 0)
            return "Borin's private memory repository sync has not been configured.";
        if (!IsCredentialFreeGitHubRemote(_settings.MemoryRepositoryRemoteUrl))
            return "The Borin memory remote must be a credential-free github.com HTTPS or SSH repository URL. Store authentication in Git Credential Manager or SSH, never in borin-player.json.";
        if (!Regex.IsMatch(_settings.MemoryRepositoryBranch, @"^[A-Za-z0-9][A-Za-z0-9._/-]{0,99}$", RegexOptions.CultureInvariant) ||
            _settings.MemoryRepositoryBranch.Contains("..", StringComparison.Ordinal) ||
            _settings.MemoryRepositoryBranch.EndsWith("/", StringComparison.Ordinal))
            return "The Borin memory repository branch name is invalid.";
        if (IsSameOrSubPath(_checkoutRoot, _repositoryRoot))
            return "The private Borin memory Git checkout must live outside the RealmGate source repository.";
        return null;
    }

    private async Task<BorinMemoryRepositorySyncResult> RefreshCheckoutFromRemoteAsync(CancellationToken token)
    {
        var git = await RunGitAsync(_repositoryRoot, ["--version"], token);
        if (!git.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed("Git is not available. Install Git for Windows and authenticate GitHub with Git Credential Manager or SSH.");

        var gitDirectory = Path.Combine(_checkoutRoot, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            if (Directory.Exists(_checkoutRoot) && Directory.EnumerateFileSystemEntries(_checkoutRoot).Any())
                return BorinMemoryRepositorySyncResult.Failed("The configured Borin memory checkout folder is not empty and is not a Git repository. Nothing was deleted.");

            Directory.CreateDirectory(Path.GetDirectoryName(_checkoutRoot)!);
            var clone = await RunGitAsync(
                Path.GetDirectoryName(_checkoutRoot)!,
                ["clone", "--no-tags", "--depth", "1", "--branch", _settings.MemoryRepositoryBranch, _settings.MemoryRepositoryRemoteUrl, _checkoutRoot],
                token);
            if (!clone.Succeeded)
            {
                return BorinMemoryRepositorySyncResult.Failed(
                    "The private Borin memory repository could not be cloned. Create the private GitHub repository with an initial README/main branch and make sure normal Git authentication can access it. " +
                    SummarizeGitOutput(clone.Output));
            }
        }

        var origin = await RunGitAsync(_checkoutRoot, ["remote", "get-url", "origin"], token);
        if (!origin.Succeeded || !RemotesEquivalent(origin.Output.Trim(), _settings.MemoryRepositoryRemoteUrl))
            return BorinMemoryRepositorySyncResult.Failed("The existing Borin memory checkout points at a different Git remote. The application will not silently retarget it.");

        var fetch = await RunGitAsync(
            _checkoutRoot,
            ["fetch", "--no-tags", "--depth", "1", "origin", $"refs/heads/{_settings.MemoryRepositoryBranch}"],
            token);
        if (!fetch.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed($"The private Borin memory branch could not be fetched: {SummarizeGitOutput(fetch.Output)}");

        var reset = await RunGitAsync(_checkoutRoot, ["reset", "--hard", "FETCH_HEAD"], token);
        if (!reset.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed($"The local Borin memory mirror could not be refreshed safely: {SummarizeGitOutput(reset.Output)}");

        var checkout = await RunGitAsync(_checkoutRoot, ["checkout", "-B", _settings.MemoryRepositoryBranch, "FETCH_HEAD"], token);
        if (!checkout.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed($"The Borin memory branch could not be selected: {SummarizeGitOutput(checkout.Output)}");

        return new BorinMemoryRepositorySyncResult(true, false, "RemoteReady", "Private Borin memory repository checkout is ready.", await ReadHeadAsync(token));
    }

    private async Task<int> RestoreRemoteMemoryWithoutOverwritingUnsyncedLocalChangesAsync(CancellationToken token)
    {
        var remoteMemoryRoot = Path.Combine(_checkoutRoot, "memory");
        if (!Directory.Exists(remoteMemoryRoot))
            return 0;

        Directory.CreateDirectory(_memoryRoot);
        var state = await LoadSyncStateAsync(token);
        var changed = false;
        var restored = 0;
        foreach (var remotePath in Directory.EnumerateFiles(remoteMemoryRoot, "*", SearchOption.AllDirectories))
        {
            var relative = NormalizeRelative(Path.GetRelativePath(remoteMemoryRoot, remotePath));
            if (!IsDurableMemoryRelativePath(relative))
                continue;

            var localPath = Path.Combine(_memoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            var remoteHash = ComputeSha256(remotePath);
            var safeToRestore = !File.Exists(localPath);
            if (!safeToRestore && state.FileHashesAtLastSync.TryGetValue(relative, out var previousHash))
                safeToRestore = ComputeSha256(localPath).Equals(previousHash, StringComparison.OrdinalIgnoreCase);

            if (!safeToRestore)
                continue;

            if (File.Exists(localPath) && ComputeSha256(localPath).Equals(remoteHash, StringComparison.OrdinalIgnoreCase))
            {
                state.FileHashesAtLastSync[relative] = remoteHash;
                changed = true;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            File.Copy(remotePath, localPath, overwrite: true);
            state.FileHashesAtLastSync[relative] = remoteHash;
            restored++;
            changed = true;
        }

        if (changed)
        {
            state = state with
            {
                LastPulledAt = DateTimeOffset.UtcNow,
                LastRemoteCommit = await ReadHeadAsync(token),
            };
            await SaveSyncStateDocumentAsync(state, token);
        }
        return restored;
    }

    private IReadOnlyList<MemoryFileEntry> CaptureDurableMemoryIntoCheckout()
    {
        var destinationRoot = Path.Combine(_checkoutRoot, "memory");
        if (Directory.Exists(destinationRoot))
            Directory.Delete(destinationRoot, recursive: true);
        Directory.CreateDirectory(destinationRoot);

        var captured = new List<MemoryFileEntry>();
        if (!Directory.Exists(_memoryRoot))
            return captured;

        foreach (var sourcePath in Directory.EnumerateFiles(_memoryRoot, "*", SearchOption.AllDirectories))
        {
            var relative = NormalizeRelative(Path.GetRelativePath(_memoryRoot, sourcePath));
            if (!IsDurableMemoryRelativePath(relative))
                continue;

            var destination = Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourcePath, destination, overwrite: true);
            captured.Add(new MemoryFileEntry(relative, new FileInfo(sourcePath).Length, ComputeSha256(sourcePath)));
        }

        return captured.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task WriteManifestAsync(
        IReadOnlyList<MemoryFileEntry> files,
        BorinMemoryRepositorySyncMetadata metadata,
        CancellationToken token)
    {
        var manifest = new MemoryManifest(
            1,
            DateTimeOffset.UtcNow,
            "Borin durable-memory snapshot. Raw terminal journals, backups, credentials, generated indexes, model files, and RealmGate source is intentionally excluded.",
            Normalize(metadata.Server, 120),
            Normalize(metadata.World, 120),
            Normalize(metadata.Character, 80),
            Normalize(metadata.SessionId, 100),
            files);
        await WriteJsonAtomicAsync(Path.Combine(_checkoutRoot, "manifest.json"), manifest, token);
    }

    private async Task WriteCleanupSummaryAsync(BorinMemoryRepositorySyncMetadata metadata, CancellationToken token)
    {
        var safeId = Regex.Replace(metadata.CleanupId ?? string.Empty, "[^A-Za-z0-9._-]", "-");
        if (safeId.Length == 0)
            safeId = $"cleanup-{metadata.Timestamp:yyyyMMddHHmmss}";
        safeId = safeId[..Math.Min(100, safeId.Length)];
        var year = metadata.Timestamp.UtcDateTime.Year.ToString("0000");
        var path = Path.Combine(_checkoutRoot, "sessions", year, safeId + ".json");
        var summary = new CleanupSummary(
            safeId,
            metadata.Timestamp,
            Normalize(metadata.Server, 120),
            Normalize(metadata.World, 120),
            Normalize(metadata.Character, 80),
            Normalize(metadata.SessionId, 100),
            Normalize(metadata.Goal, 700),
            Normalize(metadata.Room, 220),
            Math.Max(0, metadata.CharacterLevel),
            Math.Max(0, metadata.ExperienceCurrent),
            Math.Max(0, metadata.VerifiedObservationCount),
            metadata.VerifiedLessons.Take(8).Select(item => Normalize(item, 700)).Where(item => item.Length > 0).ToArray(),
            Normalize(metadata.Reflection, 1800),
            Normalize(metadata.Summary, 1800));
        await WriteJsonAtomicAsync(path, summary, token);
    }

    private void PruneCleanupSummaries()
    {
        var root = Path.Combine(_checkoutRoot, "sessions");
        if (!Directory.Exists(root))
            return;
        var summaries = Directory.EnumerateFiles(root, "cleanup-*.json", SearchOption.AllDirectories)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var stale in summaries.Skip(Math.Clamp(_settings.MemoryRepositorySessionSummaryLimit, 25, 1000)))
        {
            try { File.Delete(stale); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private async Task<RepositoryMaintenanceState> LoadRepositoryMaintenanceStateAsync(CancellationToken token)
    {
        var path = Path.Combine(_checkoutRoot, RepositoryMaintenanceFileName);
        if (!File.Exists(path))
            return new RepositoryMaintenanceState();
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<RepositoryMaintenanceState>(stream, JsonOptions, token)
                   ?? new RepositoryMaintenanceState();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("Borin memory repository maintenance state could not be read; compaction will resume conservatively", exception);
            return new RepositoryMaintenanceState();
        }
    }

    private Task WriteRepositoryMaintenanceStateAsync(RepositoryMaintenanceState state, CancellationToken token) =>
        WriteJsonAtomicAsync(Path.Combine(_checkoutRoot, RepositoryMaintenanceFileName), state, token);

    private async Task<BorinMemoryRepositorySyncResult> CompactRemoteHistoryAsync(
        string expectedRemoteHead,
        RepositoryMaintenanceState maintenance,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(expectedRemoteHead))
            return BorinMemoryRepositorySyncResult.Failed("The remote memory commit could not be identified for a lease-protected history compaction.");

        var compactedState = maintenance with
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            SuccessfulSnapshotsSinceHistoryCompaction = 0,
            LastHistoryCompactedAt = DateTimeOffset.UtcNow,
        };
        await WriteRepositoryMaintenanceStateAsync(compactedState, token);

        var stage = await RunGitAsync(_checkoutRoot, ["add", "--", RepositoryMaintenanceFileName], token);
        if (!stage.Succeeded)
        {
            await RunGitAsync(_checkoutRoot, ["reset", "--hard", expectedRemoteHead], CancellationToken.None);
            return BorinMemoryRepositorySyncResult.Failed($"Git could not stage Borin's compaction marker: {SummarizeGitOutput(stage.Output)}");
        }

        var tree = await RunGitAsync(_checkoutRoot, ["write-tree"], token);
        if (!tree.Succeeded || string.IsNullOrWhiteSpace(tree.Output))
        {
            await RunGitAsync(_checkoutRoot, ["reset", "--hard", expectedRemoteHead], CancellationToken.None);
            return BorinMemoryRepositorySyncResult.Failed($"Git could not create the compacted Borin memory tree: {SummarizeGitOutput(tree.Output)}");
        }

        var commit = await RunGitAsync(
            _checkoutRoot,
            ["commit-tree", tree.Output.Trim(), "-m", $"Borin memory snapshot: compact history {DateTimeOffset.UtcNow:yyyy-MM-dd}"],
            token);
        if (!commit.Succeeded || string.IsNullOrWhiteSpace(commit.Output))
        {
            await RunGitAsync(_checkoutRoot, ["reset", "--hard", expectedRemoteHead], CancellationToken.None);
            return BorinMemoryRepositorySyncResult.Failed($"Git could not create the compacted Borin memory root commit: {SummarizeGitOutput(commit.Output)}");
        }

        var compactedCommit = commit.Output.Trim();
        var lease = $"--force-with-lease=refs/heads/{_settings.MemoryRepositoryBranch}:{expectedRemoteHead}";
        var push = await RunGitAsync(
            _checkoutRoot,
            ["push", lease, "origin", $"{compactedCommit}:refs/heads/{_settings.MemoryRepositoryBranch}"],
            token);
        if (!push.Succeeded)
        {
            await RunGitAsync(_checkoutRoot, ["reset", "--hard", expectedRemoteHead], CancellationToken.None);
            return BorinMemoryRepositorySyncResult.Failed(
                $"The lease-protected Borin memory history compaction was rejected; no remote history was rewritten: {SummarizeGitOutput(push.Output)}");
        }

        var reset = await RunGitAsync(_checkoutRoot, ["reset", "--hard", compactedCommit], token);
        if (!reset.Succeeded)
            _logger.Info($"Borin memory history compacted remotely, but the local shallow mirror will refresh on the next sync: {SummarizeGitOutput(reset.Output)}");

        _ = RunGitAsync(_checkoutRoot, ["reflog", "expire", "--expire=now", "--all"], CancellationToken.None);
        _ = RunGitAsync(_checkoutRoot, ["gc", "--prune=now"], CancellationToken.None);
        return new BorinMemoryRepositorySyncResult(
            true,
            true,
            "HistoryCompacted",
            "Borin's private memory repository history was compacted to a single complete latest snapshot using a force-with-lease safety check.",
            compactedCommit);
    }

    private async Task<BorinMemoryRepositorySyncResult> EnsureRepositoryIdentityAsync(CancellationToken token)
    {
        var name = await RunGitAsync(_checkoutRoot, ["config", "--local", "user.name", "Borin"], token);
        if (!name.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed($"Git could not configure Borin's repository-local commit name: {SummarizeGitOutput(name.Output)}");
        var email = await RunGitAsync(_checkoutRoot, ["config", "--local", "user.email", "borin-memory@users.noreply.github.com"], token);
        if (!email.Succeeded)
            return BorinMemoryRepositorySyncResult.Failed($"Git could not configure Borin's repository-local commit email: {SummarizeGitOutput(email.Output)}");
        return new BorinMemoryRepositorySyncResult(true, false, "IdentityReady", "Repository-local Borin commit identity is configured.", string.Empty);
    }

    private async Task<string> ReadHeadAsync(CancellationToken token)
    {
        var result = await RunGitAsync(_checkoutRoot, ["rev-parse", "HEAD"], token);
        return result.Succeeded ? result.Output.Trim() : string.Empty;
    }

    private async Task<SyncState> LoadSyncStateAsync(CancellationToken token)
    {
        if (!File.Exists(_statePath))
            return new SyncState();
        try
        {
            await using var stream = File.OpenRead(_statePath);
            var state = await JsonSerializer.DeserializeAsync<SyncState>(stream, JsonOptions, token) ?? new SyncState();
            return state with
            {
                FileHashesAtLastSync = new Dictionary<string, string>(state.FileHashesAtLastSync, StringComparer.OrdinalIgnoreCase),
            };
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("Borin memory sync state could not be read; local memory will be preserved conservatively", exception);
            return new SyncState();
        }
    }

    private async Task SaveSyncStateAsync(IReadOnlyList<MemoryFileEntry> captured, string commit, CancellationToken token)
    {
        var hashes = captured.ToDictionary(item => item.Path, item => item.Sha256, StringComparer.OrdinalIgnoreCase);
        var state = new SyncState
        {
            LastPulledAt = DateTimeOffset.UtcNow,
            LastPushedAt = DateTimeOffset.UtcNow,
            LastRemoteCommit = commit,
            LastPushedCommit = commit,
            FileHashesAtLastSync = hashes,
        };
        await SaveSyncStateDocumentAsync(state, token);
    }

    private Task SaveSyncStateDocumentAsync(SyncState state, CancellationToken token) =>
        WriteJsonAtomicAsync(_statePath, state, token);

    private static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, token);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static string ResolveCheckoutRoot(string repositoryRoot, string configured)
    {
        string root;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            root = Environment.ExpandEnvironmentVariables(configured.Trim());
            if (!Path.IsPathRooted(root))
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                root = Path.Combine(local, root);
            }
        }
        else
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
                local = Path.GetTempPath();
            root = Path.Combine(local, "RealmGate", "BorinMemoryRepository");
        }
        return Path.GetFullPath(root);
    }

    private static bool IsSameOrSubPath(string candidate, string parent)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool RemotesEquivalent(string actual, string expected)
    {
        static string NormalizeRemote(string value) => value.Trim().TrimEnd('/').Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);
        return NormalizeRemote(actual).Equals(NormalizeRemote(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommitMessage(BorinMemoryRepositorySyncMetadata metadata, int pendingCount)
    {
        var world = Normalize(metadata.World, 40);
        if (world.Length == 0)
            world = "MajorMUD";
        return pendingCount <= 1
            ? $"Borin memory: {world} cleanup {metadata.Timestamp:yyyy-MM-dd HH:mm}"
            : $"Borin memory: {world} {pendingCount} cleanup snapshots through {metadata.Timestamp:yyyy-MM-dd HH:mm}";
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string NormalizeRelative(string path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static string Normalize(string? value, int maximumLength)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return normalized[..Math.Min(maximumLength, normalized.Length)];
    }

    private static string SummarizeGitOutput(string output)
    {
        var normalized = Regex.Replace(output ?? string.Empty, @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"(?i)(https://)([^/@\s]+)@", "$1[REDACTED]@");
        normalized = Regex.Replace(normalized, @"(?i)(token|password|passphrase|authorization)\s*[:=]\s*\S+", "$1=[REDACTED]");
        if (normalized.Length == 0)
            return "no additional Git detail";
        return normalized[..Math.Min(600, normalized.Length)];
    }

    private static async Task<GitResult> RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var output = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Path.GetTempPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                output.AppendLine(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                output.AppendLine(eventArgs.Data);
        };

        try
        {
            if (!process.Start())
                return new GitResult(false, -1, "Git could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
            return new GitResult(process.ExitCode == 0, process.ExitCode, output.ToString());
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
            throw;
        }
        catch (Exception exception)
        {
            return new GitResult(false, -1, exception.Message);
        }
    }

    private sealed record GitResult(bool Succeeded, int ExitCode, string Output);
    private sealed record MemoryFileEntry(string Path, long Length, string Sha256);
    private sealed record MemoryManifest(
        int SchemaVersion,
        DateTimeOffset CapturedAt,
        string Policy,
        string Server,
        string World,
        string Character,
        string SessionId,
        IReadOnlyList<MemoryFileEntry> Files);
    private sealed record CleanupSummary(
        string Id,
        DateTimeOffset Timestamp,
        string Server,
        string World,
        string Character,
        string SessionId,
        string Goal,
        string Room,
        int CharacterLevel,
        long ExperienceCurrent,
        int VerifiedObservationCount,
        IReadOnlyList<string> VerifiedLessons,
        string Reflection,
        string Summary);
    private sealed record RepositoryMaintenanceState
    {
        public int SchemaVersion { get; init; } = 1;
        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
        public int SuccessfulSnapshotsSinceHistoryCompaction { get; init; }
        public DateTimeOffset? LastHistoryCompactedAt { get; init; }
    }

    private sealed record SyncState
    {
        public DateTimeOffset? LastPulledAt { get; init; }
        public DateTimeOffset? LastPushedAt { get; init; }
        public string LastRemoteCommit { get; init; } = string.Empty;
        public string LastPushedCommit { get; init; } = string.Empty;
        public Dictionary<string, string> FileHashesAtLastSync { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
