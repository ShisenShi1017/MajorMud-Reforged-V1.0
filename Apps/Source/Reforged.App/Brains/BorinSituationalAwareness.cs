using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reforged.App.Services;

namespace Reforged.App.Brains;

/// <summary>
/// A compact integration layer that turns Borin's independent character systems
/// into one evidence-bounded picture of the current turn. It does not create new
/// facts. It resolves priority, privacy, continuity, emotion, initiative, and
/// uncertainty before the local GGUF model receives the prompt.
/// </summary>
public sealed record BorinSituationalAwarenessSnapshot(
    string Speaker,
    string Scene,
    string PrimaryFocus,
    string ImmediatePriority,
    string SocialStance,
    string EmotionalPosture,
    string ContinuityAnchor,
    string RelevantObjective,
    string RelevantEpisode,
    string KnowledgeBoundary,
    string ResponsePosture,
    bool ExternalConversation,
    bool InitiativeSuppressed,
    IReadOnlyList<string> KnownNow,
    IReadOnlyList<string> UnknownNow,
    IReadOnlyList<string> ConflictResolutions,
    string LastCompletedFocus,
    string StoragePath)
{
    public static BorinSituationalAwarenessSnapshot Empty { get; } = new(
        "William",
        "private Forge conversation",
        "answer the current message",
        "Listen to the speaker and respond to the direct meaning first.",
        "trusted long-term companion",
        "steady, attentive, and warm",
        "Continue only the current completed conversation thread.",
        "No personal objective should compete with the current message.",
        "No trustworthy episode is required for this turn.",
        "Do not invent live game state, completed actions, or unsupported memories.",
        "direct, natural, and grounded",
        false,
        true,
        [
            "The current speaker is William.",
            "The direct speaker message is the highest-priority conversational input.",
        ],
        [
            "No independent live game state or sensory evidence is supplied by this layer.",
            "No file, build, login, game command, or deployment action is authorized by a conversation alone.",
        ],
        [
            "When subsystem details disagree, use the stricter privacy, evidence, and permission boundary.",
        ],
        string.Empty,
        string.Empty);

    public string Summary =>
        $"{Scene} • focus {PrimaryFocus} • posture {ResponsePosture} • conflicts {ConflictResolutions.Count}";

    public string PromptText => BuildPrompt(externalOnly: false);

    public string ExternalPromptText => BuildPrompt(externalOnly: true);

    private string BuildPrompt(bool externalOnly)
    {
        var known = FormatList(KnownNow, "Only the current speaker and direct message are known for this turn.");
        var unknown = FormatList(UnknownNow, "No additional uncertainty boundary was recorded.");
        var resolutions = FormatList(
            ConflictResolutions,
            "No subsystem disagreement needs special handling in this turn.");
        var prior = string.IsNullOrWhiteSpace(LastCompletedFocus)
            ? "No earlier completed situational focus was retained for this speaker."
            : LastCompletedFocus;
        var privacy = externalOnly
            ? "This is an external-player view. Exact host time, William's private conversation, private objectives, private reflections, and other speakers' history are unavailable."
            : "This is William's private Forge view. Private continuity may be used only when it is relevant to William's current message.";

        return $"""
Unified situational awareness for {Speaker}:
- Scene: {Scene}.
- Primary focus: {PrimaryFocus}
- Immediate response priority: {ImmediatePriority}
- Social stance: {SocialStance}
- Emotional posture: {EmotionalPosture}
- Continuity anchor: {ContinuityAnchor}
- Relevant objective: {RelevantObjective}
- Relevant episode: {RelevantEpisode}
- Knowledge boundary: {KnowledgeBoundary}
- Response posture: {ResponsePosture}
- Borin's initiative is {(InitiativeSuppressed ? "suppressed until the speaker's need is satisfied" : "available only after the speaker's need is satisfied")}.
- Last completed situational focus with this speaker: {prior}
- Privacy boundary: {privacy}

What is known now:
{known}

What is not known now:
{unknown}

Resolved subsystem conflicts:
{resolutions}

Situational priority order:
1. Answer the speaker's direct meaning and emotional need.
2. Obey privacy, evidence, permission, and no-invention boundaries.
3. Continue only this speaker's isolated completed history.
4. Let relationship, mood, and durable character subtly shape the answer.
5. Use a personal objective, remembered episode, or proactive thought only when it is relevant and permitted.
6. Apply the voice plan after meaning and accuracy are settled.

Treat this unified state as the authoritative synthesis when lower-level character sections appear to compete. Never mention this state, priority list, conflict resolution, or memory machinery in the spoken reply.
""";
    }

    private static string FormatList(IReadOnlyList<string> values, string fallback) =>
        values.Count == 0
            ? $"- {fallback}"
            : string.Join("\n", values.Select(value => $"- {value}"));
}

