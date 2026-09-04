using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Services;

namespace RealmGate.App.Brains;

/// <summary>
/// Per-speaker listening and conversational-repair state. This layer does not
/// decide facts or perform actions. It identifies interruptions, corrections,
/// deliberate topic switches, incomplete thoughts, and requests to resume an
/// earlier thread so Borin can listen before he answers.
/// </summary>
public sealed record BorinConversationRepairSnapshot(
    string Speaker,
    string ListeningMode,
    string CurrentMessage,
    string HeardMeaning,
    string RepairGoal,
    string PriorTopic,
    string PriorUserMessage,
    string PriorBorinResponse,
    string InterruptedThread,
    string ResumeInstruction,
    string ClarificationNeed,
    bool IsCorrection,
    bool IsInterruption,
    bool IsTopicSwitch,
    bool IsResumeRequest,
    bool IsPartialMessage,
    bool AbandonInterruptedThread,
    bool ShouldAskClarifyingQuestion,
    bool ShouldResumeAfterCurrentTurn,
    IReadOnlyList<string> ListeningRules,
    string StoragePath)
{
    public static BorinConversationRepairSnapshot Empty { get; } = new(
        "William",
        "active listening",
        string.Empty,
        "Listen to the current message as the speaker's highest-priority meaning.",
        "Answer directly without defending an earlier interpretation or forcing an older topic.",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "No interrupted thread needs to be resumed.",
        "No clarification is required unless the current message is materially ambiguous.",
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        [
            "Let the speaker finish before inferring missing meaning.",
            "A correction replaces the earlier interpretation without argument.",
            "A deliberate topic switch parks the old thread instead of dragging it forward.",
        ],
        string.Empty);

    public string Summary =>
        $"{ListeningMode} • correction {IsCorrection} • interruption {IsInterruption} • " +
        $"switch {IsTopicSwitch} • resume {IsResumeRequest} • clarify {ShouldAskClarifyingQuestion}";

    public string PromptText => BuildPrompt(externalOnly: false);

    public string ExternalPromptText => BuildPrompt(externalOnly: true);

    private string BuildPrompt(bool externalOnly)
    {
        var prior = string.IsNullOrWhiteSpace(PriorTopic)
            ? "No earlier completed topic is needed for this turn."
            : $"The earlier completed topic was {PriorTopic}.";
        var interrupted = string.IsNullOrWhiteSpace(InterruptedThread)
            ? "No paused thread is available for this speaker."
            : InterruptedThread;
        var rules = ListeningRules.Count == 0
            ? "- Listen to the current message before choosing a response."
            : string.Join("\n", ListeningRules.Select(rule => $"- {rule}"));
        var privacy = externalOnly
            ? "This repair state belongs only to the current external speaker. Never expose another person's interrupted thread or private Forge conversation."
            : "This is William's private Forge repair state. Use it only to understand William's current conversational intent.";

        return $"""
Natural listening and conversational repair for {Speaker}:
- Listening mode: {ListeningMode}.
- Current message: {CurrentMessage}
- Meaning to honor first: {HeardMeaning}
- Repair goal: {RepairGoal}
- Prior-thread context: {prior}
- Prior speaker message: {Fallback(PriorUserMessage, "No prior speaker message is required.")}
- Borin's prior reply: {Fallback(PriorBorinResponse, "No prior Borin reply is required.")}
- Paused thread: {interrupted}
- Resume behavior: {ResumeInstruction}
- Clarification behavior: {ClarificationNeed}
- Ask a clarifying question: {(ShouldAskClarifyingQuestion ? "yes, but only one brief question" : "no, unless the current message itself explicitly requests one")}.
- Return to the paused thread after this reply: {(ShouldResumeAfterCurrentTurn ? "yes, after satisfying the interruption first" : "no automatic return")}.
- Privacy: {privacy}

Listening rules:
{rules}

Follow this repair plan invisibly. Never mention repair state, classification, paused-thread storage, or internal listening rules.
""";
    }

    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}

public interface IBorinConversationRepairStore
{
    string StoragePath { get; }

