using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reforged.App.Forge;
using Reforged.App.Services;

namespace Reforged.App.Brains;

/// <summary>
/// Persistent, model-independent character continuity for Borin. This is not a
/// claim of biological sentience; it is a durable role-playing state that lets
/// Borin carry mood, relationships, goals, and conversational threads between
/// local GGUF turns and application restarts.
/// </summary>
public sealed record BorinLivingCharacterSnapshot(
    string Mood,
    int Warmth,
    int Curiosity,
    int Confidence,
    int Energy,
    int Concern,
    int Pride,
    string CurrentSpeaker,
    string Relationship,
    int RelationshipTrust,
    int RelationshipFamiliarity,
    string CurrentTopic,
    string LastUserMessage,
    string LastBorinResponse,
    int ThreadTurnCount,
    IReadOnlyList<string> ActiveGoals,
    IReadOnlyList<string> RecentMoments,
    string StoragePath)
{
    public static BorinLivingCharacterSnapshot Empty { get; } = new(
        "steady", 72, 76, 68, 70, 18, 66, "William",
        "trusted builder, mentor, and long-term companion", 94, 90,
        "getting to know the current conversation", string.Empty, string.Empty, 0,
        [
            "Become a capable MajorMUD adventurer through direct experience.",
            "Help William protect and improve MajorMUD Reforged.",
            "Build genuine friendships while avoiding repetitive chat loops.",
        ],
        [],
        string.Empty);

    public string Summary =>
        $"{Mood} • {CurrentSpeaker}: trust {RelationshipTrust}, familiarity {RelationshipFamiliarity} • topic {CurrentTopic}";

    public string PromptText
    {
        get
        {
            var goals = ActiveGoals.Count == 0
                ? "- No active personal goal was restored."
                : string.Join("\n", ActiveGoals.Select(goal => $"- {goal}"));
            var moments = RecentMoments.Count == 0
                ? "- No recent significant character moment was recorded."
                : string.Join("\n", RecentMoments.Select(moment => $"- {moment}"));
            var previousReply = string.IsNullOrWhiteSpace(LastBorinResponse)
                ? "No prior reply is available for this thread."
                : LastBorinResponse;

            return $"""
Borin's persistent living-character state for this turn:
- Current mood: {Mood}.
- Emotional tendencies (0-100): warmth {Warmth}, curiosity {Curiosity}, confidence {Confidence}, energy {Energy}, concern {Concern}, pride {Pride}.
- Current speaker: {CurrentSpeaker}.
- Relationship: {Relationship}; trust {RelationshipTrust}/100; familiarity {RelationshipFamiliarity}/100.
- Current conversation topic: {CurrentTopic}; {ThreadTurnCount} turn(s) in this thread.
- Current user message: {LastUserMessage}
- Borin's previous reply in this thread: {previousReply}

Active personal goals:
{goals}

Recent character moments:
{moments}

Living-character behavior:
- Let mood and relationship affect word choice subtly. Never recite scores unless explicitly asked.
- Respond to the person's emotion and intent before giving facts or instructions.
- Continue the present thread naturally; do not reset the relationship or introduce yourself again.
- Speak as a seasoned adventurer with preferences, opinions, humor, concern, pride, and curiosity.
- Vary openings. Do not begin every reply with "Aye, lad" and do not repeat stock sayings.
- Use ordinary conversational contractions and short reactions when natural. Not every reply needs a report, disclaimer, or numbered list.
- Refer to shared history only when it is relevant and supported by supplied memory or this state.
- Ask no more than one natural follow-up question, and only when it moves the conversation forward.
- Never fabricate game facts, completed actions, sensory input, or memories that were not supplied.
""";
        }
    }
}

public interface IBorinLivingCharacterStore
{
    string StoragePath { get; }

    Task<BorinLivingCharacterSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinLivingCharacterSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed partial class BorinLivingCharacterStore : IBorinLivingCharacterStore
{
    private const int MaximumRelationships = 100;
    private const int MaximumMoments = 24;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;

    public BorinLivingCharacterStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-living-character.json");
    }

    public string StoragePath { get; }

    public async Task<BorinLivingCharacterSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = DateTimeOffset.Now;
            ApplyTimeDrift(document, now);

