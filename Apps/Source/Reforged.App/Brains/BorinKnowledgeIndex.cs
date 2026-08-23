using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

public static class BorinKnowledgeAuthority
{
    public const string DecodedSource = "Decoded source";
    public const string LibrariumAuthority = "Librarium authority";
    public const string GeneratedClassic = "Generated Data/Classic";
    public const string Runtime = "Runtime source";
    public const string Validation = "Validation/report evidence";
    public const string ImportedReference = "Imported reference-only";
    public const string Documentation = "Project documentation";
    public const string Other = "Other approved project text";
}

public sealed record BorinKnowledgeIndexStatus(
    string State,
    string Detail,
    string StoragePath,
    int IndexedFiles,
    int IndexedChunks,
    int ProcessedFiles,
    int TotalFiles,
    int ChangedFiles,
    int SkippedFiles,
    int FailedFiles,
    string CurrentFile,
    DateTimeOffset? UpdatedUtc)
{
    public bool IsRunning => State.Equals("Indexing", StringComparison.OrdinalIgnoreCase);

    public int ProgressPercent => TotalFiles <= 0
        ? 0
        : Math.Clamp((int)Math.Round(ProcessedFiles * 100d / TotalFiles), 0, 100);

    public string Summary
    {
        get
        {
            var progress = IsRunning ? $" • {ProgressPercent}% ({ProcessedFiles}/{TotalFiles})" : string.Empty;
            var current = string.IsNullOrWhiteSpace(CurrentFile) ? string.Empty : $" • {CurrentFile}";
            return $"{State}: {IndexedFiles} files • {IndexedChunks} chunks • {ChangedFiles} changed • {SkippedFiles} unchanged • {FailedFiles} failed{progress}{current} — {Detail}";
        }
    }

    public static BorinKnowledgeIndexStatus NotBuilt(string repositoryRoot) =>
        new(
            "Not built",
            "Run /learn reforged to create Borin's persistent project index.",
            BorinKnowledgeIndexPaths.GetIndexPath(repositoryRoot),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            null);
}

public sealed record BorinKnowledgeEvidenceItem(
    string RelativePath,
    int StartLine,
    int EndLine,
    string Authority,
    string Text,
    double Score,
    BorinKnowledgeConfidence Confidence = BorinKnowledgeConfidence.Proven,
    IReadOnlyList<BorinKnowledgeProvenance>? Sources = null,
    string Relationship = "")
{
    public string Location => StartLine == EndLine
        ? $"{RelativePath}:{StartLine}"
        : $"{RelativePath}:{StartLine}-{EndLine}";

    public string Display =>
        $"[{Confidence.ToDisplayText()}] [{Authority}] {Location}" +
        (string.IsNullOrWhiteSpace(Relationship) ? string.Empty : $" • {Relationship}") +
        $"\n{Text}";
}

public sealed record BorinKnowledgeIndexSnapshot(
    string RepositoryRoot,
    string Query,
    IReadOnlyList<BorinKnowledgeEvidenceItem> Matches,
    string StatusDetail)
{
    public string Summary => Matches.Count == 0
        ? StatusDetail
        : $"{Matches.Count} indexed knowledge chunks retrieved for this turn.";

    public string PromptText => Matches.Count == 0
        ? StatusDetail
        : string.Join("\n\n", Matches.Select((match, index) =>
            $"Indexed knowledge {index + 1}:\n{match.Display}"));

    public static BorinKnowledgeIndexSnapshot Empty(
        string repositoryRoot = "",
        string query = "",
        string detail = "No indexed Reforged knowledge was supplied for this turn.") =>
        new(repositoryRoot, query, Array.Empty<BorinKnowledgeEvidenceItem>(), detail);
}

public interface IBorinKnowledgeIndex
{
    event Action<BorinKnowledgeIndexStatus>? StatusChanged;

    Task<BorinKnowledgeIndexStatus> GetStatusAsync(
        string repositoryRoot,
        CancellationToken token = default);

    Task<BorinKnowledgeIndexStatus> BuildAsync(
        string repositoryRoot,
        bool changedOnly,
        CancellationToken token = default);

