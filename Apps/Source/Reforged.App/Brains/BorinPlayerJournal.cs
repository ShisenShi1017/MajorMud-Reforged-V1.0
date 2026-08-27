using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Reforged.App.Brains;

public sealed record BorinPlayerJournalStatus(
    string StoragePath,
    int ReflectionCount,
    int ConversationCount,
    int SocialLessonCount,
    int SpeakerCount,
    string LastReflection,
    string LastConversationLesson,
    string LastSpeakerSummary,
    DateTimeOffset UpdatedAt)
{
    public static BorinPlayerJournalStatus Empty(string storagePath = "") =>
        new(storagePath, 0, 0, 0, 0, "No reflection written yet.", "No conversation lesson recorded yet.", "No recurring player profile yet.", DateTimeOffset.MinValue);

    public string Summary => StoragePath.Length == 0
        ? "Reflection journal is not configured."
        : $"{ReflectionCount} reflections • {ConversationCount} conversations • {SocialLessonCount} social lessons • {SpeakerCount} player profiles";
}

public sealed record BorinPlayerReflectionEntry(
    string Id,
    DateTimeOffset Timestamp,
    string Source,
    string Goal,
    string Room,
    string Text)
{
    public string Summary => Text.Length == 0 ? "No reflection was written." : Text;
}

public sealed record BorinConversationJournalEntry(
    string Id,
    DateTimeOffset Timestamp,
    string Speaker,
    string Channel,
    string MessageSummary,
    string BorinResponse,
    string Trust,
    bool AddressedToBorin,
    bool Suppressed,
    string SuppressionReason,
    string Rating,
    string ReviewNote,
    string VerifiedBy)
{
    public string Summary =>
        $"{Speaker} ({Channel}) • {Rating} • " +
        (ReviewNote.Length == 0 ? (Suppressed ? SuppressionReason : BorinResponse) : ReviewNote);
}

public sealed record BorinSocialLesson(
    string Id,
    DateTimeOffset Timestamp,
    string Scope,
    string Lesson,
    string Classification,
    string VerifiedBy)
{
    public string Summary => $"{Scope}: {Lesson} ({Classification}{(VerifiedBy.Length == 0 ? string.Empty : $", {VerifiedBy}")})";
}

/// <summary>
/// Private, local reflection/conversation ledger for Borin's own external-player session.
/// It stores concise summaries rather than hidden chain-of-thought or hidden model reasoning and is never sent to the game server.
/// </summary>
internal sealed class BorinPlayerJournalStore : IAsyncDisposable
{
    private const int MaximumConversations = 500;
    private const int MaximumReflections = 250;
    private const int MaximumSocialLessons = 150;
    private const int MaximumSpeakerProfiles = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _repositoryRoot = string.Empty;
    private string _sessionId = string.Empty;
    private string _path = string.Empty;
    private JournalLedger _ledger = new();
    private bool _disposed;

    public event EventHandler<BorinPlayerJournalStatus>? Changed;

    public BorinPlayerJournalStatus CurrentStatus => CreateStatus();