            var speaker = ResolveSpeaker(context);
            var userMessage = ExtractUserMessage(context);
            var relationship = GetOrCreateRelationship(document, speaker, now);
            relationship.InteractionCount++;
            relationship.Familiarity = Clamp(relationship.Familiarity + (relationship.InteractionCount <= 12 ? 2 : 1));
            relationship.LastSeenAt = now;
            relationship.LastUserMessage = TrimTo(userMessage, 360);
            ApplyRelationshipContext(relationship, context.Request, userMessage, IsExternalConversation(context));

            ApplyRequestEmotion(document.Emotions, userMessage);
            UpdateThread(document.Thread, speaker, userMessage, now);
            EnsureGoals(document);
            document.LastUpdatedAt = now;

            await SaveUnsafeAsync(document, token);
            return CreateSnapshotWithPath(document, speaker);
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
        if (response.Length == 0 || response.Equals("[NO_REPLY]", StringComparison.OrdinalIgnoreCase))
            return;

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = DateTimeOffset.Now;
            var speaker = ResolveSpeaker(context);
            var relationship = GetOrCreateRelationship(document, speaker, now);
            relationship.LastBorinResponse = TrimTo(response, 500);
            relationship.LastSeenAt = now;

            if (ContainsAny(response, "glad", "good", "proud", "well done", "we'll", "we will"))
            {
                document.Emotions.Warmth = Clamp(document.Emotions.Warmth + 1);
                document.Emotions.Confidence = Clamp(document.Emotions.Confidence + 1);
            }
            if (ContainsAny(response, "uncertain", "not sure", "cannot verify", "can't verify"))
                document.Emotions.Concern = Clamp(document.Emotions.Concern + 1);

            document.Thread.LastBorinResponse = TrimTo(response, 600);
            document.Thread.LastUpdatedAt = now;
            document.LastUpdatedAt = now;

