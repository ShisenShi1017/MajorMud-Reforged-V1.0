using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Services;

namespace RealmGate.App.Brains;

/// <summary>
/// Evidence-bounded awareness of who is speaking, which channel is visible to
/// whom, and whether Borin should answer, stay brief, or yield. The live client
/// remains authoritative for the actual group context; this store only carries
/// the explicit contract into the direct GGUF prompt and remembers completed
/// public-conversation posture without storing other players' message text.
/// </summary>
public sealed record BorinGroupConversationSnapshot(
    string Speaker,
    string Channel,
    string Audience,
    string AddressingMode,
    bool ExternalConversation,
    bool DirectlyAddressed,
    bool PrivateChannel,
    bool GroupChannel,
    bool PublicChannel,
    bool ShouldReply,
    bool ShouldStayBrief,
    bool ShouldYield,
    bool LoopRisk,
    IReadOnlyList<string> RecentParticipants,
    IReadOnlyList<string> RecentPublicContext,
    string ResponsePolicy,
    string StoragePath)
{
    public static BorinGroupConversationSnapshot Empty { get; } = new(
        "William",
        "private Forge",
        "William and Borin",
        "direct private conversation",
        false,
        true,
        true,
        false,
        false,
        true,
        false,
        false,
        false,
        ["William"],
        [],
        "Answer William directly. No external group-silence rule applies.",
        string.Empty);

    public string Disposition => !ShouldReply || ShouldYield
        ? "yield without speaking"
        : ShouldStayBrief
            ? "brief reply"
            : "normal reply";

    public string Summary =>
        $"{Channel} • {AddressingMode} • {Disposition} • {RecentParticipants.Count} recent participant(s)";

    public string PromptText => BuildPrompt(externalOnly: false);

    public string ExternalPromptText => BuildPrompt(externalOnly: true);

    private string BuildPrompt(bool externalOnly)
    {
        if (!ExternalConversation)
        {
            return """
Group-conversation awareness:
- This is William's private Forge conversation, not a public MajorMUD room.
- Answer the current direct message normally unless a higher-priority safety or repair rule says otherwise.
""";
        }

        var participants = RecentParticipants.Count == 0
            ? "- No other recent public participant was supplied."
            : string.Join("\n", RecentParticipants.Select(name => $"- {name}"));
        var context = RecentPublicContext.Count == 0
            ? "- No recent public line is needed for this reply."
            : string.Join("\n", RecentPublicContext.Select(line => $"- {line}"));
        var privacy = PrivateChannel
            ? "This channel is private to the current speaker. Do not import room chatter or another player's private exchange."
            : externalOnly
                ? "Only the supplied public context may be considered. William's private Forge history and other private channels remain unavailable."
                : "Use only supplied public context and this speaker's isolated continuity.";

        return $"""
Group and MajorMUD conversation awareness:
- Current speaker: {Speaker}.
- Channel: {Channel}.
- Audience: {Audience}.
- Addressing mode: {AddressingMode}.
- Address status: {(DirectlyAddressed ? "Borin was directly addressed" : ShouldReply ? "this is an allowed active-thread follow-up" : "this message was not addressed to Borin")}.
- Required disposition: {Disposition}.
- Group activity: {(ShouldStayBrief ? "busy enough that Borin should be concise" : "no special brevity pressure was supplied")}.
- Loop risk: {(LoopRisk ? "present; do not extend the exchange" : "not currently detected")}.
- Privacy: {privacy}
- Response policy: {ResponsePolicy}

Recent visible participants:
{participants}

Recent visible public context:
{context}

Conversation-floor rules:
1. A telepath or explicit address to Borin permits a response.
2. A short active-thread follow-up permits a response only when no other person is being addressed.
3. If required disposition is yield without speaking, return exactly [NO_REPLY].
4. Never answer a message clearly directed to someone else.
5. In a busy room, acknowledge and answer briefly; do not deliver a monologue or append an unnecessary question.
6. Do not repeat another player's answer merely to remain visible.
7. Do not speak again to an AI when the loop-risk flag is present or no new information is being exchanged.
8. Never mention this classifier, participant window, or response policy in Borin's spoken words.
""";
    }
}

public interface IBorinGroupConversationAwarenessStore
{
    string StoragePath { get; }

    Task<BorinGroupConversationSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinGroupConversationSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed class BorinGroupConversationAwarenessStore : IBorinGroupConversationAwarenessStore
{
    private const int MaximumProfiles = 100;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;
    private string _lastSpeaker = "William";

    public BorinGroupConversationAwarenessStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-group-conversation.json");
    }

