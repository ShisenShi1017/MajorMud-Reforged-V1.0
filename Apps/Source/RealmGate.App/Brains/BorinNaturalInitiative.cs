using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Services;

namespace RealmGate.App.Brains;

/// <summary>
/// Persistent, surface-aware gate for Borin speaking without a fresh incoming
/// message. This service decides whether silence should be interrupted; it never
/// grants gameplay, file, tool, login, or deployment authority.
/// </summary>
public sealed record BorinNaturalInitiativeRequest(
    string Surface,
    string Speaker,
    DateTimeOffset EvaluatedAt,
    bool Enabled,
    bool HasPriorConversation,
    bool IsBusy,
    bool UserIsTyping,
    bool IsConnected,
    bool IsLoggedIntoMajorMud,
    bool InCombat,
    bool IsMoving,
    int HealthPercent,
    int DangerHealthPercent,
    int QuietMinutes,
    int CooldownMinutes,
    int MaximumInitiativesPerHour,
    bool PublicConversationBusy,
    bool PendingDirectQuestion,
    string CurrentGoal,
    string CurrentRoom,
    string RecentPrivateContext,
    string VerifiedStateSummary);

public sealed record BorinNaturalInitiativeDecision(
    bool ShouldSpeak,
    string Surface,
    string Speaker,
    string Trigger,
    string Reason,
    string PromptText,
    int PreferredMaximumCharacters,
    DateTimeOffset EvaluatedAt)
{
    public static BorinNaturalInitiativeDecision StaySilent(
        BorinNaturalInitiativeRequest request,
        string reason) =>
        new(
            false,
            request.Surface,
            request.Speaker,
            "none",
            reason,
            string.Empty,
            request.Surface.Equals(BorinNaturalInitiativeSurfaces.MajorMudPrivate, StringComparison.OrdinalIgnoreCase)
                ? 220
                : 420,
            request.EvaluatedAt);
}

public static class BorinNaturalInitiativeSurfaces
{
    public const string Forge = "forge";
    public const string MajorMudPrivate = "majormud-private";
}

public interface IBorinNaturalInitiativeStore
{
    string StoragePath { get; }

    Task RecordHumanActivityAsync(
        string surface,
        string speaker,
        DateTimeOffset timestamp,
        CancellationToken token = default);

    Task RecordMeaningfulEventAsync(
        string surface,
        string speaker,
        string classification,
        string summary,
        DateTimeOffset timestamp,
        CancellationToken token = default);

    Task<BorinNaturalInitiativeDecision> EvaluateAsync(
        BorinNaturalInitiativeRequest request,
        CancellationToken token = default);

    Task RecordOutcomeAsync(
        BorinNaturalInitiativeDecision decision,
        string response,
        bool sent,
        CancellationToken token = default);
}

public sealed class BorinNaturalInitiativeStore : IBorinNaturalInitiativeStore
{
    private const int MaximumProfiles = 24;
    private const int MaximumInitiativeTimes = 12;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;

    public BorinNaturalInitiativeStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-natural-initiative.json");
    }

    public string StoragePath { get; }

    public async Task RecordHumanActivityAsync(
        string surface,
        string speaker,
        DateTimeOffset timestamp,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = GetOrCreateProfile(document, surface, speaker, timestamp);
            if (timestamp > profile.LastHumanActivityAt)
            {
                profile.LastHumanActivityAt = timestamp;
                profile.InteractionCount++;
            }
            if (timestamp >= profile.LastInitiativeAt)
                profile.AwaitingResponse = false;
            profile.LastUpdatedAt = timestamp;
            document.LastUpdatedAt = timestamp;
            PruneProfiles(document);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordMeaningfulEventAsync(
        string surface,
        string speaker,
        string classification,
        string summary,
        DateTimeOffset timestamp,
        CancellationToken token = default)
    {
        classification = NormalizeText(classification, 80);
        summary = NormalizeText(summary, 260);
        if (classification.Length == 0 || summary.Length == 0)
            return;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = GetOrCreateProfile(document, surface, speaker, timestamp);
            var fingerprint = BuildFingerprint(classification, summary);
            if (!fingerprint.Equals(profile.LastEventFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                profile.LastEventClassification = classification;
                profile.LastEventSummary = summary;
                profile.LastEventFingerprint = fingerprint;
                profile.LastMeaningfulEventAt = timestamp;
            }
            profile.LastUpdatedAt = timestamp;
            document.LastUpdatedAt = timestamp;
            PruneProfiles(document);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinNaturalInitiativeDecision> EvaluateAsync(
        BorinNaturalInitiativeRequest request,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = request.EvaluatedAt;
            var profile = GetOrCreateProfile(document, request.Surface, request.Speaker, now);
            PruneInitiativeTimes(profile, now);

            var silentReason = FindSilenceReason(request, profile, now);
            if (silentReason.Length > 0)
            {
                profile.LastEvaluationAt = now;
                profile.LastEvaluationReason = silentReason;
                profile.LastUpdatedAt = now;
                document.LastUpdatedAt = now;
                await SaveUnsafeAsync(document, token);
                return BorinNaturalInitiativeDecision.StaySilent(request, silentReason);
            }

            var eventFresh = profile.LastMeaningfulEventAt != DateTimeOffset.MinValue &&
                             now - profile.LastMeaningfulEventAt <= TimeSpan.FromMinutes(20) &&
                             !profile.LastEventFingerprint.Equals(profile.LastInitiativeEventFingerprint, StringComparison.OrdinalIgnoreCase);
            var trigger = eventFresh
                ? $"verified {profile.LastEventClassification} event"
                : "quiet companionship";
            var eventText = eventFresh
                ? $"Most recent verified event: {profile.LastEventClassification} — {profile.LastEventSummary}."
                : "No fresh verified event requires comment. Silence is acceptable.";
            var preferredMaximum = request.Surface.Equals(
                    BorinNaturalInitiativeSurfaces.MajorMudPrivate,
                    StringComparison.OrdinalIgnoreCase)
                ? 220
                : 420;

            var prompt = $"""
BORIN NATURAL INITIATIVE CONTRACT
This is a proactive conversational turn, not a reply to a new message. Decide whether one brief, genuinely useful thought is worth interrupting the quiet.
Return exactly [NO_REPLY] when silence is more natural. Never speak merely to prove Borin is active.
If speaking, return only Borin's words with no labels, commands, JSON, stage directions, asterisk actions, or explanation of this contract.
Address {request.Speaker} naturally. Use one clear topic. Do not repeat the last exchange, ask for attention, advertise capabilities, or end with a generic offer to help.
A purposeful question is allowed only when it follows directly from the unfinished private context; never ask a question merely to force a response.
Do not claim Borin acted, traveled, fought, drank, slept, worked, observed, or completed anything off-screen.
Do not invent room, combat, inventory, equipment, connection, login, repository, build, or deployment state.
This turn grants no permission to send gameplay commands, edit files, run tools, log in, build, publish, deploy, or change settings.

Surface: {request.Surface}
Recipient: {request.Speaker}
Trigger: {trigger}
Current goal: {NormalizeText(request.CurrentGoal, 300)}
Current room: {NormalizeText(request.CurrentRoom, 160)}
Verified state: {NormalizeText(request.VerifiedStateSummary, 420)}
{eventText}
Recent private context: {NormalizeText(request.RecentPrivateContext, 700)}
Target length: at most {preferredMaximum} characters.
""";

            profile.LastEvaluationAt = now;
            profile.LastEvaluationReason = $"Eligible for {trigger}.";
            profile.LastUpdatedAt = now;
            document.LastUpdatedAt = now;
            await SaveUnsafeAsync(document, token);

            return new BorinNaturalInitiativeDecision(
                true,
                request.Surface,
                request.Speaker,
                trigger,
                profile.LastEvaluationReason,
                prompt,
                preferredMaximum,
                now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordOutcomeAsync(
        BorinNaturalInitiativeDecision decision,
        string response,
        bool sent,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        response = NormalizeText(response, 700);
        var now = DateTimeOffset.Now;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var profile = GetOrCreateProfile(document, decision.Surface, decision.Speaker, now);
            profile.LastAttemptAt = now;
            profile.LastOutcome = sent ? "sent" : response.Equals("[NO_REPLY]", StringComparison.OrdinalIgnoreCase) || response.Length == 0
                ? "silence chosen"
                : "discarded safely";
            if (sent)
            {
                profile.LastInitiativeAt = now;
                profile.LastInitiativeText = response;
                profile.AwaitingResponse = true;
                profile.InitiativeTimes.Add(now);
                while (profile.InitiativeTimes.Count > MaximumInitiativeTimes)
                    profile.InitiativeTimes.RemoveAt(0);
                profile.LastInitiativeEventFingerprint = profile.LastEventFingerprint;
            }
            profile.LastUpdatedAt = now;
            document.LastUpdatedAt = now;
            PruneInitiativeTimes(profile, now);
            PruneProfiles(document);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string FindSilenceReason(
        BorinNaturalInitiativeRequest request,
        NaturalInitiativeProfile profile,
        DateTimeOffset now)
    {
        if (!request.Enabled)
            return "Natural initiative is disabled.";
        if (!request.HasPriorConversation && profile.InteractionCount == 0 && profile.LastHumanActivityAt == DateTimeOffset.MinValue)
            return "Borin has no established conversation with this person yet.";
        if (request.IsBusy)
            return "Borin or the conversation surface is already busy.";
        if (request.UserIsTyping)
            return "The user is composing a message, so Borin yields.";
        if (request.PublicConversationBusy)
            return "Visible conversation is active, so Borin does not interrupt it.";
        if (request.PendingDirectQuestion)
            return "Borin already asked a direct question and should wait for the answer.";
        if (request.Surface.Equals(BorinNaturalInitiativeSurfaces.MajorMudPrivate, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.IsConnected || !request.IsLoggedIntoMajorMud)
                return "The verified external MajorMUD session is not ready.";
            if (request.InCombat)
                return "Combat is active, so unsolicited conversation is suppressed.";
            if (request.IsMoving)
                return "Movement is active, so Borin waits until the situation is stable.";
            if (request.HealthPercent <= request.DangerHealthPercent)
                return "Health is in the danger range; safety outranks conversation.";
        }
        if (profile.AwaitingResponse && profile.LastHumanActivityAt <= profile.LastInitiativeAt)
            return "Borin already spoke first and is waiting rather than sending another unsolicited message.";
        if (profile.LastAttemptAt != DateTimeOffset.MinValue && now - profile.LastAttemptAt < TimeSpan.FromMinutes(3))
            return "A recent initiative check already chose speech or silence.";

        var humanActivity = profile.LastHumanActivityAt;
        if (humanActivity == DateTimeOffset.MinValue)
            return "No completed human activity timestamp is available.";
        var quietFor = now - humanActivity;
        if (quietFor < TimeSpan.FromMinutes(Math.Clamp(request.QuietMinutes, 2, 120)))
            return "The conversation has not been quiet long enough.";
        if (profile.LastInitiativeAt != DateTimeOffset.MinValue &&
            now - profile.LastInitiativeAt < TimeSpan.FromMinutes(Math.Clamp(request.CooldownMinutes, 5, 240)))
            return "The proactive-speech cooldown is still active.";
        if (profile.InitiativeTimes.Count >= Math.Clamp(request.MaximumInitiativesPerHour, 1, 4))
            return "The hourly proactive-speech limit has been reached.";
        return string.Empty;
    }

    private NaturalInitiativeProfile GetOrCreateProfile(
        NaturalInitiativeDocument document,
        string surface,
        string speaker,
        DateTimeOffset now)
    {
        surface = NormalizeIdentifier(surface, BorinNaturalInitiativeSurfaces.Forge);
        speaker = NormalizeName(speaker, "William");
        var profile = document.Profiles.FirstOrDefault(item =>
            item.Surface.Equals(surface, StringComparison.OrdinalIgnoreCase) &&
            item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
            return profile;

        profile = new NaturalInitiativeProfile
        {
            Surface = surface,
            Speaker = speaker,
            CreatedAt = now,
            LastUpdatedAt = now,
        };
        document.Profiles.Add(profile);
        return profile;
    }

    private async Task<NaturalInitiativeDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (!File.Exists(StoragePath))
            return new NaturalInitiativeDocument();

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<NaturalInitiativeDocument>(stream, Json, token)
                   ?? new NaturalInitiativeDocument();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("Borin natural-initiative state could not be read; safe silence defaults are being used", exception);
            return new NaturalInitiativeDocument();
        }
    }

    private async Task SaveUnsafeAsync(NaturalInitiativeDocument document, CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(_storageRoot);
            var temporary = StoragePath + ".tmp";
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, document, Json, token);
            File.Move(temporary, StoragePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Borin natural-initiative state could not be saved; the current turn remains safely bounded", exception);
        }
    }

    private static void PruneProfiles(NaturalInitiativeDocument document)
    {
        if (document.Profiles.Count <= MaximumProfiles)
            return;
        document.Profiles = document.Profiles
            .OrderByDescending(item => item.LastUpdatedAt)
            .Take(MaximumProfiles)
            .ToList();
    }

    private static void PruneInitiativeTimes(NaturalInitiativeProfile profile, DateTimeOffset now) =>
        profile.InitiativeTimes.RemoveAll(item => now - item > TimeSpan.FromHours(1));

    private static string BuildFingerprint(string classification, string summary)
    {
        var normalized = Regex.Replace($"{classification} {summary}".ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private static string NormalizeIdentifier(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(candidate, "[^a-z0-9._-]", "-");
        return normalized.Length == 0 ? fallback : normalized[..Math.Min(60, normalized.Length)];
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var candidate = NormalizeText(value, 80);
        return candidate.Length == 0 ? fallback : candidate;
    }

    private static string NormalizeText(string? value, int maximumLength)
    {
        var candidate = Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", " ");
        return candidate[..Math.Min(Math.Max(0, maximumLength), candidate.Length)];
    }

    private sealed class NaturalInitiativeDocument
    {
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.MinValue;
        public List<NaturalInitiativeProfile> Profiles { get; set; } = [];
    }

    private sealed class NaturalInitiativeProfile
    {
        public string Surface { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastHumanActivityAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastMeaningfulEventAt { get; set; } = DateTimeOffset.MinValue;
        public string LastEventClassification { get; set; } = string.Empty;
        public string LastEventSummary { get; set; } = string.Empty;
        public string LastEventFingerprint { get; set; } = string.Empty;
        public string LastInitiativeEventFingerprint { get; set; } = string.Empty;
        public DateTimeOffset LastEvaluationAt { get; set; } = DateTimeOffset.MinValue;
        public string LastEvaluationReason { get; set; } = string.Empty;
        public DateTimeOffset LastAttemptAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastInitiativeAt { get; set; } = DateTimeOffset.MinValue;
        public string LastInitiativeText { get; set; } = string.Empty;
        public string LastOutcome { get; set; } = string.Empty;
        public bool AwaitingResponse { get; set; }
        public int InteractionCount { get; set; }
        public List<DateTimeOffset> InitiativeTimes { get; set; } = [];
    }
}