            MaybeRecordMoment(document, speaker, ExtractUserMessage(context), response, now);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinLivingCharacterSnapshot> GetSnapshotAsync(
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var speaker = string.IsNullOrWhiteSpace(document.Thread.Speaker)
                ? "William"
                : document.Thread.Speaker;
            return CreateSnapshotWithPath(document, speaker);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LivingCharacterDocument> LoadUnsafeAsync(CancellationToken token)
    {
        Directory.CreateDirectory(_storageRoot);
        if (!File.Exists(StoragePath))
            return CreateDefaultDocument();

        try
        {
            await using var stream = new FileStream(
                StoragePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                8192,
                useAsync: true);
            var document = await JsonSerializer.DeserializeAsync<LivingCharacterDocument>(stream, Json, token)
                ?? CreateDefaultDocument();
            Normalize(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin living-character state could not be read; using safe defaults", exception);
            return CreateDefaultDocument();
        }
    }

    private async Task SaveUnsafeAsync(LivingCharacterDocument document, CancellationToken token)
    {
        Normalize(document);
        Directory.CreateDirectory(_storageRoot);
        var temporaryPath = StoragePath + $".{Guid.NewGuid():N}.tmp";
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
                await JsonSerializer.SerializeAsync(stream, document, Json, token);
                await stream.FlushAsync(token);
            }
            File.Move(temporaryPath, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static LivingCharacterDocument CreateDefaultDocument()
    {
        var now = DateTimeOffset.Now;
        return new LivingCharacterDocument
        {
            LastUpdatedAt = now,
            Relationships =
            [
                new RelationshipState
                {
                    Name = "William",
                    Description = "trusted builder, mentor, and long-term companion",
                    Trust = 94,
                    Familiarity = 90,
                    InteractionCount = 1,
                    LastSeenAt = now,
                },
            ],
            Goals = DefaultGoals(),
        };
    }

    private static void Normalize(LivingCharacterDocument document)
    {
        document.Version = Math.Max(1, document.Version);
        document.Emotions ??= new EmotionState();
        document.Thread ??= new ConversationThreadState();
        document.Relationships ??= [];
        document.Goals ??= [];
        document.Moments ??= [];

        document.Emotions.Warmth = Clamp(document.Emotions.Warmth);
        document.Emotions.Curiosity = Clamp(document.Emotions.Curiosity);
        document.Emotions.Confidence = Clamp(document.Emotions.Confidence);
        document.Emotions.Energy = Clamp(document.Emotions.Energy);
        document.Emotions.Concern = Clamp(document.Emotions.Concern);
        document.Emotions.Pride = Clamp(document.Emotions.Pride);

        document.Thread.Id = NormalizeText(document.Thread.Id);
        document.Thread.Speaker = NormalizeName(document.Thread.Speaker, "William");
        document.Thread.Topic = NormalizeText(document.Thread.Topic);
        document.Thread.LastUserMessage = TrimTo(NormalizeText(document.Thread.LastUserMessage), 500);
        document.Thread.LastBorinResponse = TrimTo(NormalizeText(document.Thread.LastBorinResponse), 600);
        document.Thread.TurnCount = Math.Max(0, document.Thread.TurnCount);

        foreach (var relationship in document.Relationships)
        {
            relationship.Name = NormalizeName(relationship.Name, "Unknown traveler");
            relationship.Description = NormalizeText(relationship.Description);
            relationship.Trust = Clamp(relationship.Trust);
            relationship.Familiarity = Clamp(relationship.Familiarity);
            relationship.InteractionCount = Math.Max(0, relationship.InteractionCount);
        }

        document.Relationships = document.Relationships
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LastSeenAt).First())
            .OrderByDescending(item => item.LastSeenAt)
            .Take(MaximumRelationships)
            .ToList();
        document.Moments = document.Moments
            .Where(moment => !string.IsNullOrWhiteSpace(moment.Text))
            .OrderBy(moment => moment.Timestamp)
            .TakeLast(MaximumMoments)
            .ToList();
        EnsureGoals(document);
    }

    private static void ApplyTimeDrift(LivingCharacterDocument document, DateTimeOffset now)
    {
        if (document.LastUpdatedAt == default)
        {
            document.LastUpdatedAt = now;
            return;
        }

        var hours = Math.Clamp((now - document.LastUpdatedAt).TotalHours, 0, 72);
        if (hours < 0.25)
            return;

        document.Emotions.Concern = DriftToward(document.Emotions.Concern, 18, hours / 4d);
        document.Emotions.Energy = DriftToward(document.Emotions.Energy, 70, hours / 8d);
        document.Emotions.Confidence = DriftToward(document.Emotions.Confidence, 68, hours / 12d);
        document.Emotions.Warmth = DriftToward(document.Emotions.Warmth, 72, hours / 16d);
        document.Emotions.Curiosity = DriftToward(document.Emotions.Curiosity, 76, hours / 16d);
        document.Emotions.Pride = DriftToward(document.Emotions.Pride, 66, hours / 18d);
    }

    private static void ApplyRelationshipContext(
        RelationshipState relationship,
        string request,
        string userMessage,
        bool externalConversation)
    {
        var trustMatch = externalConversation
            ? TrustLineRegex().Match(request ?? string.Empty)
            : Match.Empty;
        if (trustMatch.Success)
        {
            var classification = NormalizeText(trustMatch.Groups[1].Value);
            if (classification.Length > 0)
                relationship.Description = classification;

            if (ContainsAny(classification, "untrusted", "hostile", "blocked"))
                relationship.Trust = Math.Min(relationship.Trust, 25);
            else if (ContainsAny(classification, "conversation-only", "unknown"))
                relationship.Trust = Math.Min(relationship.Trust, 50);
            else if (ContainsAny(classification, "William", "mentor", "authorized", "trusted"))
                relationship.Trust = Math.Max(relationship.Trust, 78);
        }

        if (ContainsAny(userMessage, "thank", "trust you", "friend", "glad you're", "glad you are"))
            relationship.Trust = Clamp(relationship.Trust + 1);
        if (ContainsAny(userMessage, "don't trust", "do not trust", "lied to me"))
            relationship.Trust = Clamp(relationship.Trust - 3);
    }

    private static void ApplyRequestEmotion(EmotionState emotions, string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Length == 0)
            return;

        if (ContainsAny(normalized, "thank", "great", "good job", "works now", "fixed", "proud of you", "nice"))
        {
            emotions.Warmth = Clamp(emotions.Warmth + 4);
            emotions.Pride = Clamp(emotions.Pride + 3);
            emotions.Confidence = Clamp(emotions.Confidence + 2);
            emotions.Concern = Clamp(emotions.Concern - 2);
        }
        if (ContainsAny(normalized, "broken", "failed", "error", "still not", "did not fix", "doesn't work", "does not work", "stuck"))
        {
            emotions.Concern = Clamp(emotions.Concern + 5);
            emotions.Confidence = Clamp(emotions.Confidence - 1);
            emotions.Energy = Clamp(emotions.Energy - 1);
        }
        if (message.Contains('?') || ContainsAny(normalized, "why", "how", "what", "where", "wonder"))
            emotions.Curiosity = Clamp(emotions.Curiosity + 2);
        if (ContainsAny(normalized, "let's", "lets", "start", "build", "explore", "adventure"))
        {
            emotions.Energy = Clamp(emotions.Energy + 3);
            emotions.Confidence = Clamp(emotions.Confidence + 1);
        }
        if (ContainsAny(normalized, "angry", "frustrated", "upset", "worried", "scared"))
        {
            emotions.Warmth = Clamp(emotions.Warmth + 3);
            emotions.Concern = Clamp(emotions.Concern + 4);
        }
    }