    Task<BorinKnowledgeIndexSnapshot> SearchAsync(
        string repositoryRoot,
        string query,
        int maximumResults = 8,
        CancellationToken token = default);
}

/// <summary>
/// Persistent, read-only project knowledge index for Borin. It reads approved project text,
/// writes only its own index under AI/Memory/Knowledge, and retrieves small ranked chunks for
/// the local GGUF prompt. It never modifies source, gameplay data, reports, or logs.
/// </summary>
public sealed class BorinKnowledgeIndex : IBorinKnowledgeIndex
{
    private const int MaximumFileBytes = 8 * 1024 * 1024;
    private const int MaximumChunkCharacters = 3200;
    private const int MaximumChunkLines = 60;
    private const int CheckpointInterval = 25;
    private const int IndexVersion = 1;
    private const long LargeLegacyIndexBytes = 64L * 1024 * 1024;
    private const int QueryCacheCapacity = 16;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex SearchTokenPattern = new(
        "[A-Za-z0-9_./-]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "will", "your", "you", "are", "was", "were",
        "for", "but", "not", "can", "could", "would", "should", "into", "about", "what", "when", "where", "why",
        "how", "our", "now", "then", "just", "like", "want", "need", "please", "borin", "majormud", "reforged",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".json", ".jsonl", ".md", ".txt", ".log", ".xml",
        ".props", ".targets", ".sln", ".csproj", ".ps1", ".bat", ".cmd",
        ".yml", ".yaml", ".sql", ".csv", ".ini", ".config"
    };

    private static readonly string[] ExcludedSegments =
    [
        "/.git/", "/.vs/", "/bin/", "/obj/", "/node_modules/", "/packages/",
        "/backups/", "/build/", "/release/", "/ai/models/", "/ai/memory/knowledge/"
    ];

