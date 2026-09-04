using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reforged.App.Brains;

public sealed record BorinReforgedBootstrapStatus(
    string State,
    string Detail,
    string CardsRoot,
    string StatePath,
    string RepositoryFingerprint,
    int CardCount,
    bool RepositoryChanged,
    DateTimeOffset? UpdatedUtc)
{
    public string Summary =>
        $"{State}: {CardCount} compact RealmGate cards • " +
        $"{(RepositoryChanged ? "repository changed — fresh evidence required" : "repository baseline current")} • {Detail}";

    public static BorinReforgedBootstrapStatus Empty(string repositoryRoot = "") =>
        new(
            "Not initialized",
            "The compact RealmGate knowledge bootstrap has not been initialized.",
            BorinReforgedKnowledgePaths.GetCardsRoot(repositoryRoot),
            BorinReforgedKnowledgePaths.GetStatePath(repositoryRoot),
            string.Empty,
            0,
            false,
            null);
}

public sealed record BorinReforgedBootstrapSnapshot(
    BorinKnowledgeSubsystem Subsystem,
    string PromptText,
    IReadOnlyList<string> CardPaths,
    BorinReforgedBootstrapStatus Status)
{
    public string Summary => CardPaths.Count == 0
        ? Status.Summary
        : $"{CardPaths.Count} compact cards for {Subsystem} • {Status.State}";

    public static BorinReforgedBootstrapSnapshot Empty(string repositoryRoot = "") =>
        new(
            BorinKnowledgeSubsystem.General,
            "No compact RealmGate bootstrap cards were supplied for this turn.",
            Array.Empty<string>(),
            BorinReforgedBootstrapStatus.Empty(repositoryRoot));
}

public interface IBorinReforgedKnowledgeBootstrap
{
    Task<BorinReforgedBootstrapStatus> InitializeAsync(
        string repositoryRoot,
        CancellationToken token = default);

    Task<BorinReforgedBootstrapStatus> GetStatusAsync(
        string repositoryRoot,
        CancellationToken token = default);

    Task<BorinReforgedBootstrapStatus> RefreshAsync(
        string repositoryRoot,
        CancellationToken token = default);

    Task<BorinReforgedBootstrapSnapshot> PrepareAsync(
        string repositoryRoot,
        string request,
        CancellationToken token = default);
}