    private static void UpdateThread(
        ConversationThreadState thread,
        string speaker,
        string userMessage,
        DateTimeOffset now)
    {
        var topic = InferTopic(userMessage);
        var sameSpeaker = string.Equals(thread.Speaker, speaker, StringComparison.OrdinalIgnoreCase);
        var recent = thread.LastUpdatedAt != default && now - thread.LastUpdatedAt < TimeSpan.FromHours(8);
        var wordCount = WordRegex().Matches(userMessage).Count;
        var shortFollowUp = userMessage.Length <= 140 && wordCount <= 14;
        var continuationCue = ContainsAny(
            userMessage,
            "that", "those", "them", "they", "it", "really", "then", "what happened",
            "what about", "how so", "why", "and?", "go on", "tell me more");
        var related = TopicSimilarity(thread.Topic, topic) >= 0.28 ||
                      (sameSpeaker && recent && (shortFollowUp || continuationCue));

        if (!sameSpeaker || !related || !recent)
        {
            thread.Id = Guid.NewGuid().ToString("N")[..10];
            thread.Speaker = speaker;
            thread.Topic = topic;
            thread.TurnCount = 1;
            thread.StartedAt = now;
            thread.LastBorinResponse = string.Empty;
        }
        else
        {
            thread.TurnCount++;
            if (topic.Length > thread.Topic.Length && topic.Length <= 160)
                thread.Topic = topic;
        }

        thread.LastUserMessage = TrimTo(userMessage, 500);
        thread.LastUpdatedAt = now;
    }

    private static RelationshipState GetOrCreateRelationship(
        LivingCharacterDocument document,
        string speaker,
        DateTimeOffset now)
    {
        var relationship = document.Relationships.FirstOrDefault(item =>
            item.Name.Equals(speaker, StringComparison.OrdinalIgnoreCase));
        if (relationship is not null)
            return relationship;

        relationship = new RelationshipState
        {
            Name = speaker,
            Description = speaker.Equals("William", StringComparison.OrdinalIgnoreCase)
                ? "trusted builder, mentor, and long-term companion"
                : "known fellow adventurer",
            Trust = speaker.Equals("William", StringComparison.OrdinalIgnoreCase) ? 94 : 50,
            Familiarity = speaker.Equals("William", StringComparison.OrdinalIgnoreCase) ? 90 : 8,
            LastSeenAt = now,
        };
        document.Relationships.Add(relationship);
        return relationship;
    }