    public async Task ConfigureAsync(string repositoryRoot, string sessionId, CancellationToken token)
    {
        ThrowIfDisposed();
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _sessionId = NormalizeIdentifier(sessionId);
        _path = Path.Combine(_repositoryRoot, "AI", "Memory", "Gameplay", _sessionId, "borin-reflection-journal.json");

        await _gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path))
            {
                try
                {
                    await using var stream = File.OpenRead(_path);
                    _ledger = await JsonSerializer.DeserializeAsync<JournalLedger>(stream, JsonOptions, token) ?? new JournalLedger();
                }
                catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
                {
                    _ledger = new JournalLedger();
                }
            }
            else
            {
                _ledger = new JournalLedger();
                await SaveLockedAsync(token);
            }

            NormalizeLedger();
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
    }

    public async Task<BorinConversationJournalEntry> RecordConversationAsync(
        string speaker,
        string channel,
        string message,
        string response,
        string trust,
        bool addressedToBorin,
        bool suppressed,
        string suppressionReason,
        CancellationToken token)
    {
        ThrowIfDisposed();
        var now = DateTimeOffset.Now;
        var entry = new ConversationEntry
        {
            Id = $"conversation-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..49],
            Timestamp = now,
            Speaker = NormalizeText(speaker, "Unknown", 80),
            Channel = NormalizeText(channel, "unknown", 24).ToLowerInvariant(),
            MessageSummary = SummarizeMessage(message, channel),
            BorinResponse = NormalizeText(response, string.Empty, 600),
            Trust = NormalizeText(trust, "Conversation-only player", 100),
            AddressedToBorin = addressedToBorin,
            Suppressed = suppressed,
            SuppressionReason = NormalizeText(suppressionReason, string.Empty, 300),
            Rating = "Unreviewed",
            ReviewNote = string.Empty,
            VerifiedBy = string.Empty,
        };

        await _gate.WaitAsync(token);
        try
        {
            _ledger.Conversations.Add(entry);
            while (_ledger.Conversations.Count > MaximumConversations)
                _ledger.Conversations.RemoveAt(0);
            UpdateSpeakerProfileLocked(entry);
            TrimSpeakerProfilesLocked();
            _ledger.UpdatedAt = now;
            await SaveLockedAsync(token);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
        return entry.ToPublic();
    }

    public async Task<BorinPlayerReflectionEntry> AddReflectionAsync(
        string source,
        string goal,
        string room,
        string text,
        CancellationToken token)
    {
        ThrowIfDisposed();
        var now = DateTimeOffset.Now;
        var entry = new ReflectionEntry
        {
            Id = $"reflection-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..47],
            Timestamp = now,
            Source = NormalizeText(source, "manual", 40),
            Goal = NormalizeText(goal, string.Empty, 500),
            Room = NormalizeText(room, "Unknown room", 200),
            Text = NormalizeReflection(text),
        };

        await _gate.WaitAsync(token);
        try
        {
            _ledger.Reflections.Add(entry);
            while (_ledger.Reflections.Count > MaximumReflections)
                _ledger.Reflections.RemoveAt(0);
            _ledger.UpdatedAt = now;
            await SaveLockedAsync(token);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
        return entry.ToPublic();
    }

    public async Task<BorinConversationJournalEntry?> ReviewLastConversationAsync(
        string rating,
        string note,
        string verifier,
        CancellationToken token)
    {
        ThrowIfDisposed();
        var normalizedRating = NormalizeRating(rating);
        await _gate.WaitAsync(token);
        try
        {
            var entry = _ledger.Conversations.LastOrDefault(item => item.AddressedToBorin);
            if (entry is null)
                return null;

            entry.Rating = normalizedRating;
            entry.ReviewNote = NormalizeText(note, DefaultReviewNote(normalizedRating), 500);
            entry.VerifiedBy = NormalizeText(verifier, "William", 80);
            var lesson = BuildLesson(entry);
            if (lesson is not null)
            {
                var duplicate = _ledger.SocialLessons.Any(item =>
                    item.Scope.Equals(lesson.Scope, StringComparison.OrdinalIgnoreCase) &&
                    item.Lesson.Equals(lesson.Lesson, StringComparison.OrdinalIgnoreCase));
                if (!duplicate)
                {
                    _ledger.SocialLessons.Add(lesson);
                    while (_ledger.SocialLessons.Count > MaximumSocialLessons)
                        _ledger.SocialLessons.RemoveAt(0);
                }
            }

            RebuildSpeakerProfileLocked(entry.Speaker);
            _ledger.UpdatedAt = DateTimeOffset.Now;
            await SaveLockedAsync(token);
            var result = entry.ToPublic();
            RaiseChanged();
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public string BuildConversationGuidance(string speaker)
    {
        ThrowIfDisposed();
        var normalizedSpeaker = NormalizeText(speaker, "Unknown", 80);
        var profile = _ledger.Speakers.FirstOrDefault(item => item.Name.Equals(normalizedSpeaker, StringComparison.OrdinalIgnoreCase));
        var lessons = _ledger.SocialLessons
            .Where(item => item.Scope.Equals("General", StringComparison.OrdinalIgnoreCase) || item.Scope.Equals(normalizedSpeaker, StringComparison.OrdinalIgnoreCase))
            .TakeLast(6)
            .Select(item => item.Lesson)
            .ToArray();
        var recent = _ledger.Conversations
            .Where(item => item.Speaker.Equals(normalizedSpeaker, StringComparison.OrdinalIgnoreCase))
            .TakeLast(3)
            .Select(item => $"{item.Channel}: {item.MessageSummary} -> {item.BorinResponse}")
            .ToArray();

        var builder = new StringBuilder();
        if (profile is not null)
        {
            builder.Append($"Speaker profile: {profile.Name}; {profile.InteractionCount} prior exchanges; ");
            builder.Append($"helpful {profile.HelpfulCount}, awkward {profile.AwkwardCount}, incorrect {profile.IncorrectCount}. ");
        }
        if (lessons.Length > 0)
            builder.Append("Verified social lessons: ").Append(string.Join(" | ", lessons)).Append(". ");
        if (recent.Length > 0)
            builder.Append("Recent summarized exchanges: ").Append(string.Join(" || ", recent)).Append('.');
        return builder.Length == 0
            ? "No verified social preference is known for this speaker. Keep the reply concise, natural, and focused on one topic."
            : builder.ToString();
    }

    public string BuildReflectionContext(int maximumConversations = 6)
    {
        ThrowIfDisposed();
        var conversations = _ledger.Conversations.TakeLast(Math.Clamp(maximumConversations, 1, 12)).Select(item =>
            $"- {item.Speaker} ({item.Channel}): {item.MessageSummary} -> Borin: {item.BorinResponse} [{item.Rating}]");
        var lessons = _ledger.SocialLessons.TakeLast(8).Select(item => $"- {item.Scope}: {item.Lesson}");
        var reflections = _ledger.Reflections.TakeLast(6).Select(item =>
            $"- {item.Timestamp:yyyy-MM-dd HH:mm}: {item.Text}");
        return $"Recent prior reflections (self-authored strategy notes, never gameplay proof):\n{JoinOrNone(reflections)}\nRecent conversations:\n{JoinOrNone(conversations)}\nVerified social lessons:\n{JoinOrNone(lessons)}";
    }

    public string BuildGameplayReflectionGuidance(int maximumReflections = 6)
    {
        ThrowIfDisposed();
        var reflections = _ledger.Reflections
            .TakeLast(Math.Clamp(maximumReflections, 1, 12))
            .Select(item => $"- {item.Timestamp:yyyy-MM-dd HH:mm} • goal {item.Goal} • room {item.Room}: {item.Text}");
        return $"Earlier Borin reflections are strategy hypotheses and continuity notes, not evidence. Live server state and verified gameplay observations override them.\n{JoinOrNone(reflections)}";
    }

    public string BuildVisibleSummary()
    {
        var status = CreateStatus();
        return $"{status.Summary}\nLast reflection: {status.LastReflection}\nLast social lesson: {status.LastConversationLesson}\nPlayer profile: {status.LastSpeakerSummary}\nStored privately at: {status.StoragePath}";
    }

    private void UpdateSpeakerProfileLocked(ConversationEntry entry)
    {
        var profile = _ledger.Speakers.FirstOrDefault(item => item.Name.Equals(entry.Speaker, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            profile = new SpeakerProfile { Name = entry.Speaker };
            _ledger.Speakers.Add(profile);
        }
        profile.Trust = entry.Trust;
        profile.InteractionCount++;
        profile.LastSeen = entry.Timestamp;
        profile.LastTopic = entry.MessageSummary;
    }

    private void RebuildSpeakerProfileLocked(string speaker)
    {
        var profile = _ledger.Speakers.FirstOrDefault(item => item.Name.Equals(speaker, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
            return;
        var entries = _ledger.Conversations.Where(item => item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase)).ToArray();
        profile.InteractionCount = entries.Length;
        profile.HelpfulCount = entries.Count(item => item.Rating.Equals("Helpful", StringComparison.OrdinalIgnoreCase));
        profile.AwkwardCount = entries.Count(item => item.Rating.Equals("Awkward", StringComparison.OrdinalIgnoreCase));
        profile.IncorrectCount = entries.Count(item => item.Rating.Equals("Incorrect", StringComparison.OrdinalIgnoreCase));
        var last = entries.LastOrDefault();
        if (last is not null)
        {
            profile.LastSeen = last.Timestamp;
            profile.LastTopic = last.MessageSummary;
            profile.Trust = last.Trust;
        }
    }

    private static SocialLessonEntry? BuildLesson(ConversationEntry entry)
    {
        if (entry.Rating.Equals("Unreviewed", StringComparison.OrdinalIgnoreCase))
            return null;
        var prefix = entry.Rating switch
        {
            "Helpful" => "Continue",
            "Awkward" => "Improve",
            "Incorrect" => "Avoid",
            _ => "Remember",
        };
        return new SocialLessonEntry
        {
            Id = $"social-{DateTimeOffset.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..43],
            Timestamp = DateTimeOffset.Now,
            Scope = entry.Speaker,
            Lesson = $"{prefix}: {entry.ReviewNote}",
            Classification = "Verified social preference",
            VerifiedBy = entry.VerifiedBy,
        };
    }

    private BorinPlayerJournalStatus CreateStatus()
    {
        var reflection = _ledger.Reflections.LastOrDefault()?.Text ?? "No reflection written yet.";
        var lesson = _ledger.SocialLessons.LastOrDefault()?.ToPublic().Summary ?? "No conversation lesson recorded yet.";
        var speaker = _ledger.Speakers.OrderByDescending(item => item.LastSeen).FirstOrDefault();
        var speakerSummary = speaker is null
            ? "No recurring player profile yet."
            : $"{speaker.Name}: {speaker.InteractionCount} exchanges; helpful {speaker.HelpfulCount}, awkward {speaker.AwkwardCount}, incorrect {speaker.IncorrectCount}; last topic: {speaker.LastTopic}";
        return new BorinPlayerJournalStatus(
            _path.Length == 0 || _repositoryRoot.Length == 0 ? string.Empty : Path.GetRelativePath(_repositoryRoot, _path).Replace(Path.DirectorySeparatorChar, '\\'),
            _ledger.Reflections.Count,
            _ledger.Conversations.Count,
            _ledger.SocialLessons.Count,
            _ledger.Speakers.Count,
            reflection.Length > 700 ? reflection[..700] + "…" : reflection,
            lesson.Length > 500 ? lesson[..500] + "…" : lesson,
            speakerSummary.Length > 500 ? speakerSummary[..500] + "…" : speakerSummary,
            _ledger.UpdatedAt);
    }

    private async Task SaveLockedAsync(CancellationToken token)
    {
        if (_path.Length == 0)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, _ledger, JsonOptions, token);
        File.Move(temp, _path, overwrite: true);
    }

    private void NormalizeLedger()
    {
        _ledger.Reflections ??= [];
        _ledger.Conversations ??= [];
        _ledger.SocialLessons ??= [];
        _ledger.Speakers ??= [];

        if (_ledger.Reflections.Count > MaximumReflections)
            _ledger.Reflections = _ledger.Reflections.TakeLast(MaximumReflections).ToList();
        if (_ledger.Conversations.Count > MaximumConversations)
            _ledger.Conversations = _ledger.Conversations.TakeLast(MaximumConversations).ToList();
        if (_ledger.SocialLessons.Count > MaximumSocialLessons)
            _ledger.SocialLessons = _ledger.SocialLessons.TakeLast(MaximumSocialLessons).ToList();

        foreach (var speaker in _ledger.Speakers)
            RebuildSpeakerProfileLocked(speaker.Name);
        TrimSpeakerProfilesLocked();
    }

    private void TrimSpeakerProfilesLocked()
    {
        if (_ledger.Speakers.Count <= MaximumSpeakerProfiles)
            return;

        _ledger.Speakers = _ledger.Speakers
            .OrderByDescending(item => item.LastSeen)
            .Take(MaximumSpeakerProfiles)
            .ToList();
    }

    private void RaiseChanged() => Changed?.Invoke(this, CreateStatus());

    private static string SummarizeMessage(string message, string channel)
    {
        var cleaned = Redact(message);
        var maximum = channel.Equals("telepath", StringComparison.OrdinalIgnoreCase) ? 180 : 260;
        if (cleaned.Length <= maximum)
            return cleaned;
        var cut = cleaned.LastIndexOf(' ', maximum - 1, maximum);
        if (cut < maximum / 2)
            cut = maximum;
        return cleaned[..cut].TrimEnd() + "…";
    }

    private static string NormalizeReflection(string text)
    {
        var cleaned = Redact(text).Trim();
        if (cleaned.Length > 2400)
            cleaned = cleaned[..2400] + "…";
        return cleaned;
    }

    private static string NormalizeRating(string rating) => rating.Trim().ToLowerInvariant() switch
    {
        "helpful" or "good" => "Helpful",
        "awkward" => "Awkward",
        "incorrect" or "wrong" => "Incorrect",
        _ => throw new InvalidOperationException("Conversation rating must be Helpful, Awkward, or Incorrect."),
    };

    private static string DefaultReviewNote(string rating) => rating switch
    {
        "Helpful" => "The reply was useful and natural; use this approach in similar conversations.",
        "Awkward" => "The reply should be shorter, clearer, and better matched to the speaker's question.",
        "Incorrect" => "The reply was inaccurate or inappropriate and must not be repeated.",
        _ => "Review the exchange before reusing this approach.",
    };

    private static string NormalizeIdentifier(string value)
    {
        var normalized = Regex.Replace(value?.Trim() ?? string.Empty, "[^A-Za-z0-9._-]", "-");
        return normalized.Length == 0 ? "borin-training-01" : normalized[..Math.Min(64, normalized.Length)];
    }

    private static string NormalizeText(string? value, string fallback, int maximum)
    {
        var cleaned = Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", " ");
        if (cleaned.Length == 0)
            cleaned = fallback;
        return cleaned[..Math.Min(maximum, cleaned.Length)];
    }

    private static string Redact(string? value)
    {
        var text = value ?? string.Empty;
        text = Regex.Replace(text, @"(?i)(password|passphrase|account\s*key|login\s*code)\s*[:=]\s*\S+", "$1=[REDACTED]");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var materialized = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return materialized.Length == 0 ? "- None recorded." : string.Join("\n", materialized);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BorinPlayerJournalStore));
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _disposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class JournalLedger
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        public List<ReflectionEntry> Reflections { get; set; } = [];
        public List<ConversationEntry> Conversations { get; set; } = [];
        public List<SocialLessonEntry> SocialLessons { get; set; } = [];
        public List<SpeakerProfile> Speakers { get; set; } = [];
    }

    private sealed class ReflectionEntry
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public BorinPlayerReflectionEntry ToPublic() => new(Id, Timestamp, Source, Goal, Room, Text);
    }

    private sealed class ConversationEntry
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string Speaker { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string MessageSummary { get; set; } = string.Empty;
        public string BorinResponse { get; set; } = string.Empty;
        public string Trust { get; set; } = string.Empty;
        public bool AddressedToBorin { get; set; }
        public bool Suppressed { get; set; }
        public string SuppressionReason { get; set; } = string.Empty;
        public string Rating { get; set; } = "Unreviewed";
        public string ReviewNote { get; set; } = string.Empty;
        public string VerifiedBy { get; set; } = string.Empty;
        public BorinConversationJournalEntry ToPublic() => new(
            Id, Timestamp, Speaker, Channel, MessageSummary, BorinResponse, Trust, AddressedToBorin, Suppressed,
            SuppressionReason, Rating, ReviewNote, VerifiedBy);
    }

    private sealed class SocialLessonEntry
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string Scope { get; set; } = "General";
        public string Lesson { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string VerifiedBy { get; set; } = string.Empty;
        public BorinSocialLesson ToPublic() => new(Id, Timestamp, Scope, Lesson, Classification, VerifiedBy);
    }

    private sealed class SpeakerProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Trust { get; set; } = string.Empty;
        public int InteractionCount { get; set; }
        public int HelpfulCount { get; set; }
        public int AwkwardCount { get; set; }
        public int IncorrectCount { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public string LastTopic { get; set; } = string.Empty;
    }
}