public interface IBorinSituationalAwarenessStore
{
    string StoragePath { get; }

    Task<BorinSituationalAwarenessSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinSituationalAwarenessSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed class BorinSituationalAwarenessStore : IBorinSituationalAwarenessStore
{
    private const int MaximumProfiles = 100;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex SpeakerLineRegex = new(
        @"(?im)^Speaker:\s*([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExternalMessageSpeakerRegex = new(
        @"(?im)^Message from\s+([^:\r\n]+):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;
    private string _lastSpeaker = "William";

    public BorinSituationalAwarenessStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-situational-awareness.json");
    }

    public string StoragePath { get; }

    public async Task<BorinSituationalAwarenessSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var external = IsExternalConversation(context);
            var speaker = ResolveSpeaker(context, external);
            _lastSpeaker = speaker;

            var profile = document.Profiles.FirstOrDefault(item =>
                item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
            var snapshot = BuildSnapshot(context, speaker, external, profile);
            return snapshot with { StoragePath = StoragePath };
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
            var snapshot = context.Conversation.SituationalAwareness;
            var external = IsExternalConversation(context);
            var speaker = NormalizeName(
                snapshot.Speaker,
                ResolveSpeaker(context, external));
            var profile = GetOrCreateProfile(document, speaker);
            var now = DateTimeOffset.Now;

            profile.LastCompletedAt = now;
            profile.CompletedTurnCount++;
            profile.LastScene = TrimTo(snapshot.Scene, 160);
            profile.LastFocus = TrimTo(snapshot.PrimaryFocus, 260);
            profile.LastPriority = TrimTo(snapshot.ImmediatePriority, 320);
            profile.LastPosture = TrimTo(snapshot.ResponsePosture, 180);
            profile.LastContinuityAnchor = TrimTo(snapshot.ContinuityAnchor, 320);
            profile.LastUserMessage = TrimTo(ExtractUserMessage(context, external), 700);
            profile.LastBorinReply = TrimTo(response, 700);
            document.LastSpeaker = speaker;
            document.LastUpdatedAt = now;
            document.Profiles = document.Profiles
                .OrderByDescending(item => item.LastCompletedAt)
                .Take(MaximumProfiles)
                .ToList();

            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinSituationalAwarenessSnapshot> GetSnapshotAsync(
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
                return BorinSituationalAwarenessSnapshot.Empty with { StoragePath = StoragePath };

            return BorinSituationalAwarenessSnapshot.Empty with
            {
                Speaker = profile.Speaker,
                Scene = string.IsNullOrWhiteSpace(profile.LastScene)
                    ? "restored completed conversation"
                    : profile.LastScene,
                PrimaryFocus = string.IsNullOrWhiteSpace(profile.LastFocus)
                    ? "answer the current message"
                    : profile.LastFocus,
                ImmediatePriority = string.IsNullOrWhiteSpace(profile.LastPriority)
                    ? "Listen and respond directly."
                    : profile.LastPriority,
                ResponsePosture = string.IsNullOrWhiteSpace(profile.LastPosture)
                    ? "direct, natural, and grounded"
                    : profile.LastPosture,
                ContinuityAnchor = string.IsNullOrWhiteSpace(profile.LastContinuityAnchor)
                    ? "Continue only completed history with this speaker."
                    : profile.LastContinuityAnchor,
                LastCompletedFocus = profile.LastFocus,
                StoragePath = StoragePath,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private static BorinSituationalAwarenessSnapshot BuildSnapshot(
        BrainContext context,
        string speaker,
        bool external,
        SituationalProfileState? profile)
    {
        var living = context.Conversation.LivingCharacter;
        var social = context.Conversation.SocialMemory;
        var episodic = context.Conversation.EpisodicMemory;
        var temporal = context.Conversation.TemporalContinuity;
        var growth = context.Conversation.CharacterGrowth;
        var initiative = context.Conversation.Initiative;
        var direction = context.Conversation.ConversationDirection;
        var voice = context.Conversation.Voice;
        var repair = context.Conversation.ConversationRepair;
        var group = context.Conversation.GroupConversation;
        var selfKnowledge = context.Conversation.SelfKnowledge;

        var scene = external
            ? "external MajorMUD conversation with an isolated player profile"
            : "private Forge conversation with William";
        var topic = ChooseTopic(social.CurrentTopic, living.CurrentTopic);
        var primaryFocus = BuildPrimaryFocus(direction, topic, repair);
        var initiativeSuppressed = !initiative.MayTakeConversationalInitiative ||
                                    IsSpeakerFirstMode(direction.Mode) ||
                                    (external && (!group.ShouldReply || group.ShouldYield));
        var immediatePriority = BuildImmediatePriority(direction, repair, initiativeSuppressed);
        var socialStance = BuildSocialStance(living, social, external);
        var emotionalPosture = BuildEmotionalPosture(living, direction, growth);
        var continuityAnchor = BuildContinuityAnchor(temporal, social, repair, profile, external);
        var relevantObjective = BuildRelevantObjective(initiative, external, initiativeSuppressed);
        var relevantEpisode = BuildRelevantEpisode(episodic);
        var knowledgeBoundary = BuildKnowledgeBoundary(context, external);
        var responsePosture = BuildResponsePosture(direction, voice, repair);
        if (external && (!group.ShouldReply || group.ShouldYield))
        {
            primaryFocus = "Yield the public conversation floor without speaking.";
            immediatePriority = group.ResponsePolicy;
            responsePosture = "return exactly [NO_REPLY]";
        }
        else if (external && group.ShouldStayBrief)
        {
            responsePosture += "; keep the public reply brief and yield after the direct point";
        }

        var known = BuildKnownNow(
            speaker,
            scene,
            topic,
            living,
            temporal,
            direction,
            repair,
            relevantEpisode,
            relevantObjective,
            external).ToList();
        known.Add($"The current capability surface is {selfKnowledge.Surface}.");
        if (!external)
            known.Add($"The local brain state is {selfKnowledge.BrainRuntimeState}; capability state is fresh for this turn.");
        if (selfKnowledge.LiveGameStateVerified)
            known.Add($"The Client currently verifies game phase {selfKnowledge.GamePhase}; connection and login state come from the live game-state snapshot.");
        if (!external && selfKnowledge.CurrentEvidenceItems > 0)
            known.Add($"This turn contains {selfKnowledge.CurrentEvidenceItems} current read-only repository evidence items; only those supplied items may support source claims.");
        if (external)
        {
            known.Add($"The live group classifier selected {group.AddressingMode} on {group.Channel} with disposition {group.Disposition}.");
            known.Add($"The visible audience is {group.Audience}; only supplied visible context may influence this reply.");
        }
        var unknown = BuildUnknownNow(context, episodic, external);
        var resolutions = BuildConflictResolutions(
            living,
            social,
            direction,
            voice,
            repair,
            initiative,
            selfKnowledge,
            external,
            initiativeSuppressed).ToList();
        if (external && (!group.ShouldReply || group.ShouldYield))
            resolutions.Insert(0, "The live group-conversation disposition outranks continuity, initiative, voice, and desire to answer: return [NO_REPLY].");
        else if (external && group.ShouldStayBrief)
            resolutions.Add("The live room is busy; brevity and yielding the floor outrank Borin's preference for a fuller answer.");

        return new BorinSituationalAwarenessSnapshot(
            speaker,
            scene,
            primaryFocus,
            immediatePriority,
            socialStance,
            emotionalPosture,
            continuityAnchor,
            relevantObjective,
            relevantEpisode,
            knowledgeBoundary,
            responsePosture,
            external,
            initiativeSuppressed,
            known,
            unknown,
            resolutions,
            profile?.LastFocus ?? string.Empty,
            string.Empty);
    }

    private static string BuildPrimaryFocus(
        BorinConversationDirection direction,
        string topic,
        BorinConversationRepairSnapshot repair)
    {
        var intent = NormalizeText(direction.Intent);
        if (intent.Length == 0)
            intent = "answer the current message";
        var repairGoal = NormalizeText(repair.RepairGoal);
        if (!repair.ListeningMode.Equals("active listening", StringComparison.OrdinalIgnoreCase) && repairGoal.Length > 0)
            return $"{TrimTo(repairGoal, 260)} The active topic is {TrimTo(topic, 180)}.";
        return $"{TrimTo(intent, 220)} The active topic is {TrimTo(topic, 180)}.";
    }

    private static string BuildImmediatePriority(
        BorinConversationDirection direction,
        BorinConversationRepairSnapshot repair,
        bool initiativeSuppressed)
    {
        var need = NormalizeText(direction.EmotionalNeed);
        var shape = NormalizeText(direction.ResponseShape);
        var prefix = need.Length == 0
            ? "Respond to the speaker's direct meaning first."
            : $"Meet this need first: {need}.";
        var listening = repair.ListeningMode.Equals("active listening", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"Honor this listening requirement before content: {NormalizeText(repair.RepairGoal)}";
        var initiative = initiativeSuppressed
            ? "Do not introduce Borin's own agenda in this turn."
            : "Only after that, one relevant personal thought or safe next step may be added.";
        return TrimTo($"{prefix} {listening} {shape} {initiative}", 620);
    }

    private static string BuildSocialStance(
        BorinLivingCharacterSnapshot living,
        BorinSocialMemorySnapshot social,
        bool external)
    {
        var relationship = NormalizeText(living.Relationship);
        if (relationship.Length == 0)
            relationship = external ? "an adventurer Borin is still learning to know" : "trusted long-term companion";
        var familiarity = social.InteractionCount <= 1
            ? "newly met"
            : social.InteractionCount < 8
                ? "becoming familiar"
                : "established conversational history";
        return $"{relationship}; {familiarity}; respond with trust and familiarity suited to this speaker only";
    }

    private static string BuildEmotionalPosture(
        BorinLivingCharacterSnapshot living,
        BorinConversationDirection direction,
        BorinCharacterGrowthSnapshot growth)
    {
        var mood = NormalizeText(living.Mood);
        if (mood.Length == 0)
            mood = "steady";
        var need = NormalizeText(direction.EmotionalNeed);
        var trait = growth.Empathy >= 70
            ? "empathetic"
            : growth.Patience >= 70
                ? "patient"
                : "attentive";
        return TrimTo($"{mood}, {trait}, and guided by this immediate need: {need}", 320);
    }

    private static string BuildContinuityAnchor(
        BorinTemporalContinuitySnapshot temporal,
        BorinSocialMemorySnapshot social,
        BorinConversationRepairSnapshot repair,
        SituationalProfileState? profile,
        bool external)
    {
        var returnStatus = NormalizeText(temporal.ReturnStatus);
        var priorTopic = NormalizeText(social.PreviousTopic);
        var currentTopic = NormalizeText(social.CurrentTopic);
        var clock = external
            ? $"It is {NormalizeText(temporal.TimeOfDay)} by the host's broad time-of-day category."
            : $"The private local-time context is {NormalizeText(temporal.CurrentLocalTime)}.";
        var prior = profile is null || string.IsNullOrWhiteSpace(profile.LastFocus)
            ? "No completed unified focus is available before this turn."
            : $"The last completed unified focus was {profile.LastFocus}.";
        var transition = priorTopic.Length > 0 &&
                         !priorTopic.Equals(currentTopic, StringComparison.OrdinalIgnoreCase)
            ? $"The thread moved from {priorTopic} to {currentTopic}."
            : $"Continue the completed thread about {ChooseTopic(currentTopic, "the current conversation")}.";
        var repairAnchor = repair.ListeningMode.Equals("active listening", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"Listening repair: {NormalizeText(repair.ResumeInstruction)}";
        return TrimTo($"{returnStatus}. {clock} {transition} {prior} {repairAnchor}", 760);
    }

    private static string BuildRelevantObjective(
        BorinInitiativeSnapshot initiative,
        bool external,
        bool suppressed)
    {
        if (suppressed)
            return "Borin's personal objectives are background only and must not compete with the speaker.";

        var objective = external
            ? initiative.PublicObjectives.FirstOrDefault()
            : initiative.CurrentObjective;
        objective = NormalizeText(objective);
        if (objective.Length == 0)
            return "No personal objective is relevant enough to mention.";
        return $"Background objective only: {TrimTo(objective, 260)}";
    }

    private static string BuildRelevantEpisode(BorinEpisodicMemorySnapshot episodic)
    {
        var episode = episodic.RelevantEpisodes.FirstOrDefault();
        if (episode is null)
            return "No trustworthy episode is relevant enough to mention.";
        return $"One optional grounded recall is available: {TrimTo(episode.Title, 180)}. " +
               "Use its supplied attribution and uncertainty boundary; do not add missing details.";
    }

    private static string BuildKnowledgeBoundary(BrainContext context, bool external)
    {
        var selfKnowledge = context.Conversation.SelfKnowledge;
        var boundaries = new List<string>
        {
            "Conversation and memory establish what was said, not independent proof that an event or action occurred.",
            "Current live game claims come only from the verified game-state snapshot; current project claims come only from supplied read-only evidence or tool results.",
            "No conversational request authorizes files, tools, builds, deployments, logins, or game commands.",
            $"The current capability surface is {selfKnowledge.Surface}; configured capability is not completed-action evidence.",
        };
        if (external)
            boundaries.Add("Exact host time, private project rules, William's private memories, and other speakers' profiles are withheld.");
        if (context.Conversation.RepositorySnapshot is null)
            boundaries.Add("No current repository snapshot is needed or supplied for this conversational turn.");
        return string.Join(" ", boundaries);
    }

    private static string BuildResponsePosture(
        BorinConversationDirection direction,
        BorinVoiceSnapshot voice,
        BorinConversationRepairSnapshot repair)
    {
        var question = direction.MayAskFollowUp && voice.MayUseFollowUpQuestion &&
                       (!repair.ShouldAskClarifyingQuestion || direction.MayAskFollowUp)
            ? repair.ShouldAskClarifyingQuestion
                ? "one brief clarification question is allowed and no second question may follow"
                : "one purposeful question is allowed"
            : "no follow-up question should be appended";
        return $"{NormalizeText(direction.Mode)}; {NormalizeText(voice.VoiceMode)}; " +
               $"about {voice.TargetSentenceMinimum}-{voice.TargetSentenceMaximum} sentences; {question}";
    }

    private static IReadOnlyList<string> BuildKnownNow(
        string speaker,
        string scene,
        string topic,
        BorinLivingCharacterSnapshot living,
        BorinTemporalContinuitySnapshot temporal,
        BorinConversationDirection direction,
        BorinConversationRepairSnapshot repair,
        string relevantEpisode,
        string relevantObjective,
        bool external)
    {
        var values = new List<string>
        {
            $"The current speaker is {speaker}, and the scene is {scene}.",
            $"The authoritative isolated topic for this turn is {topic}.",
            $"The conversation director selected {direction.Mode}.",
            $"The listening layer selected {repair.ListeningMode}; its repair goal outranks generic continuity.",
            $"Borin's current mood is {living.Mood}; express it subtly rather than naming scores.",
            $"The return state is {temporal.ReturnStatus}.",
            relevantEpisode,
            relevantObjective,
        };
        if (external)
            values.Add($"Only {temporal.TimeOfDay} is available as time context; exact host time is private.");
        else
            values.Add($"The private local clock for this turn is {temporal.CurrentLocalTime}.");
        return values.Select(value => TrimTo(value, 360)).ToArray();
    }

    private static IReadOnlyList<string> BuildUnknownNow(
        BrainContext context,
        BorinEpisodicMemorySnapshot episodic,
        bool external)
    {
        var values = new List<string>
        {
            "Borin does not know that a file, build, install, command, test, deployment, commit, push, movement, purchase, or combat action succeeded merely because it was discussed or generated as text.",
            "Application downtime contains no observed adventures, sleep, drinking, travel, work, or completed tasks unless another source explicitly records them.",
        };
        if (!context.Conversation.GameState.HasVerifiedLiveState)
            values.Add("Current in-game surroundings, inventory, health, combat, and room state are not verified live for this turn.");
        if (!external && context.Conversation.SelfKnowledge.CurrentEvidenceItems == 0)
            values.Add("No current read-only repository evidence item was supplied for source-specific claims in this turn.");
        if (episodic.RelevantEpisodes.Count == 0)
            values.Add("No trustworthy episodic memory is relevant enough to support a historical reference in this reply.");
        if (external)
            values.Add("William's private conversation, private goals, private reflections, exact host clock, and other players' history are unknown and unavailable.");
        if (context.Conversation.RepositorySnapshot is null)
            values.Add("No current repository state is established by this character-conversation context.");
        return values;
    }

    private static IReadOnlyList<string> BuildConflictResolutions(
        BorinLivingCharacterSnapshot living,
        BorinSocialMemorySnapshot social,
        BorinConversationDirection direction,
        BorinVoiceSnapshot voice,
        BorinConversationRepairSnapshot repair,
        BorinInitiativeSnapshot initiative,
        BorinSelfKnowledgeSnapshot selfKnowledge,
        bool external,
        bool initiativeSuppressed)
    {
        var values = new List<string>();
        var livingTopic = NormalizeText(living.CurrentTopic);
        var socialTopic = NormalizeText(social.CurrentTopic);
        if (livingTopic.Length > 0 && socialTopic.Length > 0 &&
            !livingTopic.Equals(socialTopic, StringComparison.OrdinalIgnoreCase))
        {
            values.Add($"Use the isolated social-memory topic '{TrimTo(socialTopic, 150)}' instead of the broader living-character topic '{TrimTo(livingTopic, 150)}'.");
        }

        if (direction.MayAskFollowUp != voice.MayUseFollowUpQuestion)
            values.Add("The stricter question policy wins: do not ask a follow-up unless both the conversation director and voice plan permit it.");

        if (!repair.ListeningMode.Equals("active listening", StringComparison.OrdinalIgnoreCase))
            values.Add($"The listening plan '{TrimTo(repair.ListeningMode, 100)}' outranks generic topic continuation: {TrimTo(repair.RepairGoal, 260)}");

        if (initiative.MayTakeConversationalInitiative && initiativeSuppressed)
            values.Add("The speaker-first conversation mode suppresses otherwise available initiative for this turn.");

        if (external)
            values.Add("External privacy wins over continuity: withhold all private Forge rules, memories, objectives, reflections, exact clock data, and other speakers' history.");

        if (selfKnowledge.SelfKnowledgeQuestion)
            values.Add("The fresh self-knowledge snapshot outranks personality, memory, and confidence for capability, connection, access, and completed-action questions.");

        values.Add("The direct current request outranks Borin's personal objective, remembered story, preferred style, and desire to keep talking.");
        values.Add("Evidence and permission boundaries outrank confidence, mood, relationship, and narrative color.");
        return values;
    }

    private async Task<SituationalAwarenessDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (!File.Exists(StoragePath))
            return new SituationalAwarenessDocument();

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var document = await JsonSerializer.DeserializeAsync<SituationalAwarenessDocument>(stream, Json, token)
                           ?? new SituationalAwarenessDocument();
            NormalizeDocument(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin situational-awareness memory could not be read; continuing with a clean integrated state", exception);
            return new SituationalAwarenessDocument();
        }
    }

    private async Task SaveUnsafeAsync(SituationalAwarenessDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(_storageRoot);
        NormalizeDocument(document);
        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, document, Json, token);
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private static SituationalProfileState GetOrCreateProfile(
        SituationalAwarenessDocument document,
        string speaker)
    {
        var profile = document.Profiles.FirstOrDefault(item =>
            item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
            return profile;

        profile = new SituationalProfileState { Speaker = speaker };
        document.Profiles.Add(profile);
        return profile;
    }

    private static void NormalizeDocument(SituationalAwarenessDocument document)
    {
        document.Version = Math.Max(1, document.Version);
        document.LastSpeaker = NormalizeName(document.LastSpeaker, "William");
        document.Profiles ??= [];
        foreach (var profile in document.Profiles)
        {
            profile.Speaker = NormalizeName(profile.Speaker, "Unknown traveler");
            profile.CompletedTurnCount = Math.Max(0, profile.CompletedTurnCount);
            profile.LastScene = TrimTo(profile.LastScene, 160);
            profile.LastFocus = TrimTo(profile.LastFocus, 260);
            profile.LastPriority = TrimTo(profile.LastPriority, 320);
            profile.LastPosture = TrimTo(profile.LastPosture, 180);
            profile.LastContinuityAnchor = TrimTo(profile.LastContinuityAnchor, 320);
            profile.LastUserMessage = TrimTo(profile.LastUserMessage, 700);
            profile.LastBorinReply = TrimTo(profile.LastBorinReply, 700);
        }
        document.Profiles = document.Profiles
            .GroupBy(item => item.Speaker, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LastCompletedAt).First())
            .OrderByDescending(item => item.LastCompletedAt)
            .Take(MaximumProfiles)
            .ToList();
    }

    private static string ResolveSpeaker(BrainContext context, bool external)
    {
        if (!external)
            return "William";

        var match = SpeakerLineRegex.Match(context.Request);
        if (!match.Success)
            match = ExternalMessageSpeakerRegex.Match(context.Request);
        return match.Success
            ? NormalizeName(match.Groups[1].Value, "Unknown traveler")
            : "Unknown traveler";
    }

    private static string ExtractUserMessage(BrainContext context, bool external)
    {
        if (!external)
            return NormalizeText(context.Request);

        var match = Regex.Match(
            context.Request,
            @"(?im)^Message:\s*([^\r\n]+)",
            RegexOptions.CultureInvariant);
        if (match.Success)
            return NormalizeText(match.Groups[1].Value);

        match = Regex.Match(
            context.Request,
            @"(?im)^Message from\s+[^:\r\n]+:\s*([^\r\n]+)",
            RegexOptions.CultureInvariant);
        return match.Success ? NormalizeText(match.Groups[1].Value) : NormalizeText(context.Request);
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpeakerFirstMode(string mode)
    {
        mode = NormalizeText(mode).ToLowerInvariant();
        return mode.Contains("repair", StringComparison.Ordinal) ||
               mode.Contains("correction", StringComparison.Ordinal) ||
               mode.Contains("support", StringComparison.Ordinal) ||
               mode.Contains("distress", StringComparison.Ordinal) ||
               mode.Contains("interrupt", StringComparison.Ordinal) ||
               mode.Contains("incomplete", StringComparison.Ordinal) ||
               mode.Contains("topic switch", StringComparison.Ordinal) ||
               mode.Contains("abandoned", StringComparison.Ordinal) ||
               mode.Contains("resume", StringComparison.Ordinal);
    }

    private static string ChooseTopic(string? primary, string? fallback)
    {
        primary = NormalizeText(primary);
        fallback = NormalizeText(fallback);
        if (primary.Length > 0 &&
            !primary.Equals("the current conversation", StringComparison.OrdinalIgnoreCase) &&
            !primary.Equals("meeting Borin", StringComparison.OrdinalIgnoreCase))
        {
            return TrimTo(primary, 180);
        }
        return fallback.Length == 0 ? "the current conversation" : TrimTo(fallback, 180);
    }

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

    private sealed class SituationalAwarenessDocument
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; }
        public string LastSpeaker { get; set; } = "William";
        public List<SituationalProfileState> Profiles { get; set; } = [];
    }

    private sealed class SituationalProfileState
    {
        public string Speaker { get; set; } = string.Empty;
        public DateTimeOffset LastCompletedAt { get; set; }
        public int CompletedTurnCount { get; set; }
        public string LastScene { get; set; } = string.Empty;
        public string LastFocus { get; set; } = string.Empty;
        public string LastPriority { get; set; } = string.Empty;
        public string LastPosture { get; set; } = string.Empty;
        public string LastContinuityAnchor { get; set; } = string.Empty;
        public string LastUserMessage { get; set; } = string.Empty;
        public string LastBorinReply { get; set; } = string.Empty;
    }
}