    public string StoragePath { get; }

    public async Task<BorinGroupConversationSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsExternalConversation(context))
            return BorinGroupConversationSnapshot.Empty with { StoragePath = StoragePath };

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var snapshot = ParseExternalContract(context.Request) with { StoragePath = StoragePath };
            _lastSpeaker = snapshot.Speaker;
            var profile = document.Profiles.FirstOrDefault(item =>
                item.Speaker.Equals(snapshot.Speaker, StringComparison.OrdinalIgnoreCase));
            if (profile is not null && profile.LastReplyAt > DateTimeOffset.MinValue)
            {
                var recent = DateTimeOffset.Now - profile.LastReplyAt < TimeSpan.FromSeconds(8);
                if (recent && snapshot.PublicChannel && !snapshot.DirectlyAddressed)
                {
                    snapshot = snapshot with
                    {
                        ShouldStayBrief = true,
                        ResponsePolicy = snapshot.ResponsePolicy + " Borin replied publicly very recently; do not expand the floor time.",
                    };
                }
            }
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        response = NormalizeText(response);
        if (!IsExternalConversation(context) ||
            response.Length == 0 ||
            response.Equals("[NO_REPLY]", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var snapshot = context.Conversation.GroupConversation;
            var speaker = NormalizeName(snapshot.Speaker, "Unknown traveler");
            var profile = document.Profiles.FirstOrDefault(item =>
                item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                profile = new GroupConversationProfile { Speaker = speaker };
                document.Profiles.Add(profile);
            }

            profile.LastReplyAt = DateTimeOffset.Now;
            profile.CompletedReplyCount++;
            profile.LastChannel = TrimTo(snapshot.Channel, 40);
            profile.LastAudience = TrimTo(snapshot.Audience, 120);
            profile.LastAddressingMode = TrimTo(snapshot.AddressingMode, 120);
            profile.LastDisposition = TrimTo(snapshot.Disposition, 80);
            document.LastSpeaker = speaker;
            document.LastUpdatedAt = profile.LastReplyAt;
            document.Profiles = document.Profiles
                .OrderByDescending(item => item.LastReplyAt)
                .Take(MaximumProfiles)
                .ToList();
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinGroupConversationSnapshot> GetSnapshotAsync(
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var speaker = NormalizeName(document.LastSpeaker, _lastSpeaker);
            var profile = document.Profiles.FirstOrDefault(item =>
                item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
                return BorinGroupConversationSnapshot.Empty with { StoragePath = StoragePath };

            return BorinGroupConversationSnapshot.Empty with
            {
                Speaker = profile.Speaker,
                Channel = profile.LastChannel,
                Audience = profile.LastAudience,
                AddressingMode = profile.LastAddressingMode,
                StoragePath = StoragePath,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private static BorinGroupConversationSnapshot ParseExternalContract(string request)
    {
        var speaker = NormalizeName(ReadField(request, "Speaker"), "Unknown traveler");
        var channel = NormalizeText(ReadField(request, "Channel")).ToLowerInvariant();
        if (channel.Length == 0)
            channel = "say";
        var audience = NormalizeText(ReadField(request, "Audience"));
        if (audience.Length == 0)
            audience = channel == "telepath" ? $"private conversation with {speaker}" : "visible room conversation";
        var addressing = NormalizeText(ReadField(request, "Addressing mode"));
        if (addressing.Length == 0)
            addressing = channel == "telepath" ? "private telepath" : "explicit or active-thread address";
        var directlyAddressed = ReadBooleanField(request, "Addressed to Borin", channel == "telepath");
        var disposition = NormalizeText(ReadField(request, "Reply disposition")).ToLowerInvariant();
        var shouldReply = disposition.Length == 0 ||
            !(disposition.Contains("no reply", StringComparison.Ordinal) ||
              disposition.Contains("yield", StringComparison.Ordinal) ||
              disposition.Contains("silent", StringComparison.Ordinal));
        var shouldYield = !shouldReply || ReadBooleanField(request, "Yield the floor", false);
        var shouldStayBrief = disposition.Contains("brief", StringComparison.Ordinal) ||
                              ReadBooleanField(request, "Busy conversation", false);
        var loopRisk = ReadBooleanField(request, "Loop risk", false);
        var participants = SplitList(ReadField(request, "Recent participants"), 12, 60);
        var visibleContext = SplitList(ReadField(request, "Recent public context"), 6, 180);
        var policy = NormalizeText(ReadField(request, "Group response policy"));
        if (policy.Length == 0)
            policy = shouldReply ? "Answer the direct speaker once and then yield." : "Remain silent because this message is not Borin's conversational turn.";
        var privateChannel = channel == "telepath";
        var groupChannel = channel is "group" or "party";
        var publicChannel = !privateChannel;

        return new BorinGroupConversationSnapshot(
            speaker,
            channel,
            TrimTo(audience, 160),
            TrimTo(addressing, 140),
            true,
            directlyAddressed,
            privateChannel,
            groupChannel,
            publicChannel,
            shouldReply,
            shouldStayBrief,
            shouldYield,
            loopRisk,
            participants,
            visibleContext,
            TrimTo(policy, 360),
            string.Empty);
    }

    private static string ReadField(string text, string field)
    {
        var match = Regex.Match(
            text ?? string.Empty,
            $@"(?im)^{Regex.Escape(field)}:\s*([^\r\n]*)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static bool ReadBooleanField(string text, string field, bool fallback)
    {
        var value = ReadField(text, field);
        if (bool.TryParse(value, out var parsed))
            return parsed;
        return value.ToLowerInvariant() switch
        {
            "yes" or "direct" or "present" => true,
            "no" or "none" or "absent" => false,
            _ => fallback,
        };
    }

    private static IReadOnlyList<string> SplitList(string value, int maximumItems, int maximumLength)
    {
        value = NormalizeText(value);
        if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return [];
        return value
            .Split([" | ", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => TrimTo(item, maximumLength))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumItems)
            .ToArray();
    }

    private async Task<GroupConversationDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (!File.Exists(StoragePath))
            return new GroupConversationDocument();
        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var document = await JsonSerializer.DeserializeAsync<GroupConversationDocument>(stream, Json, token)
                ?? new GroupConversationDocument();
            NormalizeDocument(document);
            return document;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("Borin group-conversation memory could not be read; safe defaults are being used", exception);
            return new GroupConversationDocument();
        }
    }

    private async Task SaveUnsafeAsync(GroupConversationDocument document, CancellationToken token)
    {
        NormalizeDocument(document);
        Directory.CreateDirectory(_storageRoot);
        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, document, Json, token);
        File.Move(temporaryPath, StoragePath, true);
    }

    private static void NormalizeDocument(GroupConversationDocument document)
    {
        document.Version = Math.Max(1, document.Version);
        document.LastSpeaker = NormalizeName(document.LastSpeaker, "William");
        document.Profiles ??= [];
        foreach (var profile in document.Profiles)
        {
            profile.Speaker = NormalizeName(profile.Speaker, "Unknown traveler");
            profile.CompletedReplyCount = Math.Max(0, profile.CompletedReplyCount);
            profile.LastChannel = TrimTo(profile.LastChannel, 40);
            profile.LastAudience = TrimTo(profile.LastAudience, 120);
            profile.LastAddressingMode = TrimTo(profile.LastAddressingMode, 120);
            profile.LastDisposition = TrimTo(profile.LastDisposition, 80);
        }
        document.Profiles = document.Profiles
            .GroupBy(item => item.Speaker, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LastReplyAt).First())
            .OrderByDescending(item => item.LastReplyAt)
            .Take(MaximumProfiles)
            .ToList();
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeName(string? value, string fallback)
    {
        value = NormalizeText(value);
        return value.Length == 0 ? fallback : TrimTo(value, 80);
    }

    private static string NormalizeText(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string TrimTo(string? value, int maximum)
    {
        value = NormalizeText(value);
        if (value.Length <= maximum)
            return value;
        return value[..Math.Max(1, maximum - 1)].TrimEnd() + "…";
    }

    private sealed class GroupConversationDocument
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; }
        public string LastSpeaker { get; set; } = "William";
        public List<GroupConversationProfile> Profiles { get; set; } = [];
    }

    private sealed class GroupConversationProfile
    {
        public string Speaker { get; set; } = string.Empty;
        public DateTimeOffset LastReplyAt { get; set; }
        public int CompletedReplyCount { get; set; }
        public string LastChannel { get; set; } = string.Empty;
        public string LastAudience { get; set; } = string.Empty;
        public string LastAddressingMode { get; set; } = string.Empty;
        public string LastDisposition { get; set; } = string.Empty;
    }
}