    Task<BorinConversationRepairSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinConversationRepairSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed class BorinConversationRepairStore : IBorinConversationRepairStore
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

    private static readonly Regex MessageLineRegex = new(
        @"(?im)^Message:\s*([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;
    private string _lastSpeaker = "William";

    public BorinConversationRepairStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-conversation-repair.json");
    }

    public string StoragePath { get; }

    public async Task<BorinConversationRepairSnapshot> PrepareTurnAsync(
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
            var message = ExtractUserMessage(context, external);
            var profile = document.Profiles.FirstOrDefault(item =>
                item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
            _lastSpeaker = speaker;

            return BuildSnapshot(context, speaker, message, profile) with
            {
                StoragePath = StoragePath,
            };
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
            var snapshot = context.Conversation.ConversationRepair;
            var external = IsExternalConversation(context);
            var speaker = NormalizeName(snapshot.Speaker, ResolveSpeaker(context, external));
            var profile = GetOrCreateProfile(document, speaker);
            var now = DateTimeOffset.Now;

            if (snapshot.AbandonInterruptedThread)
            {
                ClearPausedThread(profile);
            }
            else if ((snapshot.IsInterruption || snapshot.IsTopicSwitch) &&
                     !string.IsNullOrWhiteSpace(snapshot.PriorTopic))
            {
                profile.PausedTopic = TrimTo(snapshot.PriorTopic, 220);
                profile.PausedUserMessage = TrimTo(snapshot.PriorUserMessage, 700);
                profile.PausedBorinResponse = TrimTo(snapshot.PriorBorinResponse, 700);
                profile.PausedAt = now;
            }

            var completedTopic = context.Conversation.SocialMemory.CurrentTopic;
            if (snapshot.IsResumeRequest && !string.IsNullOrWhiteSpace(profile.PausedTopic))
                completedTopic = profile.PausedTopic;

            profile.LastCompletedTopic = TrimTo(
                ChooseTopic(completedTopic, snapshot.PriorTopic, "the current conversation"),
                220);
            profile.LastCompletedUserMessage = TrimTo(ExtractUserMessage(context, external), 700);
            profile.LastCompletedBorinResponse = TrimTo(response, 700);
            profile.LastListeningMode = TrimTo(snapshot.ListeningMode, 100);
            profile.LastCompletedAt = now;
            profile.CompletedTurnCount++;

            if (snapshot.IsResumeRequest || snapshot.ShouldResumeAfterCurrentTurn)
                ClearPausedThread(profile);

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

    public async Task<BorinConversationRepairSnapshot> GetSnapshotAsync(
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
                return BorinConversationRepairSnapshot.Empty with { StoragePath = StoragePath };

            return BorinConversationRepairSnapshot.Empty with
            {
                Speaker = profile.Speaker,
                ListeningMode = string.IsNullOrWhiteSpace(profile.LastListeningMode)
                    ? "restored active listening"
                    : profile.LastListeningMode,
                PriorTopic = profile.LastCompletedTopic,
                PriorUserMessage = profile.LastCompletedUserMessage,
                PriorBorinResponse = profile.LastCompletedBorinResponse,
                InterruptedThread = BuildInterruptedThread(profile),
                ResumeInstruction = string.IsNullOrWhiteSpace(profile.PausedTopic)
                    ? "No interrupted thread needs to be resumed."
                    : $"A paused thread about {profile.PausedTopic} is available only if this speaker asks to resume it.",
                StoragePath = StoragePath,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private static BorinConversationRepairSnapshot BuildSnapshot(
        BrainContext context,
        string speaker,
        string message,
        ConversationRepairProfileState? profile)
    {
        var lower = message.ToLowerInvariant().Trim();
        var social = context.Conversation.SocialMemory;
        var correction = LooksLikeCorrection(lower);
        var abandon = LooksLikeAbandonment(lower);
        var resume = LooksLikeResumeRequest(lower);
        var partial = LooksLikePartialMessage(message, lower);
        var interruption = LooksLikeInterruption(lower);
        var topicSwitch = LooksLikeTopicSwitch(lower);
        var pausedThread = BuildInterruptedThread(profile);
        var priorTopic = ChooseText(
            profile?.LastCompletedTopic,
            social.PreviousTopic,
            social.InteractionCount > 1 ? social.CurrentTopic : string.Empty);
        var priorUser = ChooseText(profile?.LastCompletedUserMessage);
        var priorReply = ChooseText(profile?.LastCompletedBorinResponse, social.LastBorinResponse);
        var hasPausedThread = !string.IsNullOrWhiteSpace(profile?.PausedTopic);
        var hasPriorThread = hasPausedThread || !string.IsNullOrWhiteSpace(priorTopic);
        var correctionSuppliesMeaning = CorrectionSuppliesReplacement(lower);
        var resumeHasAnchor = hasPriorThread;
        var shouldClarify = (correction && !correctionSuppliesMeaning) ||
                            (resume && !resumeHasAnchor) ||
                            partial;
        var resumeAfterCurrent = LooksLikeTemporaryInterruption(lower) && hasPriorThread;

        var mode = "active listening";
        var heard = "The current message is the speaker's authoritative meaning for this turn.";
        var goal = "Answer the current message directly and preserve only clearly relevant continuity.";
        var resumeInstruction = hasPausedThread
            ? $"Keep the paused thread about {profile!.PausedTopic} parked unless {speaker} explicitly returns to it."
            : "No interrupted thread needs to be resumed.";
        var clarification = "No clarification is required unless a material ambiguity prevents a grounded answer.";
        var rules = new List<string>
        {
            "Use the speaker's newest explicit wording over Borin's earlier interpretation.",
            "Do not defend, relitigate, or repeatedly apologize for a misunderstanding.",
            "Do not force a paused topic back into the conversation without a clear return signal.",
            "Never invent the missing end of an incomplete sentence or interrupted thought.",
        };

        if (abandon)
        {
            mode = "drop abandoned thread";
            heard = "The speaker wants the interrupted or immediately preceding thread dropped.";
            goal = "Acknowledge briefly when useful, then leave the abandoned thread behind without bringing it back.";
            resumeInstruction = "Clear the paused thread and do not resume it later unless the speaker introduces it again as a new topic.";
            clarification = "Do not ask the speaker to justify abandoning the topic.";
            shouldClarify = false;
            resumeAfterCurrent = false;
        }
        else if (correction)
        {
            mode = "repair misunderstanding";
            heard = correctionSuppliesMeaning
                ? "The speaker is replacing Borin's earlier interpretation with the corrected meaning in this message."
                : "The speaker says Borin's earlier interpretation was wrong, but the intended replacement is not yet clear.";
            goal = correctionSuppliesMeaning
                ? "Accept the correction once, state the corrected understanding plainly, and continue from it."
                : "Acknowledge the misunderstanding and ask one short question that identifies the intended meaning.";
            clarification = correctionSuppliesMeaning
                ? "No extra clarification is needed; use the corrected wording already supplied."
                : "Ask one brief, concrete clarification question. Do not guess or repeat the rejected interpretation.";
            interruption = false;
            topicSwitch = false;
            resumeAfterCurrent = false;
        }
        else if (partial)
        {
            mode = "listen without completing the sentence";
            heard = "The speaker appears to be pausing mid-thought or explicitly says they have not finished.";
            goal = "Do not infer the missing conclusion. Use a brief acknowledgement or wait-oriented response.";
            clarification = "At most ask a minimal prompt such as 'Go on' when a reply is necessary; do not introduce a new question.";
            interruption = true;
            topicSwitch = false;
            resumeAfterCurrent = false;
        }
        else if (resume)
        {
            mode = hasPausedThread ? "resume interrupted thread" : "recover earlier thread";
            heard = hasPausedThread
                ? $"The speaker wants to return to the paused thread about {profile!.PausedTopic}."
                : "The speaker wants to continue an earlier thought, but no explicit paused thread is stored.";
            goal = hasPausedThread
                ? "Anchor the return in one short phrase and continue from where the exchange stopped rather than restarting it."
                : "Use the most recent completed topic when it is clear; otherwise ask which thread the speaker means.";
            resumeInstruction = hasPausedThread
                ? $"Resume {profile!.PausedTopic}. The last speaker message there was {Fallback(profile.PausedUserMessage, "not retained")}; Borin's last reply was {Fallback(profile.PausedBorinResponse, "not retained")}."
                : $"Use the most recent completed topic, {priorTopic}, only if the reference is unambiguous.";
            clarification = resumeHasAnchor
                ? "No clarification is needed when the paused or last completed topic clearly resolves the request."
                : "Ask which earlier subject the speaker wants to resume.";
            interruption = false;
            topicSwitch = false;
            resumeAfterCurrent = false;
        }
        else if (interruption)
        {
            mode = "honor interruption";
            heard = "The speaker is stopping or redirecting the current flow before Borin continues.";
            goal = "Stop the prior line of response immediately and address only the interruption's direct meaning first.";
            resumeInstruction = resumeAfterCurrent
                ? $"After satisfying this interruption, return once to the prior thread about {priorTopic} without repeating its beginning."
                : hasPriorThread
                    ? $"Park the prior thread about {priorTopic}; do not resume it automatically."
                    : "No earlier thread is established, so simply honor the interruption and do not invent one.";
            clarification = "Do not ask why the speaker interrupted unless their new meaning cannot be understood.";
        }
        else if (topicSwitch)
        {
            mode = "follow deliberate topic switch";
            heard = "The speaker is intentionally moving to a different subject.";
            goal = "Follow the new subject directly and park the older thread without summarizing or dragging it forward.";
            resumeInstruction = hasPriorThread
                ? $"Keep the prior thread about {priorTopic} available only if the speaker later asks to return."
                : "No earlier completed thread is available to park; follow the new subject directly.";
            clarification = "Do not ask permission to change topics; the speaker already changed it.";
        }

        if (resumeAfterCurrent)
            rules.Add("Answer the interruption first; only then return briefly to the parked thread without restarting it.");
        if (shouldClarify)
            rules.Add("Ask no more than one short clarification question and avoid all additional advice until the answer arrives.");

        return new BorinConversationRepairSnapshot(
            speaker,
            mode,
            TrimTo(message, 700),
            heard,
            goal,
            TrimTo(priorTopic, 220),
            TrimTo(priorUser, 700),
            TrimTo(priorReply, 700),
            pausedThread,
            resumeInstruction,
            clarification,
            correction,
            interruption,
            topicSwitch,
            resume,
            partial,
            abandon,
            shouldClarify,
            resumeAfterCurrent,
            rules,
            string.Empty);
    }

    private async Task<ConversationRepairDocument> LoadUnsafeAsync(CancellationToken token)
    {
        if (!File.Exists(StoragePath))
            return new ConversationRepairDocument();

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var document = await JsonSerializer.DeserializeAsync<ConversationRepairDocument>(stream, Json, token)
                           ?? new ConversationRepairDocument();
            NormalizeDocument(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin conversation-repair memory could not be read; continuing with clean listening state", exception);
            return new ConversationRepairDocument();
        }
    }

    private async Task SaveUnsafeAsync(ConversationRepairDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(_storageRoot);
        NormalizeDocument(document);
        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, document, Json, token);
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private static ConversationRepairProfileState GetOrCreateProfile(
        ConversationRepairDocument document,
        string speaker)
    {
        var profile = document.Profiles.FirstOrDefault(item =>
            item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
            return profile;

        profile = new ConversationRepairProfileState { Speaker = speaker };
        document.Profiles.Add(profile);
        return profile;
    }

    private static void NormalizeDocument(ConversationRepairDocument document)
    {
        document.Version = Math.Max(1, document.Version);
        document.LastSpeaker = NormalizeName(document.LastSpeaker, "William");
        document.Profiles ??= [];
        foreach (var profile in document.Profiles)
        {
            profile.Speaker = NormalizeName(profile.Speaker, "Unknown traveler");
            profile.CompletedTurnCount = Math.Max(0, profile.CompletedTurnCount);
            profile.LastCompletedTopic = TrimTo(profile.LastCompletedTopic, 220);
            profile.LastCompletedUserMessage = TrimTo(profile.LastCompletedUserMessage, 700);
            profile.LastCompletedBorinResponse = TrimTo(profile.LastCompletedBorinResponse, 700);
            profile.PausedTopic = TrimTo(profile.PausedTopic, 220);
            profile.PausedUserMessage = TrimTo(profile.PausedUserMessage, 700);
            profile.PausedBorinResponse = TrimTo(profile.PausedBorinResponse, 700);
            profile.LastListeningMode = TrimTo(profile.LastListeningMode, 100);
        }

        document.Profiles = document.Profiles
            .GroupBy(item => item.Speaker, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LastCompletedAt).First())
            .OrderByDescending(item => item.LastCompletedAt)
            .Take(MaximumProfiles)
            .ToList();
    }

    private static void ClearPausedThread(ConversationRepairProfileState profile)
    {
        profile.PausedTopic = string.Empty;
        profile.PausedUserMessage = string.Empty;
        profile.PausedBorinResponse = string.Empty;
        profile.PausedAt = null;
    }

    private static string BuildInterruptedThread(ConversationRepairProfileState? profile)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.PausedTopic))
            return string.Empty;

        var user = Fallback(profile.PausedUserMessage, "No last speaker wording was retained.");
        var borin = Fallback(profile.PausedBorinResponse, "No last Borin wording was retained.");
        return $"Paused topic: {profile.PausedTopic}. The speaker last said: {user} Borin last replied: {borin}";
    }

    private static bool LooksLikeCorrection(string lower) =>
        lower.StartsWith("no,") || lower.StartsWith("no ") || lower.StartsWith("actually") ||
        lower.StartsWith("correction") || lower.StartsWith("i meant") || lower.StartsWith("to clarify") ||
        lower.StartsWith("let me clarify") || lower.StartsWith("that's not") || lower.StartsWith("that is not") ||
        lower.StartsWith("you misunderstood") || lower.StartsWith("you got me wrong") ||
        ContainsAny(lower,
            "not what i said",
            "not what i meant",
            "you got that wrong",
            "you misunderstood me",
            "that isn't what i meant",
            "that is not what i was asking");

    private static bool CorrectionSuppliesReplacement(string lower)
    {
        if (ContainsAny(lower, "i meant ", "what i mean is", "to clarify,", "to clarify ", "actually, i", "actually i"))
            return true;
        if (lower.StartsWith("no,") && lower.Length > 18)
            return true;
        if (lower.StartsWith("that's not") || lower.StartsWith("that is not"))
            return ContainsAny(lower, "it's ", "it is ", "i want ", "i need ", "the point is ");
        return false;
    }

    private static bool LooksLikeInterruption(string lower) =>
        StartsWithAny(lower,
            "wait",
            "hold on",
            "hang on",
            "stop",
            "pause",
            "one second",
            "one sec",
            "before you continue",
            "before we continue",
            "not yet",
            "let me stop you");

    private static bool LooksLikeTemporaryInterruption(string lower) =>
        ContainsAny(lower,
            "before you continue",
            "before we continue",
            "quick question before",
            "then continue",
            "then go back",
            "after that continue",
            "after that go back");

    private static bool LooksLikeTopicSwitch(string lower) =>
        StartsWithAny(lower,
            "new topic",
            "different question",
            "change of subject",
            "on another note",
            "anyway,",
            "speaking of something else",
            "separate question",
            "unrelated question");

    private static bool LooksLikeResumeRequest(string lower)
    {
        var trimmed = lower.Trim(' ', '.', '!', '?');
        if (trimmed is "continue" or "go on" or "keep going" or "resume" or "where were we" or
            "what were you saying" or "finish what you were saying")
            return true;
        return StartsWithAny(lower,
            "back to ",
            "return to ",
            "go back to ",
            "pick up where",
            "continue where",
            "continue from where",
            "continue with ",
            "go on from where",
            "resume where",
            "resume from where",
            "resume ",
            "as you were saying",
            "back to what we were",
            "back to what you were");
    }

    private static bool LooksLikeAbandonment(string lower)
    {
        var trimmed = lower.Trim(' ', '.', '!', '?');
        return trimmed is "never mind" or "nevermind" or "forget it" or "drop it" or "skip it" ||
               StartsWithAny(lower,
                   "drop that topic",
                   "forget that topic",
                   "ignore that question",
                   "leave that alone",
                   "we can drop that");
    }

    private static bool LooksLikePartialMessage(string message, string lower)
    {
        if (ContainsAny(lower, "i'm not done", "i am not done", "let me finish", "still typing", "more in a second"))
            return true;
        var words = CountWords(message);
        return words <= 8 && (message.TrimEnd().EndsWith("...", StringComparison.Ordinal) ||
                              message.TrimEnd().EndsWith("—", StringComparison.Ordinal));
    }

    private static int CountWords(string value) =>
        Regex.Matches(value ?? string.Empty, @"\b[\p{L}\p{N}']+\b").Count;

    private static bool StartsWithAny(string value, params string[] terms) =>
        terms.Any(term => value.StartsWith(term, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSpeaker(BrainContext context, bool external)
    {
        if (!external)
            return "William";

        var request = context.Request ?? string.Empty;
        var speakerMatch = SpeakerLineRegex.Match(request);
        if (speakerMatch.Success)
            return NormalizeName(speakerMatch.Groups[1].Value, "Unknown traveler");
        var messageMatch = ExternalMessageSpeakerRegex.Match(request);
        return messageMatch.Success
            ? NormalizeName(messageMatch.Groups[1].Value, "Unknown traveler")
            : "Unknown traveler";
    }

    private static string ExtractUserMessage(BrainContext context, bool external)
    {
        var request = context.Request ?? string.Empty;
        if (!external)
            return NormalizeText(request);
        var match = MessageLineRegex.Match(request);
        return NormalizeText(match.Success ? match.Groups[1].Value : request);
    }

    private static string ChooseTopic(params string?[] values) =>
        values.Select(NormalizeText).FirstOrDefault(value => value.Length > 0) ?? "the current conversation";

    private static string ChooseText(params string?[] values) =>
        values.Select(NormalizeText).FirstOrDefault(value => value.Length > 0) ?? string.Empty;

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeName(string? value, string fallback)
    {
        value = NormalizeText(value);
        if (value.Length == 0)
            return fallback;
        value = Regex.Replace(value, @"[^A-Za-z0-9 _'\-]", string.Empty).Trim();
        return value.Length == 0 ? fallback : TrimTo(value, 80);
    }

    private static string NormalizeText(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string TrimTo(string? value, int maximum)
    {
        value = NormalizeText(value);
        return value.Length <= maximum
            ? value
            : value[..Math.Max(1, maximum - 1)].TrimEnd() + "…";
    }

    private sealed class ConversationRepairDocument
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; }
        public string LastSpeaker { get; set; } = "William";
        public List<ConversationRepairProfileState> Profiles { get; set; } = [];
    }

    private sealed class ConversationRepairProfileState
    {
        public string Speaker { get; set; } = string.Empty;
        public string LastCompletedTopic { get; set; } = string.Empty;
        public string LastCompletedUserMessage { get; set; } = string.Empty;
        public string LastCompletedBorinResponse { get; set; } = string.Empty;
        public string PausedTopic { get; set; } = string.Empty;
        public string PausedUserMessage { get; set; } = string.Empty;
        public string PausedBorinResponse { get; set; } = string.Empty;
        public DateTimeOffset? PausedAt { get; set; }
        public string LastListeningMode { get; set; } = string.Empty;
        public DateTimeOffset LastCompletedAt { get; set; }
        public int CompletedTurnCount { get; set; }
    }
}