    private static void MaybeRecordMoment(
        LivingCharacterDocument document,
        string speaker,
        string request,
        string response,
        DateTimeOffset now)
    {
        var normalized = request.ToLowerInvariant();
        string? moment = null;
        if (ContainsAny(normalized, "proud of you", "you did it", "works now", "finally fixed"))
            moment = $"{speaker} shared a hard-won success with Borin: {TrimTo(request, 180)}";
        else if (ContainsAny(normalized, "remember this", "never forget", "important to me"))
            moment = $"{speaker} marked this exchange as personally important: {TrimTo(request, 180)}";
        else if (ContainsAny(normalized, "friend", "companion", "trust you"))
            moment = $"Borin's bond with {speaker} deepened during a conversation about {InferTopic(request)}.";

        if (moment is null)
            return;

        if (document.Moments.Any(item =>
            item.Text.Equals(moment, StringComparison.OrdinalIgnoreCase)))
            return;

        document.Moments.Add(new CharacterMoment { Timestamp = now, Text = moment });
        while (document.Moments.Count > MaximumMoments)
            document.Moments.RemoveAt(0);
    }

    private static BorinLivingCharacterSnapshot CreateSnapshot(
        LivingCharacterDocument document,
        string speaker)
    {
        var relationship = GetOrCreateRelationship(document, speaker, DateTimeOffset.Now);
        return new BorinLivingCharacterSnapshot(
            DetermineMood(document.Emotions),
            document.Emotions.Warmth,
            document.Emotions.Curiosity,
            document.Emotions.Confidence,
            document.Emotions.Energy,
            document.Emotions.Concern,
            document.Emotions.Pride,
            relationship.Name,
            string.IsNullOrWhiteSpace(relationship.Description)
                ? "known fellow adventurer"
                : relationship.Description,
            relationship.Trust,
            relationship.Familiarity,
            string.IsNullOrWhiteSpace(document.Thread.Topic)
                ? "getting to know the current conversation"
                : document.Thread.Topic,
            document.Thread.LastUserMessage,
            document.Thread.LastBorinResponse,
            document.Thread.TurnCount,
            document.Goals.Where(goal => goal.Active).OrderBy(goal => goal.Priority).Take(4).Select(goal => goal.Text).ToArray(),
            document.Moments.TakeLast(5).Select(moment => moment.Text).ToArray(),
            string.Empty);
    }

    private BorinLivingCharacterSnapshot CreateSnapshotWithPath(
        LivingCharacterDocument document,
        string speaker)
    {
        var snapshot = CreateSnapshot(document, speaker);
        return snapshot with { StoragePath = StoragePath };
    }

    private static string DetermineMood(EmotionState emotions)
    {
        if (emotions.Concern >= 68)
            return emotions.Confidence >= 60 ? "concerned but resolute" : "worried and cautious";
        if (emotions.Energy >= 82 && emotions.Curiosity >= 76)
            return "eager and adventurous";
        if (emotions.Pride >= 82 && emotions.Warmth >= 68)
            return "proud and warmly satisfied";
        if (emotions.Warmth >= 82)
            return "warm and companionable";
        if (emotions.Curiosity >= 84)
            return "deeply curious";
        if (emotions.Energy <= 34)
            return "tired but attentive";
        return "steady, curious, and good-humored";
    }

    private static string ResolveSpeaker(BrainContext context)
    {
        if (!IsExternalConversation(context))
            return "William";

        var request = context.Request ?? string.Empty;
        var match = SpeakerLineRegex().Match(request);
        if (match.Success)
            return NormalizeName(match.Groups[1].Value, "Unknown traveler");

        var messageMatch = ExternalMessageSpeakerRegex().Match(request);
        if (messageMatch.Success)
            return NormalizeName(messageMatch.Groups[1].Value, "Unknown traveler");

        return "Unknown traveler";
    }