/// <summary>
/// Loads a small deterministic Reforged orientation before the larger repository index is
/// consulted. The cards are project files under AI/Knowledge/Reforged. The service writes
/// only a fingerprint ledger under AI/Memory/Knowledge and never modifies source or gameplay data.
/// </summary>
public sealed class BorinReforgedKnowledgeBootstrap : IBorinReforgedKnowledgeBootstrap
{
    private const int StateVersion = 1;
    private const int MaximumCardCharacters = 7000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> FingerprintExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".json", ".jsonl", ".md", ".txt", ".xml", ".csproj", ".sln", ".ps1"
    };

    private static readonly string[] FingerprintRoots =
    [
        "Apps/Source",
        "Core/Source",
        "Librarium",
        "Data/Classic",
        "Runtime",
        "Tests",
        "AI/Knowledge/Reforged",
    ];

    private static readonly string[] ExcludedSegments =
    [
        "/bin/", "/obj/", "/build/", "/release/", "/backups/", "/.git/", "/ai/memory/", "/ai/models/"
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private BorinReforgedBootstrapStatus? _cachedStatus;
    private DateTimeOffset _lastStatusCheckUtc;

    public async Task<BorinReforgedBootstrapStatus> InitializeAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        await _gate.WaitAsync(token);
        try
        {
            if (_cachedStatus is not null &&
                DateTimeOffset.UtcNow - _lastStatusCheckUtc < TimeSpan.FromMinutes(5))
            {
                return _cachedStatus;
            }

            var cards = await LoadManifestAsync(root, token);
            var currentFingerprint = await ComputeRepositoryFingerprintAsync(root, token);
            var stored = await LoadStateAsync(root, token);
            if (stored is null)
            {
                var created = new BootstrapStateDocument(
                    StateVersion,
                    currentFingerprint,
                    DateTimeOffset.UtcNow,
                    cards.Cards.Count);
                await SaveStateAsync(root, created, token);
                _cachedStatus = CreateStatus(
                    root,
                    "Ready",
                    "Compact orientation created. Exact project claims still require current file evidence.",
                    currentFingerprint,
                    cards.Cards.Count,
                    repositoryChanged: false,
                    created.UpdatedUtc);
                _lastStatusCheckUtc = DateTimeOffset.UtcNow;
                return _cachedStatus;
            }

            var changed = !string.Equals(
                stored.RepositoryFingerprint,
                currentFingerprint,
                StringComparison.OrdinalIgnoreCase);
            _cachedStatus = CreateStatus(
                root,
                changed ? "Stale baseline" : "Ready",
                changed
                    ? "Repository content changed since the last bootstrap refresh. Cards remain useful for routing, but indexed claims must be refreshed with /learn changed or checked through fresh read-only evidence."
                    : "Compact orientation is ready. Exact project claims still require current file evidence.",
                currentFingerprint,
                cards.Cards.Count,
                changed,
                stored.UpdatedUtc);
            _lastStatusCheckUtc = DateTimeOffset.UtcNow;
            return _cachedStatus;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<BorinReforgedBootstrapStatus> GetStatusAsync(
        string repositoryRoot,
        CancellationToken token = default) =>
        InitializeAsync(repositoryRoot, token);

    public async Task<BorinReforgedBootstrapStatus> RefreshAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        await _gate.WaitAsync(token);
        try
        {
            var cards = await LoadManifestAsync(root, token);
            var fingerprint = await ComputeRepositoryFingerprintAsync(root, token);
            var state = new BootstrapStateDocument(
                StateVersion,
                fingerprint,
                DateTimeOffset.UtcNow,
                cards.Cards.Count);
            await SaveStateAsync(root, state, token);
            _cachedStatus = CreateStatus(
                root,
                "Ready",
                "Bootstrap baseline refreshed. Run /learn changed after source updates so the larger persistent index catches up.",
                fingerprint,
                cards.Cards.Count,
                repositoryChanged: false,
                state.UpdatedUtc);
            _lastStatusCheckUtc = DateTimeOffset.UtcNow;
            return _cachedStatus;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinReforgedBootstrapSnapshot> PrepareAsync(
        string repositoryRoot,
        string request,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        var status = await InitializeAsync(root, token);
        var manifest = await LoadManifestAsync(root, token);
        var subsystem = BorinIntentRouter.ClassifySubsystem(request);
        var normalized = string.IsNullOrWhiteSpace(request)
            ? string.Empty
            : request.Trim().ToLowerInvariant();

        var selected = manifest.Cards
            .Where(card => card.Always || CardMatches(card, subsystem, normalized))
            .OrderByDescending(card => card.Priority)
            .ThenBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var cards = new List<string>();
        var paths = new List<string>();
        foreach (var descriptor in selected)
        {
            token.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(Path.Combine(BorinReforgedKnowledgePaths.GetCardsRoot(root), descriptor.Path));
            if (!IsUnderRoot(fullPath, BorinReforgedKnowledgePaths.GetCardsRoot(root)) || !File.Exists(fullPath))
                continue;

            var text = await File.ReadAllTextAsync(fullPath, token);
            if (text.Length > MaximumCardCharacters)
                text = text[..MaximumCardCharacters] + "\n[card truncated]";
            var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            paths.Add(relative);
            cards.Add($"Knowledge card `{relative}` ({descriptor.Subsystem}):\n{text.Trim()}");
        }

        var staleNotice = status.RepositoryChanged
            ? "IMPORTANT: the repository changed after the bootstrap baseline. Use these cards only for architecture and routing. Verify all current implementation/status claims through fresh read-only evidence, current reports, and exact file/line citations."
            : "These cards are compact orientation. They never replace fresh source evidence, decoded provenance, runtime consumers, reports, or validation output.";

        var prompt = cards.Count == 0
            ? staleNotice
            : staleNotice + "\n\n" + string.Join("\n\n", cards);

        return new BorinReforgedBootstrapSnapshot(subsystem, prompt, paths, status);
    }

    private static bool CardMatches(
        BootstrapCardDescriptor card,
        BorinKnowledgeSubsystem subsystem,
        string normalizedRequest)
    {
        if (card.Subsystem.Equals("General", StringComparison.OrdinalIgnoreCase))
            return true;
        if (card.Subsystem.Equals(subsystem.ToString(), StringComparison.OrdinalIgnoreCase))
            return true;
        return card.Terms.Any(term =>
            normalizedRequest.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static BorinReforgedBootstrapStatus CreateStatus(
        string root,
        string state,
        string detail,
        string fingerprint,
        int cardCount,
        bool repositoryChanged,
        DateTimeOffset? updatedUtc) =>
        new(
            state,
            detail,
            BorinReforgedKnowledgePaths.GetCardsRoot(root),
            BorinReforgedKnowledgePaths.GetStatePath(root),
            fingerprint,
            cardCount,
            repositoryChanged,
            updatedUtc);

    private static async Task<BootstrapManifestDocument> LoadManifestAsync(
        string root,
        CancellationToken token)
    {
        var path = BorinReforgedKnowledgePaths.GetManifestPath(root);
        if (!File.Exists(path))
            throw new FileNotFoundException("RealmGate bootstrap manifest was not found.", path);

        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<BootstrapManifestDocument>(stream, JsonOptions, token)
            ?? throw new InvalidDataException("RealmGate bootstrap manifest was empty.");
        if (manifest.Version != StateVersion || manifest.Cards.Count == 0)
            throw new InvalidDataException("RealmGate bootstrap manifest has an unsupported version or no cards.");
        return manifest;
    }

    private static async Task<BootstrapStateDocument?> LoadStateAsync(
        string root,
        CancellationToken token)
    {
        var path = BorinReforgedKnowledgePaths.GetStatePath(root);
        if (!File.Exists(path))
            return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<BootstrapStateDocument>(stream, JsonOptions, token);
    }

    private static async Task SaveStateAsync(
        string root,
        BootstrapStateDocument state,
        CancellationToken token)
    {
        var path = BorinReforgedKnowledgePaths.GetStatePath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, token);
            await stream.FlushAsync(token);
        }
        File.Move(temporary, path, overwrite: true);
    }

    private static async Task<string> ComputeRepositoryFingerprintAsync(
        string root,
        CancellationToken token)
    {
        return await Task.Run(() =>
        {
            using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var relativeRoot in FingerprintRoots)
            {
                token.ThrowIfCancellationRequested();
                var fullRoot = Path.Combine(root, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(fullRoot))
                {
                    Append(incremental, relativeRoot + "|missing\n");
                    continue;
                }

                long count = 0;
                long totalBytes = 0;
                long newestTicks = 0;
                foreach (var path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    var normalized = path.Replace('\\', '/').ToLowerInvariant();
                    if (ExcludedSegments.Any(segment => normalized.Contains(segment, StringComparison.Ordinal)))
                        continue;
                    if (!FingerprintExtensions.Contains(Path.GetExtension(path)))
                        continue;
                    try
                    {
                        var info = new FileInfo(path);
                        count++;
                        totalBytes += info.Length;
                        newestTicks = Math.Max(newestTicks, info.LastWriteTimeUtc.Ticks);
                    }
                    catch (IOException)
                    {
                        // A concurrently written report should not make startup fatal.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Inaccessible project artifacts are skipped and exact evidence remains required.
                    }
                }
                Append(incremental, $"{relativeRoot}|{count}|{totalBytes}|{newestTicks}\n");
            }
            return Convert.ToHexString(incremental.GetHashAndReset());
        }, token);
    }

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
        var root = Path.GetFullPath(repositoryRoot.Trim());
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Repository root was not found: {root}");
        return root;
    }

    private sealed record BootstrapManifestDocument(
        int Version,
        IReadOnlyList<BootstrapCardDescriptor> Cards);

    private sealed record BootstrapCardDescriptor(
        string Id,
        string Subsystem,
        string Path,
        bool Always,
        int Priority,
        IReadOnlyList<string> Terms);

    private sealed record BootstrapStateDocument(
        int Version,
        string RepositoryFingerprint,
        DateTimeOffset UpdatedUtc,
        int CardCount);
}

public static class BorinReforgedKnowledgePaths
{
    public static string GetCardsRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Knowledge", "Reforged");

    public static string GetManifestPath(string repositoryRoot) =>
        Path.Combine(GetCardsRoot(repositoryRoot), "BootstrapManifest.json");

    public static string GetStatePath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Memory", "Knowledge", "reforged-bootstrap-state.json");
}
