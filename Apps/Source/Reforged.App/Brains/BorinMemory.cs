using System.IO;
using System.Text;
using System.Text.Json;
using Reforged.App.Services;

namespace Reforged.App.Brains;

public enum BorinMemoryCategory
{
    StandingInstruction,
    ProjectDecision,
    CurrentTask,
    Bug,
    WorkResult,
    ConversationSummary,
    Conversation,
}

public enum BorinMemoryEvidence
{
    Discussed,
    Decided,
    Installed,
    Built,
    Validated,
}

public enum BorinMemoryScope
{
    Permanent,
    Temporary,
}

public sealed class BorinMemoryEntry
{
    public string Id { get; set; } = CreateId();
    public BorinMemoryCategory Category { get; set; } = BorinMemoryCategory.Conversation;
    public BorinMemoryEvidence Evidence { get; set; } = BorinMemoryEvidence.Discussed;
    public BorinMemoryScope Scope { get; set; } = BorinMemoryScope.Temporary;
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = "Conversation";
    public bool Pinned { get; set; }
    public bool Archived { get; set; }
    public string SupersedesId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? LastRecalledAt { get; set; }
    public int RecallCount { get; set; }

    public string Display
    {
        get
        {
            var state = Archived ? "Archived" : "Active";
            var supersedes = string.IsNullOrWhiteSpace(SupersedesId)
                ? string.Empty
                : $" • supersedes {SupersedesId}";
            return $"{Id} • {Category} • {Evidence} • {Scope} • {state}{supersedes} • {UpdatedAt.LocalDateTime:g}\n{Text}";
        }
    }

    private static string CreateId() => Guid.NewGuid().ToString("N")[..8];
}

public sealed record BorinMemorySnapshot(
    IReadOnlyList<BorinMemoryEntry> RelevantMemories,
    int TotalCount,
    int DurableCount,
    int ConversationCount,
    int ArchivedCount,
    string StoragePath)
{
    public static BorinMemorySnapshot Empty { get; } =
        new([], 0, 0, 0, 0, string.Empty);

    public string Summary =>
        $"{TotalCount} memories • {DurableCount} durable • {ConversationCount} conversation • {ArchivedCount} archived";

    public string RecallSummary => RelevantMemories.Count == 0
        ? "Last answer used no saved memories."
        : $"Last answer used {RelevantMemories.Count} memories: {string.Join(", ", RelevantMemories.Select(memory => memory.Id))}";

    public string PromptText
    {
        get
        {
            if (RelevantMemories.Count == 0)
                return "No relevant persistent memories were recalled for this turn.";

            return string.Join(
                "\n",
                RelevantMemories.Select(memory =>
                    $"- [{memory.Id}] [{memory.Category}] [{memory.Evidence}] [{memory.Scope}] {memory.Text}"));
        }
    }
}

public interface IBorinMemoryStore
{
    string StoragePath { get; }

    Task<BorinMemorySnapshot> RecallAsync(
        string request,
        int maximum = 12,
        CancellationToken token = default);

    Task<IReadOnlyList<BorinMemoryEntry>> ListAsync(
        bool includeConversation = false,
        bool includeArchived = false,
        CancellationToken token = default);

    Task<BorinMemoryEntry> RememberAsync(
        BorinMemoryCategory category,
        string text,
        BorinMemoryEvidence evidence = BorinMemoryEvidence.Decided,
        bool pinned = true,
        string source = "User",
        CancellationToken token = default);

    Task<BorinMemoryEntry?> UpdateAsync(
        string id,
        string text,
        BorinMemoryCategory? category = null,
        CancellationToken token = default);

    Task<BorinMemoryEntry?> MarkEvidenceAsync(
        string id,
        BorinMemoryEvidence evidence,
        CancellationToken token = default);

    Task<BorinMemoryEntry?> ArchiveAsync(
        string id,
        string? result = null,
        CancellationToken token = default);

    Task<bool> ForgetAsync(string id, CancellationToken token = default);

    Task<int> ClearAsync(
        bool includeConversation = true,
        CancellationToken token = default);

    Task RecordTurnAsync(
        string userText,
        string borinText,
        CancellationToken token = default);
}

/// <summary>
/// Borin's local, user-controlled memory ledger. It intentionally uses an atomic
/// JSON file so Stage 4 adds no database or native dependency. The ledger lives
/// under AI/Memory and is never treated as authoritative gameplay data.
/// </summary>
public sealed class BorinMemoryStore : IBorinMemoryStore
{
    private const int MaximumEntries = 250;
    private const int MaximumRawConversationEntries = 24;
    private const int ConversationCompactionBatch = 8;
    private const int MaximumConversationSummaries = 24;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _memoryRoot;