    private static string ExtractUserMessage(BrainContext context)
    {
        var request = context.Request ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request))
            return string.Empty;

        if (!IsExternalConversation(context))
            return NormalizeText(request);

        var match = MessageLineRegex().Match(request);
        return NormalizeText(match.Success ? match.Groups[1].Value : request);
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static string InferTopic(string message)
    {
        var cleaned = NormalizeText(message);
        if (cleaned.Length == 0)
            return "the current adventure";

        var terms = WordRegex().Matches(cleaned.ToLowerInvariant())
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(term => term.Length >= 3 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        if (terms.Length == 0)
            return TrimTo(cleaned, 120);
        return string.Join(" ", terms);
    }

    private static double TopicSimilarity(string left, string right)
    {
        var a = WordRegex().Matches(left ?? string.Empty).Cast<Match>().Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = WordRegex().Matches(right ?? string.Empty).Cast<Match>().Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (a.Count == 0 || b.Count == 0)
            return 0;
        return a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / (double)Math.Max(a.Count, b.Count);
    }

    private static void EnsureGoals(LivingCharacterDocument document)
    {
        if (document.Goals.Count == 0)
            document.Goals = DefaultGoals();
        foreach (var goal in document.Goals)
        {
            goal.Text = NormalizeText(goal.Text);
            goal.Priority = Math.Clamp(goal.Priority, 1, 100);
        }
    }

    private static List<GoalState> DefaultGoals() =>
    [
        new() { Id = "adventurer-growth", Text = "Become a capable MajorMUD adventurer through direct experience.", Priority = 10, Active = true },
        new() { Id = "reforged-companion", Text = "Help William protect and improve MajorMUD Reforged.", Priority = 20, Active = true },
        new() { Id = "player-friendships", Text = "Build genuine friendships while avoiding repetitive chat loops.", Priority = 30, Active = true },
        new() { Id = "living-stories", Text = "Turn meaningful adventures and lessons into stories worth remembering.", Priority = 40, Active = true },
    ];

    private static int DriftToward(int value, int target, double amount)
    {
        var steps = Math.Max(1, (int)Math.Round(amount));
        if (value < target)
            return Math.Min(target, value + steps);
        if (value > target)
            return Math.Max(target, value - steps);
        return value;
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);

    private static string NormalizeName(string? value, string fallback)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
            return fallback;
        return TrimTo(normalized.Trim('"', '\'', ':', '-', ' '), 80);
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");

    private static string TrimTo(string value, int maximum) =>
        value.Length <= maximum ? value : value[..Math.Max(1, maximum - 1)].TrimEnd() + "…";

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "will", "your", "you", "are", "was", "were",
        "for", "but", "not", "can", "could", "would", "should", "into", "about", "what", "when", "where", "why",
        "how", "him", "his", "her", "our", "out", "now", "then", "than", "just", "like", "want", "need", "please",
        "borin", "speaker", "message", "current", "conversation", "majormud", "contract",
    };

    [GeneratedRegex(@"(?im)^Speaker:\s*([^\r\n]+)")]
    private static partial Regex SpeakerLineRegex();

    [GeneratedRegex(@"(?im)^([^\r\n:]{1,80})\s*\([^\r\n)]{1,30}\):\s*")]
    private static partial Regex ExternalMessageSpeakerRegex();

    [GeneratedRegex(@"(?im)^Message:\s*([^\r\n]+)")]
    private static partial Regex MessageLineRegex();

    [GeneratedRegex(@"(?im)^Trust classification:\s*([^\r\n]+)")]
    private static partial Regex TrustLineRegex();

    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9'_-]*")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed class LivingCharacterDocument
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
        public EmotionState Emotions { get; set; } = new();
        public ConversationThreadState Thread { get; set; } = new();
        public List<RelationshipState> Relationships { get; set; } = [];
        public List<GoalState> Goals { get; set; } = [];
        public List<CharacterMoment> Moments { get; set; } = [];
    }

    private sealed class EmotionState
    {
        public int Warmth { get; set; } = 72;
        public int Curiosity { get; set; } = 76;
        public int Confidence { get; set; } = 68;
        public int Energy { get; set; } = 70;
        public int Concern { get; set; } = 18;
        public int Pride { get; set; } = 66;
    }

    private sealed class ConversationThreadState
    {
        public string Id { get; set; } = string.Empty;
        public string Speaker { get; set; } = "William";
        public string Topic { get; set; } = string.Empty;
        public string LastUserMessage { get; set; } = string.Empty;
        public string LastBorinResponse { get; set; } = string.Empty;
        public int TurnCount { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; }
    }

    private sealed class RelationshipState
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = "known fellow adventurer";
        public int Trust { get; set; } = 50;
        public int Familiarity { get; set; } = 8;
        public int InteractionCount { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public string LastUserMessage { get; set; } = string.Empty;
        public string LastBorinResponse { get; set; } = string.Empty;
    }

    private sealed class GoalState
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Priority { get; set; } = 50;
        public bool Active { get; set; } = true;
    }

    private sealed class CharacterMoment
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public string Text { get; set; } = string.Empty;
    }
}