    private static readonly string[] ApprovedPrefixes =
    [
        "apps/source/", "core/source/", "librarium/", "data/classic/", "decoded/",
        "decodeddata/", "worldgroupnative/", "reverseengineering/", "reports/", "tools/",
        "sim/", "simulator/", "tests/", "docs/", "documentation/", "imported/", "imports/",
        "ai/knowledge/reforged/", "ai/knowledge/majormud/"
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private readonly object _cacheSync = new();
    private BorinKnowledgeIndexDocument? _cachedSearchDocument;
    private string _cachedSearchPath = string.Empty;
    private long _cachedSearchLength;
    private long _cachedSearchWriteTicks;
    private readonly Dictionary<string, BorinKnowledgeIndexSnapshot> _queryCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _queryCacheOrder = new();

    public event Action<BorinKnowledgeIndexStatus>? StatusChanged;

    public async Task<BorinKnowledgeIndexStatus> GetStatusAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        var indexPath = BorinKnowledgeIndexPaths.GetIndexPath(root);
        if (!File.Exists(indexPath))
            return BorinKnowledgeIndexStatus.NotBuilt(root);

        var sidecarPath = BorinKnowledgeIndexPaths.GetStatusPath(root);
        if (File.Exists(sidecarPath))
        {
            try
            {
                await using var sidecarStream = File.OpenRead(sidecarPath);
                var sidecar = await JsonSerializer.DeserializeAsync<BorinKnowledgeStatusSidecar>(
                    sidecarStream,
                    JsonOptions,
                    token);
                if (sidecar is not null)
                {
                    return new BorinKnowledgeIndexStatus(
                        sidecar.IsComplete ? "Ready" : "Incomplete",
                        sidecar.IsComplete
                            ? "Persistent index is ready. Search metadata was read without loading the full knowledge corpus."
                            : "The previous learning pass was interrupted. Run /learn reforged to resume.",
                        indexPath,
                        sidecar.IndexedFiles,
                        sidecar.IndexedChunks,
                        0,
                        0,
                        sidecar.LastChangedFiles,
                        sidecar.LastSkippedFiles,
                        sidecar.LastFailedFiles,
                        string.Empty,
                        sidecar.UpdatedUtc == default ? null : sidecar.UpdatedUtc);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Fall through to the legacy status path. A bad sidecar must never
                // prevent access to the authoritative index.
                _ = exception;
            }
        }

        try
        {
            var file = new FileInfo(indexPath);
            if (file.Length >= LargeLegacyIndexBytes)
            {
                return new BorinKnowledgeIndexStatus(
                    "Ready",
                    $"Large legacy index detected ({file.Length / (1024d * 1024d):0} MB). " +
                    "Startup skipped full deserialization; exact counts will be cached after the first indexed search or the next /learn changed pass.",
                    indexPath,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    string.Empty,
                    file.LastWriteTimeUtc);
            }

            var document = await LoadDocumentAsync(indexPath, token);
            SaveStatusSidecar(root, document);
            return CreateStatus(
                document.IsComplete ? "Ready" : "Incomplete",
                document.IsComplete
                    ? "Persistent index is ready. Use /learn changed after repository updates."
                    : "The previous learning pass was interrupted. Run /learn reforged to resume.",
                indexPath,
                document,
                0,
                0,
                document.LastChangedFiles,
                document.LastSkippedFiles,
                document.LastFailedFiles,
                string.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new BorinKnowledgeIndexStatus(
                "Faulted",
                $"The knowledge index could not be read safely: {exception.Message}",
                indexPath,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                string.Empty,
                null);
        }
    }

    public async Task<BorinKnowledgeIndexStatus> BuildAsync(
        string repositoryRoot,
        bool changedOnly,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        await _gate.WaitAsync(token);
        await _searchGate.WaitAsync(token);
        try
        {
            InvalidateSearchCache();
            return await Task.Run(() => BuildCore(root, changedOnly, token), token);
        }
        finally
        {
            _searchGate.Release();
            _gate.Release();
        }
    }

    public async Task<BorinKnowledgeIndexSnapshot> SearchAsync(
        string repositoryRoot,
        string query,
        int maximumResults = 8,
        CancellationToken token = default)
    {
        var root = NormalizeRepositoryRoot(repositoryRoot);
        maximumResults = Math.Clamp(maximumResults, 1, 20);
        if (string.IsNullOrWhiteSpace(query))
            return BorinKnowledgeIndexSnapshot.Empty(root, query, "No knowledge query was supplied.");

        var indexPath = BorinKnowledgeIndexPaths.GetIndexPath(root);
        if (!File.Exists(indexPath))
        {
            return BorinKnowledgeIndexSnapshot.Empty(
                root,
                query,
                "Borin's persistent knowledge index has not been built. Run /learn reforged first.");
        }

        var file = new FileInfo(indexPath);
        var normalizedQuery = query.Trim();
        var cacheKey = $"{file.Length}|{file.LastWriteTimeUtc.Ticks}|{maximumResults}|{normalizedQuery.ToLowerInvariant()}";
        lock (_cacheSync)
        {
            if (_queryCache.TryGetValue(cacheKey, out var cached))
                return cached with { StatusDetail = cached.StatusDetail + " Result reused from Borin's warm query cache." };
        }

        BorinKnowledgeIndexDocument document;
        try
        {
            document = await GetSearchDocumentAsync(root, indexPath, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return BorinKnowledgeIndexSnapshot.Empty(
                root,
                query,
                $"Borin's knowledge index could not be read safely: {exception.Message}");
        }

        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
            return BorinKnowledgeIndexSnapshot.Empty(root, query, "The knowledge query contained no searchable terms.");

        var matches = new List<BorinKnowledgeEvidenceItem>();
        foreach (var chunk in document.Chunks)
        {
            token.ThrowIfCancellationRequested();
            var score = ScoreChunk(chunk, normalizedQuery, queryTerms);
            if (score <= 0)
                continue;

            matches.Add(new BorinKnowledgeEvidenceItem(
                chunk.RelativePath,
                chunk.StartLine,
                chunk.EndLine,
                chunk.Authority,
                NormalizeSnippet(chunk.Text, 2200),
                score));
        }

        var ranked = matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.StartLine)
            .Take(maximumResults)
            .ToArray();

        var detail = ranked.Length == 0
            ? $"The warm persistent index contains {document.Files.Count} files and {document.Chunks.Count} chunks, but none matched this question."
            : $"Retrieved {ranked.Length} of {document.Chunks.Count} indexed chunks from the warm in-process index. Exact paths and line ranges are shown.";
        var snapshot = new BorinKnowledgeIndexSnapshot(root, query, ranked, detail);
        RememberQuery(cacheKey, snapshot);
        return snapshot;
    }

    private async Task<BorinKnowledgeIndexDocument> GetSearchDocumentAsync(
        string repositoryRoot,
        string indexPath,
        CancellationToken token)
    {
        var file = new FileInfo(indexPath);
        lock (_cacheSync)
        {
            if (_cachedSearchDocument is not null &&
                string.Equals(_cachedSearchPath, indexPath, StringComparison.OrdinalIgnoreCase) &&
                _cachedSearchLength == file.Length &&
                _cachedSearchWriteTicks == file.LastWriteTimeUtc.Ticks)
            {
                return _cachedSearchDocument;
            }
        }

        await _searchGate.WaitAsync(token);
        try
        {
            file.Refresh();
            lock (_cacheSync)
            {
                if (_cachedSearchDocument is not null &&
                    string.Equals(_cachedSearchPath, indexPath, StringComparison.OrdinalIgnoreCase) &&
                    _cachedSearchLength == file.Length &&
                    _cachedSearchWriteTicks == file.LastWriteTimeUtc.Ticks)
                {
                    return _cachedSearchDocument;
                }
            }

            SetStatus(new BorinKnowledgeIndexStatus(
                "Loading",
                file.Length >= LargeLegacyIndexBytes
                    ? $"Warming the large persistent index once ({file.Length / (1024d * 1024d):0} MB). Later searches reuse it without deserializing again."
                    : "Loading the persistent index into the in-process search cache.",
                indexPath,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                file.LastWriteTimeUtc));

            var document = await LoadDocumentAsync(indexPath, token);
            SaveStatusSidecar(repositoryRoot, document);
            lock (_cacheSync)
            {
                _cachedSearchDocument = document;
                _cachedSearchPath = indexPath;
                _cachedSearchLength = file.Length;
                _cachedSearchWriteTicks = file.LastWriteTimeUtc.Ticks;
                _queryCache.Clear();
                _queryCacheOrder.Clear();
            }

            SetStatus(CreateStatus(
                document.IsComplete ? "Ready" : "Incomplete",
                $"Warm search cache ready: {document.Files.Count} files and {document.Chunks.Count} chunks. Future searches skip full index deserialization.",
                indexPath,
                document,
                0,
                0,
                document.LastChangedFiles,
                document.LastSkippedFiles,
                document.LastFailedFiles,
                string.Empty));
            return document;
        }
        finally
        {
            _searchGate.Release();
        }
    }

    private void RememberQuery(string key, BorinKnowledgeIndexSnapshot snapshot)
    {
        lock (_cacheSync)
        {
            if (_queryCache.ContainsKey(key))
                return;
            _queryCache[key] = snapshot;
            _queryCacheOrder.Enqueue(key);
            while (_queryCacheOrder.Count > QueryCacheCapacity)
            {
                var oldest = _queryCacheOrder.Dequeue();
                _queryCache.Remove(oldest);
            }
        }
    }

    private void InvalidateSearchCache()
    {
        lock (_cacheSync)
        {
            _cachedSearchDocument = null;
            _cachedSearchPath = string.Empty;
            _cachedSearchLength = 0;
            _cachedSearchWriteTicks = 0;
            _queryCache.Clear();
            _queryCacheOrder.Clear();
        }
    }

    private BorinKnowledgeIndexStatus BuildCore(
        string repositoryRoot,
        bool changedOnly,
        CancellationToken token)
    {
        if (!Directory.Exists(repositoryRoot))
            throw new DirectoryNotFoundException($"Repository root was not found: {repositoryRoot}");

        var indexPath = BorinKnowledgeIndexPaths.GetIndexPath(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        var document = LoadDocument(indexPath);
        document.Version = IndexVersion;
        document.RepositoryRoot = repositoryRoot;
        document.IsComplete = false;
        document.LastError = string.Empty;
        document.UpdatedUtc = DateTimeOffset.UtcNow;

        var candidates = EnumerateApprovedFiles(repositoryRoot, token).ToArray();
        var candidateSet = new HashSet<string>(
            candidates.Select(candidate => candidate.RelativePath),
            StringComparer.OrdinalIgnoreCase);

        var staleFiles = document.Files.Keys
            .Where(path => !candidateSet.Contains(path))
            .ToArray();
        foreach (var stale in staleFiles)
            RemoveFile(document, stale);

        var processed = 0;
        var changed = 0;
        var skipped = 0;
        var failed = 0;
        var mode = changedOnly ? "changed-file refresh" : "complete repository learning pass";
        SetStatus(CreateStatus(
            "Indexing",
            $"Starting {mode}. Existing unchanged chunks will be reused.",
            indexPath,
            document,
            processed,
            candidates.Length,
            changed,
            skipped,
            failed,
            string.Empty));

        try
        {
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                processed++;
                var current = candidate.RelativePath;
                try
                {
                    if (document.Files.TryGetValue(current, out var existing) &&
                        existing.Length == candidate.Length &&
                        existing.LastWriteUtcTicks == candidate.LastWriteUtcTicks)
                    {
                        skipped++;
                    }
                    else
                    {
                        var contentHash = ComputeHash(candidate.FullPath);
                        if (existing is not null &&
                            existing.Length == candidate.Length &&
                            string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
                        {
                            existing.LastWriteUtcTicks = candidate.LastWriteUtcTicks;
                            skipped++;
                        }
                        else
                        {
                            RemoveFile(document, current);
                            var chunks = CreateChunks(candidate.FullPath, current, ClassifyAuthority(current), token);
                            foreach (var chunk in chunks)
                                document.Chunks.Add(chunk);
                            document.Files[current] = new BorinKnowledgeFileEntry
                            {
                                RelativePath = current,
                                Length = candidate.Length,
                                LastWriteUtcTicks = candidate.LastWriteUtcTicks,
                                ContentHash = contentHash,
                                Authority = ClassifyAuthority(current),
                                ChunkIds = chunks.Select(chunk => chunk.Id).ToList(),
                            };
                            changed++;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    failed++;
                }

                if (processed % CheckpointInterval == 0 || processed == candidates.Length)
                {
                    document.LastChangedFiles = changed;
                    document.LastSkippedFiles = skipped;
                    document.LastFailedFiles = failed;
                    document.UpdatedUtc = DateTimeOffset.UtcNow;
                    SaveDocument(indexPath, document);
                    SaveStatusSidecar(repositoryRoot, document);
                    SetStatus(CreateStatus(
                        "Indexing",
                        $"Running {mode}; the checkpoint is resumable.",
                        indexPath,
                        document,
                        processed,
                        candidates.Length,
                        changed,
                        skipped,
                        failed,
                        current));
                }
            }

            document.IsComplete = true;
            document.LastError = string.Empty;
            document.LastChangedFiles = changed;
            document.LastSkippedFiles = skipped;
            document.LastFailedFiles = failed;
            document.UpdatedUtc = DateTimeOffset.UtcNow;
            SaveDocument(indexPath, document);
            SaveStatusSidecar(repositoryRoot, document);
            InvalidateSearchCache();
            var ready = CreateStatus(
                "Ready",
                $"{mode} complete. Only relevant chunks will be supplied to Qwen.",
                indexPath,
                document,
                candidates.Length,
                candidates.Length,
                changed,
                skipped,
                failed,
                string.Empty);
            SetStatus(ready);
            return ready;
        }
        catch (OperationCanceledException)
        {
            document.IsComplete = false;
            document.LastError = "Canceled; run /learn reforged to resume from saved checkpoints.";
            document.LastChangedFiles = changed;
            document.LastSkippedFiles = skipped;
            document.LastFailedFiles = failed;
            document.UpdatedUtc = DateTimeOffset.UtcNow;
            SaveDocument(indexPath, document);
            SaveStatusSidecar(repositoryRoot, document);
            InvalidateSearchCache();
            SetStatus(CreateStatus(
                "Canceled",
                document.LastError,
                indexPath,
                document,
                processed,
                candidates.Length,
                changed,
                skipped,
                failed,
                processed > 0 && processed <= candidates.Length ? candidates[processed - 1].RelativePath : string.Empty));
            throw;
        }
        catch (Exception exception)
        {
            document.IsComplete = false;
            document.LastError = exception.Message;
            document.LastChangedFiles = changed;
            document.LastSkippedFiles = skipped;
            document.LastFailedFiles = failed + 1;
            document.UpdatedUtc = DateTimeOffset.UtcNow;
            SaveDocument(indexPath, document);
            SaveStatusSidecar(repositoryRoot, document);
            InvalidateSearchCache();
            SetStatus(CreateStatus(
                "Faulted",
                $"Indexing failed safely and can be resumed: {exception.Message}",
                indexPath,
                document,
                processed,
                candidates.Length,
                changed,
                skipped,
                failed + 1,
                string.Empty));
            throw;
        }
    }

    private static IEnumerable<BorinKnowledgeFileCandidate> EnumerateApprovedFiles(
        string repositoryRoot,
        CancellationToken token)
    {
        foreach (var file in Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            if (!IsApprovedFile(relative))
                continue;

            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if (!info.Exists || info.Length <= 0 || info.Length > MaximumFileBytes)
                    continue;
            }
            catch
            {
                continue;
            }

            yield return new BorinKnowledgeFileCandidate(
                file,
                relative,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
        }
    }

    private static bool IsApprovedFile(string relativePath)
    {
        var normalized = "/" + relativePath.Replace('\\', '/').ToLowerInvariant().TrimStart('/');
        if (ExcludedSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!AllowedExtensions.Contains(Path.GetExtension(relativePath)))
            return false;

        var withoutSlash = normalized.TrimStart('/');
        if (!withoutSlash.Contains('/'))
            return true;
        return ApprovedPrefixes.Any(prefix => withoutSlash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string ClassifyAuthority(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').ToLowerInvariant();
        if (path.Contains("imported", StringComparison.Ordinal) ||
            path.Contains("imports/", StringComparison.Ordinal) ||
            path.Contains("reference", StringComparison.Ordinal) ||
            path.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase))
            return BorinKnowledgeAuthority.ImportedReference;
        if (path.StartsWith("ai/knowledge/reforged/", StringComparison.Ordinal) ||
            path.StartsWith("ai/knowledge/majormud/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.Documentation;
        if (path.StartsWith("librarium/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.LibrariumAuthority;
        if (path.StartsWith("data/classic/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.GeneratedClassic;
        if (path.Contains("decoded", StringComparison.Ordinal) ||
            path.Contains("worldgroupnative", StringComparison.Ordinal) ||
            path.Contains("reverseengineering", StringComparison.Ordinal) ||
            path.Contains("/wcc", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.DecodedSource;
        if (path.StartsWith("apps/source/reforged.server", StringComparison.Ordinal) ||
            path.StartsWith("core/source/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.Runtime;
        if (path.StartsWith("reports/", StringComparison.Ordinal) ||
            path.StartsWith("tests/", StringComparison.Ordinal) ||
            path.StartsWith("sim/", StringComparison.Ordinal) ||
            path.StartsWith("simulator/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.Validation;
        if (path.EndsWith(".md", StringComparison.Ordinal) ||
            path.StartsWith("docs/", StringComparison.Ordinal) ||
            path.StartsWith("documentation/", StringComparison.Ordinal))
            return BorinKnowledgeAuthority.Documentation;
        return BorinKnowledgeAuthority.Other;
    }

    private static List<BorinKnowledgeChunkEntry> CreateChunks(
        string fullPath,
        string relativePath,
        string authority,
        CancellationToken token)
    {
        var lines = File.ReadAllLines(fullPath);
        var chunks = new List<BorinKnowledgeChunkEntry>();
        var buffer = new StringBuilder();
        var startLine = 1;
        var endLine = 0;
        var lineCount = 0;

        void Flush()
        {
            if (buffer.Length == 0)
                return;
            var text = buffer.ToString().Trim();
            if (text.Length > 0)
            {
                chunks.Add(new BorinKnowledgeChunkEntry
                {
                    Id = CreateChunkId(relativePath, startLine, endLine, text),
                    RelativePath = relativePath,
                    StartLine = startLine,
                    EndLine = Math.Max(startLine, endLine),
                    Authority = authority,
                    Text = text,
                });
            }
            buffer.Clear();
            lineCount = 0;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var lineNumber = index + 1;
            var line = lines[index];
            if (line.Length > MaximumChunkCharacters)
            {
                Flush();
                for (var offset = 0; offset < line.Length; offset += MaximumChunkCharacters)
                {
                    var length = Math.Min(MaximumChunkCharacters, line.Length - offset);
                    var segment = line.Substring(offset, length).Trim();
                    if (segment.Length == 0)
                        continue;
                    chunks.Add(new BorinKnowledgeChunkEntry
                    {
                        Id = CreateChunkId(relativePath, lineNumber, lineNumber, segment),
                        RelativePath = relativePath,
                        StartLine = lineNumber,
                        EndLine = lineNumber,
                        Authority = authority,
                        Text = segment,
                    });
                }
                startLine = lineNumber + 1;
                endLine = lineNumber;
                continue;
            }

            if (buffer.Length == 0)
                startLine = lineNumber;
            if (buffer.Length + line.Length + Environment.NewLine.Length > MaximumChunkCharacters ||
                lineCount >= MaximumChunkLines)
            {
                Flush();
                startLine = lineNumber;
            }

            buffer.AppendLine(line);
            endLine = lineNumber;
            lineCount++;
        }

        Flush();
        return chunks;
    }

    private static double ScoreChunk(
        BorinKnowledgeChunkEntry chunk,
        string normalizedQuery,
        IReadOnlyList<string> queryTerms)
    {
        var score = 0d;
        foreach (var term in queryTerms)
        {
            if (chunk.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 6;
            var occurrences = CountOccurrences(chunk.Text, term, 5);
            score += occurrences * 1.5;
        }

        if (normalizedQuery.Length >= 4 && chunk.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 16;
        if (normalizedQuery.Length >= 4 && chunk.RelativePath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 12;
        if (chunk.Authority == BorinKnowledgeAuthority.ImportedReference)
            score -= 0.5;
        return score;
    }

    private static int CountOccurrences(string text, string value, int maximum)
    {
        var count = 0;
        var index = 0;
        while (count < maximum && (index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += Math.Max(1, value.Length);
        }
        return count;
    }

    private static IReadOnlyList<string> Tokenize(string value) =>
        SearchTokenPattern.Matches(value)
            .Select(match => match.Value.Trim('.', '/', '-').ToLowerInvariant())
            .Where(term => term.Length >= 2 && !SearchStopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string CreateChunkId(string relativePath, int startLine, int endLine, string text)
    {
        var source = $"{relativePath}|{startLine}|{endLine}|{text}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant()[..24];
    }

    private static void RemoveFile(BorinKnowledgeIndexDocument document, string relativePath)
    {
        if (!document.Files.TryGetValue(relativePath, out var existing))
            return;
        var ids = new HashSet<string>(existing.ChunkIds, StringComparer.OrdinalIgnoreCase);
        document.Chunks.RemoveAll(chunk => ids.Contains(chunk.Id));
        document.Files.Remove(relativePath);
    }

    private static BorinKnowledgeIndexDocument LoadDocument(string indexPath)
    {
        if (!File.Exists(indexPath))
            return new BorinKnowledgeIndexDocument();
        try
        {
            var document = JsonSerializer.Deserialize<BorinKnowledgeIndexDocument>(
                               File.ReadAllText(indexPath),
                               JsonOptions)
                           ?? new BorinKnowledgeIndexDocument();
            return NormalizeDocument(document);
        }
        catch
        {
            return new BorinKnowledgeIndexDocument();
        }
    }

    private static async Task<BorinKnowledgeIndexDocument> LoadDocumentAsync(
        string indexPath,
        CancellationToken token)
    {
        await using var stream = File.OpenRead(indexPath);
        var document = await JsonSerializer.DeserializeAsync<BorinKnowledgeIndexDocument>(stream, JsonOptions, token)
                       ?? new BorinKnowledgeIndexDocument();
        return NormalizeDocument(document);
    }

    private static BorinKnowledgeIndexDocument NormalizeDocument(BorinKnowledgeIndexDocument document)
    {
        document.Files = document.Files is null
            ? new Dictionary<string, BorinKnowledgeFileEntry>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, BorinKnowledgeFileEntry>(document.Files, StringComparer.OrdinalIgnoreCase);
        document.Chunks ??= [];
        foreach (var file in document.Files.Values)
            file.ChunkIds ??= [];
        return document;
    }

    private static void SaveDocument(string indexPath, BorinKnowledgeIndexDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        var tempPath = indexPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(document, JsonOptions), new UTF8Encoding(false));
        File.Move(tempPath, indexPath, true);
    }

    private static void SaveStatusSidecar(string repositoryRoot, BorinKnowledgeIndexDocument document)
    {
        var statusPath = BorinKnowledgeIndexPaths.GetStatusPath(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
        var sidecar = new BorinKnowledgeStatusSidecar
        {
            Version = document.Version,
            UpdatedUtc = document.UpdatedUtc,
            IsComplete = document.IsComplete,
            IndexedFiles = document.Files.Count,
            IndexedChunks = document.Chunks.Count,
            LastChangedFiles = document.LastChangedFiles,
            LastSkippedFiles = document.LastSkippedFiles,
            LastFailedFiles = document.LastFailedFiles,
        };
        var temporaryPath = statusPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(sidecar, JsonOptions), new UTF8Encoding(false));
        File.Move(temporaryPath, statusPath, true);
    }

    private static string NormalizeRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root was not provided.", nameof(repositoryRoot));
        return Path.GetFullPath(repositoryRoot.Trim().Trim('"'));
    }

    private static string NormalizeSnippet(string value, int maximumCharacters)
    {
        var normalized = string.Join(
            "\n",
            value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.TrimEnd()));
        if (normalized.Length <= maximumCharacters)
            return normalized;
        return normalized[..maximumCharacters] + "\n...[indexed chunk shortened for prompt safety]";
    }

    private static BorinKnowledgeIndexStatus CreateStatus(
        string state,
        string detail,
        string indexPath,
        BorinKnowledgeIndexDocument document,
        int processed,
        int total,
        int changed,
        int skipped,
        int failed,
        string currentFile) =>
        new(
            state,
            detail,
            indexPath,
            document.Files.Count,
            document.Chunks.Count,
            processed,
            total,
            changed,
            skipped,
            failed,
            currentFile,
            document.UpdatedUtc == default ? null : document.UpdatedUtc);

    private void SetStatus(BorinKnowledgeIndexStatus status) => StatusChanged?.Invoke(status);

    private sealed class BorinKnowledgeStatusSidecar
    {
        public int Version { get; set; } = IndexVersion;
        public DateTimeOffset UpdatedUtc { get; set; }
        public bool IsComplete { get; set; }
        public int IndexedFiles { get; set; }
        public int IndexedChunks { get; set; }
        public int LastChangedFiles { get; set; }
        public int LastSkippedFiles { get; set; }
        public int LastFailedFiles { get; set; }
    }

    private sealed record BorinKnowledgeFileCandidate(
        string FullPath,
        string RelativePath,
        long Length,
        long LastWriteUtcTicks);

    private sealed class BorinKnowledgeIndexDocument
    {
        public int Version { get; set; } = IndexVersion;
        public string RepositoryRoot { get; set; } = string.Empty;
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool IsComplete { get; set; }
        public string LastError { get; set; } = string.Empty;
        public int LastChangedFiles { get; set; }
        public int LastSkippedFiles { get; set; }
        public int LastFailedFiles { get; set; }
        public Dictionary<string, BorinKnowledgeFileEntry> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<BorinKnowledgeChunkEntry> Chunks { get; set; } = [];
    }

    private sealed class BorinKnowledgeFileEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Length { get; set; }
        public long LastWriteUtcTicks { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string Authority { get; set; } = BorinKnowledgeAuthority.Other;
        public List<string> ChunkIds { get; set; } = [];
    }

    private sealed class BorinKnowledgeChunkEntry
    {
        public string Id { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string Authority { get; set; } = BorinKnowledgeAuthority.Other;
        public string Text { get; set; } = string.Empty;
    }
}

internal static class BorinKnowledgeIndexPaths
{
    public static string GetIndexPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Memory", "Knowledge", "borin-knowledge-index.json");

    public static string GetStatusPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Memory", "Knowledge", "borin-knowledge-index.status.json");
}