    public BorinMemoryStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_memoryRoot, "borin-memory.json");
    }

    public string StoragePath { get; }

    public async Task<BorinMemorySnapshot> RecallAsync(
        string request,
        int maximum = 12,
        CancellationToken token = default)
    {
        maximum = Math.Clamp(maximum, 1, 30);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var requestTerms = Tokenize(request);
            var now = DateTimeOffset.Now;

            var selected = document.Entries
                .Where(entry => !entry.Archived)
                .Select(entry => new
                {
                    Entry = entry,
                    Score = Score(entry, requestTerms, now),
                })
                .Where(item => item.Entry.Pinned || item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Entry.UpdatedAt)
                .Take(maximum)
                .Select(item => item.Entry)
                .ToList();

            foreach (var entry in selected)
            {
                entry.LastRecalledAt = now;
                entry.RecallCount++;
            }

            if (selected.Count > 0)
                await SaveUnsafeAsync(document, token);

            var active = document.Entries.Where(entry => !entry.Archived).ToArray();
            var durableCount = active.Count(entry =>
                entry.Category != BorinMemoryCategory.Conversation &&
                entry.Category != BorinMemoryCategory.ConversationSummary);
            var conversationCount = active.Length - durableCount;
            var archivedCount = document.Entries.Count(entry => entry.Archived);
            return new BorinMemorySnapshot(
                selected.Select(Clone).ToArray(),
                document.Entries.Count,
                durableCount,
                conversationCount,
                archivedCount,
                StoragePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BorinMemoryEntry>> ListAsync(
        bool includeConversation = false,
        bool includeArchived = false,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            return document.Entries
                .Where(entry => includeArchived || !entry.Archived)
                .Where(entry => includeConversation ||
                    (entry.Category != BorinMemoryCategory.Conversation &&
                     entry.Category != BorinMemoryCategory.ConversationSummary))
                .OrderBy(entry => entry.Archived)
                .ThenByDescending(entry => entry.Pinned)
                .ThenByDescending(entry => entry.UpdatedAt)
                .Select(Clone)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMemoryEntry> RememberAsync(
        BorinMemoryCategory category,
        string text,
        BorinMemoryEvidence evidence = BorinMemoryEvidence.Decided,
        bool pinned = true,
        string source = "User",
        CancellationToken token = default)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Memory text cannot be empty.", nameof(text));

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = DateTimeOffset.Now;
            var activeSameCategory = document.Entries
                .Where(entry => !entry.Archived && entry.Category == category)
                .ToArray();

            var exact = activeSameCategory.FirstOrDefault(entry =>
                NormalizeForComparison(entry.Text).Equals(
                    NormalizeForComparison(text),
                    StringComparison.Ordinal));
            if (exact is not null)
            {
                exact.Evidence = Stronger(exact.Evidence, evidence);
                exact.Pinned |= pinned;
                exact.Source = string.IsNullOrWhiteSpace(source) ? exact.Source : source.Trim();
                exact.Scope = ScopeFor(category);
                exact.UpdatedAt = now;
                await SaveUnsafeAsync(document, token);
                return Clone(exact);
            }

            var similar = activeSameCategory
                .Select(entry => new { Entry = entry, Similarity = Similarity(entry.Text, text) })
                .Where(item => item.Similarity >= 0.82)
                .OrderByDescending(item => item.Similarity)
                .FirstOrDefault();
            if (similar is not null && !LooksContradictory(similar.Entry.Text, text))
            {
                var existing = similar.Entry;
                if (text.Length > existing.Text.Length)
                    existing.Text = text;
                existing.Evidence = Stronger(existing.Evidence, evidence);
                existing.Pinned |= pinned;
                existing.Source = string.IsNullOrWhiteSpace(source) ? existing.Source : source.Trim();
                existing.Scope = ScopeFor(category);
                existing.UpdatedAt = now;
                await SaveUnsafeAsync(document, token);
                return Clone(existing);
            }

            if (category == BorinMemoryCategory.CurrentTask)
            {
                foreach (var task in activeSameCategory)
                    ArchiveUnsafe(task, now);
            }

            string supersedesId = string.Empty;
            var conflict = activeSameCategory
                .Select(entry => new { Entry = entry, Similarity = SubjectSimilarity(entry.Text, text) })
                .Where(item => item.Similarity >= 0.68 && LooksContradictory(item.Entry.Text, text))
                .OrderByDescending(item => item.Similarity)
                .FirstOrDefault();
            if (conflict is not null)
            {
                supersedesId = conflict.Entry.Id;
                ArchiveUnsafe(conflict.Entry, now);
            }

            var created = new BorinMemoryEntry
            {
                Category = category,
                Evidence = evidence,
                Scope = ScopeFor(category),
                Text = text,
                Source = string.IsNullOrWhiteSpace(source) ? "User" : source.Trim(),
                Pinned = pinned,
                SupersedesId = supersedesId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            document.Entries.Add(created);

            Trim(document);
            await SaveUnsafeAsync(document, token);
            return Clone(created);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMemoryEntry?> UpdateAsync(
        string id,
        string text,
        BorinMemoryCategory? category = null,
        CancellationToken token = default)
    {
        id = id?.Trim() ?? string.Empty;
        text = NormalizeText(text);
        if (id.Length == 0 || text.Length == 0)
            return null;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var entry = document.Entries.FirstOrDefault(candidate =>
                candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            var targetCategory = category ?? entry.Category;
            var duplicate = document.Entries.FirstOrDefault(candidate =>
                !ReferenceEquals(candidate, entry) &&
                !candidate.Archived &&
                candidate.Category == targetCategory &&
                Similarity(candidate.Text, text) >= 0.90);
            if (duplicate is not null)
            {
                duplicate.Evidence = Stronger(duplicate.Evidence, entry.Evidence);
                duplicate.Pinned |= entry.Pinned;
                duplicate.UpdatedAt = DateTimeOffset.Now;
                ArchiveUnsafe(entry, DateTimeOffset.Now);
                await SaveUnsafeAsync(document, token);
                return Clone(duplicate);
            }

            entry.Category = targetCategory;
            entry.Scope = ScopeFor(targetCategory);
            entry.Text = text;
            entry.Archived = false;
            entry.ArchivedAt = null;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(document, token);
            return Clone(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMemoryEntry?> MarkEvidenceAsync(
        string id,
        BorinMemoryEvidence evidence,
        CancellationToken token = default)
    {
        id = id?.Trim() ?? string.Empty;
        if (id.Length == 0)
            return null;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var entry = document.Entries.FirstOrDefault(candidate =>
                candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            entry.Evidence = evidence;
            entry.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(document, token);
            return Clone(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMemoryEntry?> ArchiveAsync(
        string id,
        string? result = null,
        CancellationToken token = default)
    {
        id = id?.Trim() ?? string.Empty;
        if (id.Length == 0)
            return null;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var entry = document.Entries.FirstOrDefault(candidate =>
                candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            var now = DateTimeOffset.Now;
            ArchiveUnsafe(entry, now);
            if (!string.IsNullOrWhiteSpace(result))
            {
                document.Entries.Add(new BorinMemoryEntry
                {
                    Category = BorinMemoryCategory.WorkResult,
                    Evidence = entry.Evidence,
                    Scope = BorinMemoryScope.Permanent,
                    Text = NormalizeText(result),
                    Source = $"Completion of {entry.Id}",
                    Pinned = false,
                    SupersedesId = entry.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            Trim(document);
            await SaveUnsafeAsync(document, token);
            return Clone(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ForgetAsync(string id, CancellationToken token = default)
    {
        id = id?.Trim() ?? string.Empty;
        if (id.Length == 0)
            return false;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var removed = document.Entries.RemoveAll(entry =>
                entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                await SaveUnsafeAsync(document, token);
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ClearAsync(
        bool includeConversation = true,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var removed = includeConversation
                ? document.Entries.Count
                : document.Entries.RemoveAll(entry =>
                    entry.Category != BorinMemoryCategory.Conversation &&
                    entry.Category != BorinMemoryCategory.ConversationSummary);

            if (includeConversation)
                document.Entries.Clear();

            await SaveUnsafeAsync(document, token);
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordTurnAsync(
        string userText,
        string borinText,
        CancellationToken token = default)
    {
        userText = NormalizeText(userText);
        borinText = NormalizeText(borinText);
        if (userText.Length == 0 || borinText.Length == 0)
            return;

        var text = $"User: {TrimTo(userText, 1600)}\nBorin: {TrimTo(borinText, 2400)}";
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            document.Entries.Add(new BorinMemoryEntry
            {
                Category = BorinMemoryCategory.Conversation,
                Evidence = BorinMemoryEvidence.Discussed,
                Scope = BorinMemoryScope.Temporary,
                Text = text,
                Source = "Conversation",
                Pinned = false,
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now,
            });
            Trim(document);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BorinMemoryDocument> LoadUnsafeAsync(CancellationToken token)
    {
        Directory.CreateDirectory(_memoryRoot);
        if (!File.Exists(StoragePath))
            return new BorinMemoryDocument();

        try
        {
            await using var stream = new FileStream(
                StoragePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true);
            var document = await JsonSerializer.DeserializeAsync<BorinMemoryDocument>(stream, JsonOptions, token)
                ?? new BorinMemoryDocument();
            NormalizeLoadedDocument(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin memory ledger could not be read; starting with an empty in-memory ledger", exception);
            return new BorinMemoryDocument();
        }
    }

    private async Task SaveUnsafeAsync(BorinMemoryDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(_memoryRoot);
        document.Version = 2;
        document.UpdatedAt = DateTimeOffset.Now;

        var temporaryPath = Path.Combine(
            _memoryRoot,
            $"borin-memory.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                8192,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, token);
                await stream.FlushAsync(token);
            }

            File.Move(temporaryPath, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch { }
            }
        }
    }

    private static void NormalizeLoadedDocument(BorinMemoryDocument document)
    {
        foreach (var entry in document.Entries)
        {
            entry.Text = NormalizeText(entry.Text);
            entry.Source = string.IsNullOrWhiteSpace(entry.Source) ? "Imported ledger entry" : entry.Source.Trim();
            entry.Scope = ScopeFor(entry.Category);
            if (entry.Archived && entry.ArchivedAt is null)
                entry.ArchivedAt = entry.UpdatedAt;
        }
    }

    private static double Score(
        BorinMemoryEntry entry,
        IReadOnlySet<string> requestTerms,
        DateTimeOffset now)
    {
        var score = entry.Pinned ? 100d : 0d;
        score += entry.Category switch
        {
            BorinMemoryCategory.StandingInstruction => 55,
            BorinMemoryCategory.ProjectDecision => 45,
            BorinMemoryCategory.CurrentTask => 35,
            BorinMemoryCategory.Bug => 30,
            BorinMemoryCategory.WorkResult => 25,
            BorinMemoryCategory.ConversationSummary => 12,
            _ => 3,
        };

        var memoryTerms = Tokenize(entry.Text);
        score += requestTerms.Count == 0
            ? 0
            : requestTerms.Count(term => memoryTerms.Contains(term)) * 12;

        var ageDays = Math.Max(0, (now - entry.UpdatedAt).TotalDays);
        score += Math.Max(0, 12 - Math.Min(12, ageDays / 2));
        return score;
    }

    private static HashSet<string> Tokenize(string text, bool ignoreNegation = false)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        foreach (var character in text ?? string.Empty)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                AddToken(builder, terms, ignoreNegation);
            }
        }

        AddToken(builder, terms, ignoreNegation);
        return terms;
    }

    private static void AddToken(StringBuilder builder, HashSet<string> terms, bool ignoreNegation)
    {
        if (builder.Length > 2)
        {
            var token = builder.ToString();
            if (!StopWords.Contains(token) && (!ignoreNegation || !NegationWords.Contains(token)))
                terms.Add(token);
        }
        builder.Clear();
    }

    private static double Similarity(string left, string right)
    {
        var leftTerms = Tokenize(left);
        var rightTerms = Tokenize(right);
        if (leftTerms.Count == 0 || rightTerms.Count == 0)
            return 0;
        var intersection = leftTerms.Count(rightTerms.Contains);
        var union = leftTerms.Count + rightTerms.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double SubjectSimilarity(string left, string right)
    {
        var leftTerms = Tokenize(left, ignoreNegation: true);
        var rightTerms = Tokenize(right, ignoreNegation: true);
        if (leftTerms.Count == 0 || rightTerms.Count == 0)
            return 0;
        var intersection = leftTerms.Count(rightTerms.Contains);
        return (double)intersection / Math.Max(1, Math.Min(leftTerms.Count, rightTerms.Count));
    }

    private static bool LooksContradictory(string left, string right)
    {
        var leftHasNegation = ContainsNegation(left);
        var rightHasNegation = ContainsNegation(right);
        return leftHasNegation != rightHasNegation;
    }

    private static bool ContainsNegation(string value)
    {
        var normalized = $" {NormalizeText(value).ToLowerInvariant()} ";
        return normalized.Contains(" not ", StringComparison.Ordinal) ||
               normalized.Contains(" never ", StringComparison.Ordinal) ||
               normalized.Contains(" no ", StringComparison.Ordinal) ||
               normalized.Contains(" cannot ", StringComparison.Ordinal) ||
               normalized.Contains(" can't ", StringComparison.Ordinal) ||
               normalized.Contains(" cant ", StringComparison.Ordinal) ||
               normalized.Contains(" don't ", StringComparison.Ordinal) ||
               normalized.Contains(" dont ", StringComparison.Ordinal) ||
               normalized.Contains(" won't ", StringComparison.Ordinal) ||
               normalized.Contains(" wont ", StringComparison.Ordinal) ||
               normalized.Contains(" without ", StringComparison.Ordinal);
    }

    private static void Trim(BorinMemoryDocument document)
    {
        CompactConversationHistory(document);

        var summaries = document.Entries
            .Where(entry => entry.Category == BorinMemoryCategory.ConversationSummary && !entry.Archived)
            .OrderByDescending(entry => entry.UpdatedAt)
            .Skip(MaximumConversationSummaries)
            .ToArray();
        foreach (var summary in summaries)
            document.Entries.Remove(summary);

        while (document.Entries.Count > MaximumEntries)
        {
            var removable = document.Entries
                .Where(entry => !entry.Pinned && !entry.Archived)
                .OrderBy(entry => entry.UpdatedAt)
                .FirstOrDefault()
                ?? document.Entries
                    .Where(entry => entry.Archived)
                    .OrderBy(entry => entry.UpdatedAt)
                    .FirstOrDefault()
                ?? document.Entries.OrderBy(entry => entry.UpdatedAt).First();
            document.Entries.Remove(removable);
        }
    }

    private static void CompactConversationHistory(BorinMemoryDocument document)
    {
        while (true)
        {
            var raw = document.Entries
                .Where(entry => !entry.Archived && entry.Category == BorinMemoryCategory.Conversation)
                .OrderBy(entry => entry.CreatedAt)
                .ToArray();
            if (raw.Length <= MaximumRawConversationEntries)
                return;

            var batch = raw.Take(ConversationCompactionBatch).ToArray();
            var summaryText = "Conversation summary: " + string.Join(
                " | ",
                batch.Select(entry => SummarizeTurn(entry.Text)));
            document.Entries.Add(new BorinMemoryEntry
            {
                Category = BorinMemoryCategory.ConversationSummary,
                Evidence = BorinMemoryEvidence.Discussed,
                Scope = BorinMemoryScope.Permanent,
                Text = TrimTo(summaryText, 5000),
                Source = $"Compacted {batch.Length} conversation turns",
                Pinned = false,
                CreatedAt = batch.Min(entry => entry.CreatedAt),
                UpdatedAt = batch.Max(entry => entry.UpdatedAt),
            });

            foreach (var entry in batch)
                document.Entries.Remove(entry);
        }
    }

    private static string SummarizeTurn(string text)
    {
        var normalized = NormalizeText(text)
            .Replace("User:", "U:", StringComparison.OrdinalIgnoreCase)
            .Replace("Borin:", "B:", StringComparison.OrdinalIgnoreCase);
        return TrimTo(normalized, 420);
    }

    private static BorinMemoryEvidence Stronger(
        BorinMemoryEvidence left,
        BorinMemoryEvidence right) =>
        (BorinMemoryEvidence)Math.Max((int)left, (int)right);

    private static BorinMemoryScope ScopeFor(BorinMemoryCategory category) =>
        category == BorinMemoryCategory.Conversation
            ? BorinMemoryScope.Temporary
            : BorinMemoryScope.Permanent;

    private static void ArchiveUnsafe(BorinMemoryEntry entry, DateTimeOffset now)
    {
        entry.Archived = true;
        entry.ArchivedAt = now;
        entry.UpdatedAt = now;
        entry.Pinned = false;
    }

    private static BorinMemoryEntry Clone(BorinMemoryEntry source) => new()
    {
        Id = source.Id,
        Category = source.Category,
        Evidence = source.Evidence,
        Scope = source.Scope,
        Text = source.Text,
        Source = source.Source,
        Pinned = source.Pinned,
        Archived = source.Archived,
        SupersedesId = source.SupersedesId,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        ArchivedAt = source.ArchivedAt,
        LastRecalledAt = source.LastRecalledAt,
        RecallCount = source.RecallCount,
    };

    private static string NormalizeText(string text) =>
        string.Join(" ", (text ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string NormalizeForComparison(string text) =>
        NormalizeText(text).ToUpperInvariant();

    private static string TrimTo(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "…";

    private static readonly HashSet<string> NegationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "never", "none", "cannot", "cant", "dont", "wont", "without",
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "into", "for", "are", "was", "were",
        "will", "shall", "should", "would", "could", "have", "has", "had", "its", "our", "your",
        "about", "after", "before", "when", "where", "what", "which", "then", "than", "only",
    };

    private sealed class BorinMemoryDocument
    {
        public int Version { get; set; } = 2;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        public List<BorinMemoryEntry> Entries { get; set; } = [];
    }
}
