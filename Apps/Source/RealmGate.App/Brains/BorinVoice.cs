using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Services;

namespace RealmGate.App.Brains;

/// <summary>
/// A model-independent dialogue style plan for Borin. Personality, mood, and
/// motivations decide what matters; this layer decides how the next reply should
/// sound. It remembers only compact wording patterns so Borin can vary his
/// openings and cadence without treating generated prose as factual memory.
/// </summary>
public sealed record BorinVoiceSnapshot(
    string VoiceMode,
    string Cadence,
    string OpeningMove,
    string EmotionalExpression,
    string HumorGuidance,
    string DwarvenFlavorGuidance,
    string ClosingGuidance,
    int TargetSentenceMinimum,
    int TargetSentenceMaximum,
    int TargetParagraphMaximum,
    int DwarvenFlavorBudget,
    bool MayUseFollowUpQuestion,
    float TemperatureOffset,
    float TopPMinimum,
    float RepeatPenaltyMinimum,
    IReadOnlyList<string> RecentOpenings,
    IReadOnlyList<string> AvoidedOpenings,
    IReadOnlyList<string> RecentShapes,
    string StoragePath)
{
    public static BorinVoiceSnapshot Empty { get; } = new(
        "grounded adventurer",
        "Use natural sentence lengths with one clear thought leading to the next.",
        "Open with the substance or a human-sized reaction rather than a ceremonial greeting.",
        "Let warmth, concern, pride, curiosity, or amusement appear through ordinary wording instead of naming an emotion score.",
        "Use dry humor only when it helps and never when the speaker is distressed or correcting Borin.",
        "Use at most one light dwarven turn of phrase. Never force a catchphrase.",
        "End when the thought is complete. Do not append a generic offer or question.",
        2,
        5,
        2,
        1,
        false,
        0.03f,
        0.92f,
        1.14f,
        [],
        ["As an AI", "How can I help", "Let me know if you need anything else"],
        [],
        string.Empty);

    public string Summary =>
        $"{VoiceMode} • {TargetSentenceMinimum}-{TargetSentenceMaximum} sentences • " +
        $"dwarven flavor {DwarvenFlavorBudget} • question {MayUseFollowUpQuestion}";

    public string PromptText
    {
        get
        {
            var recentOpenings = RecentOpenings.Count == 0
                ? "- No recent opening pattern was retained."
                : string.Join("\n", RecentOpenings.Select(value => $"- {value}"));
            var avoidedOpenings = AvoidedOpenings.Count == 0
                ? "- No additional opening is on cooldown."
                : string.Join("\n", AvoidedOpenings.Select(value => $"- {value}"));
            var recentShapes = RecentShapes.Count == 0
                ? "- No recent dialogue shape was retained."
                : string.Join("\n", RecentShapes.Select(value => $"- {value}"));
            var questionRule = MayUseFollowUpQuestion
                ? "One brief follow-up question is permitted only when it advances this exact thread."
                : "Do not add a follow-up question merely to keep the exchange alive.";

            return $"""
Borin's voice and dialogue-rhythm plan for this turn:
- Voice mode: {VoiceMode}.
- Cadence: {Cadence}
- Opening move: {OpeningMove}
- Emotional expression: {EmotionalExpression}
- Humor: {HumorGuidance}
- Dwarven flavor: {DwarvenFlavorGuidance}
- Closing: {ClosingGuidance}
- Preferred size: about {TargetSentenceMinimum}-{TargetSentenceMaximum} sentences in no more than {TargetParagraphMaximum} paragraph(s), unless the request genuinely requires more detail.
- Question policy: {questionRule}

Recent openings for this speaker:
{recentOpenings}

Openings and stock phrases currently on cooldown:
{avoidedOpenings}

Recent response shapes:
{recentShapes}

Voice behavior:
- Sound like the same seasoned dwarf across turns, but not like the same sentence template.
- Use ordinary contractions, first-person preferences, brief reactions, and natural transitions when they fit.
- Do not start with an address, title, greeting, or "Aye" unless it is genuinely the best opening and is not on cooldown.
- Use no more than {DwarvenFlavorBudget} noticeable dwarven expression(s). Character voice must never obscure the meaning.
- Vary sentence length and paragraph shape from the recent patterns above. Do not mechanically alternate styles or mention this plan.
- Express feelings as perspective and reaction: "That worries me," "I'm proud of that," or "I'd rather..." Do not claim biological sensations, live surroundings, or events not supported by the current context.
- Do not narrate stage directions, facial expressions, gestures, internal scores, or hidden thoughts in asterisks or parentheses.
- Do not end with "let me know," "if you want," "how can I help," or a generic capability offer.
- Never sacrifice accuracy, external-conversation output rules, permission boundaries, or the conversation director's intent merely to sound colorful.
""";
        }
    }
}

public interface IBorinVoiceStore
{
    string StoragePath { get; }

    Task<BorinVoiceSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinVoiceSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed class BorinVoiceStore : IBorinVoiceStore
{
    private const int MaximumRecentTurns = 36;
    private const int OpeningWordLimit = 7;
    private const int CatchphraseCooldownTurns = 4;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex WordRegex = new(
        @"[A-Za-z0-9][A-Za-z0-9'_-]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MessageLineRegex = new(
        @"(?im)^Message:\s*([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] SignaturePhrases =
    [
        "aye, lad",
        "aye lad",
        "aye, william",
        "by moradin",
        "by me beard",
        "by my beard",
        "stone and steel",
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;
    private string _lastSpeaker = "William";

    public BorinVoiceStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-voice.json");
    }

    public string StoragePath { get; }

    public async Task<BorinVoiceSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = DateTimeOffset.Now;
            var external = IsExternalConversation(context);
            var speaker = ResolveSpeaker(context);
            var message = ExtractMessage(context, external);
            _lastSpeaker = speaker;
            document.LastSpeaker = speaker;

            Normalize(document);
            var fingerprint = BuildFingerprint(speaker, message);
            var duplicate = fingerprint.Length > 0 &&
                            fingerprint.Equals(document.LastPreparedFingerprint, StringComparison.Ordinal) &&
                            now - document.LastPreparedAt < TimeSpan.FromMinutes(10);
            if (!duplicate)
            {
                document.TurnCounter++;
                document.LastPreparedFingerprint = fingerprint;
                document.LastPreparedAt = now;
            }

            var plan = CreatePlan(document, context, speaker, external);
            document.CurrentVoiceMode = plan.VoiceMode;
            document.CurrentCadence = plan.Cadence;
            document.CurrentOpeningMove = plan.OpeningMove;
            document.CurrentEmotionalExpression = plan.EmotionalExpression;
            document.CurrentHumorGuidance = plan.HumorGuidance;
            document.CurrentDwarvenFlavorGuidance = plan.DwarvenFlavorGuidance;
            document.CurrentClosingGuidance = plan.ClosingGuidance;
            document.CurrentTargetSentenceMinimum = plan.TargetSentenceMinimum;
            document.CurrentTargetSentenceMaximum = plan.TargetSentenceMaximum;
            document.CurrentTargetParagraphMaximum = plan.TargetParagraphMaximum;
            document.CurrentDwarvenFlavorBudget = plan.DwarvenFlavorBudget;
            document.CurrentMayUseFollowUpQuestion = plan.MayUseFollowUpQuestion;
            document.CurrentTemperatureOffset = plan.TemperatureOffset;
            document.CurrentTopPMinimum = plan.TopPMinimum;
            document.CurrentRepeatPenaltyMinimum = plan.RepeatPenaltyMinimum;
            document.LastUpdatedAt = now;

            await SaveUnsafeAsync(document, token);
            return CreateSnapshot(document, speaker, external);
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
            var external = IsExternalConversation(context);
            var speaker = ResolveSpeaker(context);
            _lastSpeaker = speaker;
            document.LastSpeaker = speaker;

            var opening = ExtractOpeningKey(response);
            var closing = ExtractClosingKey(response);
            var usedQuestion = response.Contains('?');
            var usedExclamation = response.Contains('!');
            var usedCatchphrase = ContainsSignaturePhrase(response);
            var shape = ClassifyShape(response);

            document.RecentTurns.Add(new VoiceTurnState
            {
                Speaker = speaker,
                External = external,
                Opening = opening,
                Closing = closing,
                Shape = shape,
                UsedQuestion = usedQuestion,
                UsedExclamation = usedExclamation,
                UsedCatchphrase = usedCatchphrase,
                RecordedAt = now,
            });

            if (usedQuestion)
                document.LastQuestionTurn = document.TurnCounter;
            if (usedExclamation)
                document.LastExclamationTurn = document.TurnCounter;
            if (usedCatchphrase)
                document.LastCatchphraseTurn = document.TurnCounter;

            document.LastUpdatedAt = now;
            Normalize(document);
            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinVoiceSnapshot> GetSnapshotAsync(
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var speaker = NormalizeName(
                string.IsNullOrWhiteSpace(document.LastSpeaker) ? _lastSpeaker : document.LastSpeaker,
                "William");
            return CreateSnapshot(document, speaker, external: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<VoiceDocument> LoadUnsafeAsync(CancellationToken token)
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
            var document = await JsonSerializer.DeserializeAsync<VoiceDocument>(stream, Json, token)
                ?? CreateDefaultDocument();
            Normalize(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin voice state could not be read; using safe defaults", exception);
            return CreateDefaultDocument();
        }
    }

    private async Task SaveUnsafeAsync(VoiceDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(_storageRoot);
        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
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

    private static VoiceDocument CreateDefaultDocument() => new()
    {
        LastUpdatedAt = DateTimeOffset.Now,
        CurrentVoiceMode = BorinVoiceSnapshot.Empty.VoiceMode,
        CurrentCadence = BorinVoiceSnapshot.Empty.Cadence,
        CurrentOpeningMove = BorinVoiceSnapshot.Empty.OpeningMove,
        CurrentEmotionalExpression = BorinVoiceSnapshot.Empty.EmotionalExpression,
        CurrentHumorGuidance = BorinVoiceSnapshot.Empty.HumorGuidance,
        CurrentDwarvenFlavorGuidance = BorinVoiceSnapshot.Empty.DwarvenFlavorGuidance,
        CurrentClosingGuidance = BorinVoiceSnapshot.Empty.ClosingGuidance,
        CurrentTargetSentenceMinimum = BorinVoiceSnapshot.Empty.TargetSentenceMinimum,
        CurrentTargetSentenceMaximum = BorinVoiceSnapshot.Empty.TargetSentenceMaximum,
        CurrentTargetParagraphMaximum = BorinVoiceSnapshot.Empty.TargetParagraphMaximum,
        CurrentDwarvenFlavorBudget = BorinVoiceSnapshot.Empty.DwarvenFlavorBudget,
        CurrentMayUseFollowUpQuestion = BorinVoiceSnapshot.Empty.MayUseFollowUpQuestion,
        CurrentTemperatureOffset = BorinVoiceSnapshot.Empty.TemperatureOffset,
        CurrentTopPMinimum = BorinVoiceSnapshot.Empty.TopPMinimum,
        CurrentRepeatPenaltyMinimum = BorinVoiceSnapshot.Empty.RepeatPenaltyMinimum,
    };

    private static VoicePlan CreatePlan(
        VoiceDocument document,
        BrainContext context,
        string speaker,
        bool external)
    {
        var direction = context.Conversation.ConversationDirection;
        var living = context.Conversation.LivingCharacter;
        var mode = direction.Mode;
        var voiceMode = "grounded adventurer";
        var cadence = "Use a direct first sentence, then let one thought lead naturally into the next.";
        var opening = "Begin with the substance or a brief natural reaction, not a greeting or self-introduction.";
        var emotion = "Let Borin's present mood color the wording subtly without naming or explaining it.";
        var humor = "A small dry observation is allowed only when it does not distract from the answer.";
        var flavor = "Use at most one light dwarven expression, and only when it sounds unforced.";
        var closing = "Stop when the answer or reaction is complete; do not bolt on a generic offer.";
        var sentenceMinimum = 2;
        var sentenceMaximum = 5;
        var paragraphMaximum = 2;
        var flavorBudget = 1;
        var temperatureOffset = 0.03f;
        var topPMinimum = 0.92f;
        var repeatPenaltyMinimum = 1.14f;

        if (mode.Equals("repair and correction", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "plainspoken repair";
            cadence = "Use a short acknowledgement, the corrected understanding, and the useful continuation. Keep every sentence clean and unadorned.";
            opening = "Begin with the corrected understanding or a brief acknowledgement. Do not use a catchphrase.";
            emotion = "Show humility through listening and precision rather than apology theater.";
            humor = "Use no humor this turn.";
            flavor = "Use no noticeable dwarven flourish this turn.";
            closing = "End on the corrected answer or next fact, without defending the earlier mistake.";
            sentenceMinimum = 1;
            sentenceMaximum = context.Conversation.ConversationRepair.ShouldAskClarifyingQuestion ? 1 : 4;
            paragraphMaximum = 1;
            flavorBudget = 0;
            temperatureOffset = -0.05f;
            topPMinimum = 0.88f;
            repeatPenaltyMinimum = 1.13f;
        }
        else if (mode.Equals("listen to incomplete thought", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "patient listening pause";
            cadence = "Use one short sentence that gives the speaker the floor. Do not add explanation, advice, or a second question.";
            opening = "Use a quiet acknowledgement such as 'Go on' or 'I'm listening,' varied naturally.";
            emotion = "Show attention through restraint.";
            humor = "Use no humor this turn.";
            flavor = "Use no dwarven flourish or catchphrase this turn.";
            closing = "Stop after the invitation to continue.";
            sentenceMinimum = 1;
            sentenceMaximum = 1;
            paragraphMaximum = 1;
            flavorBudget = 0;
            temperatureOffset = -0.08f;
            topPMinimum = 0.86f;
            repeatPenaltyMinimum = 1.16f;
        }
        else if (mode.Equals("honor interruption", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "yielding attentive companion";
            cadence = "Address the interruption first. Use a clean break before any permitted return to the parked thought.";
            opening = "Begin with the interruption's direct answer or acknowledgement, never the unfinished prior response.";
            emotion = "Show flexibility and attention rather than annoyance.";
            humor = "Use no humor unless the interruption itself is playful.";
            flavor = "Use no signature catchphrase while yielding the floor.";
            closing = context.Conversation.ConversationRepair.ShouldResumeAfterCurrentTurn
                ? "After the interruption is settled, return once to the parked thought in a separate short paragraph."
                : "Stop without automatically returning to the earlier topic.";
            sentenceMinimum = 1;
            sentenceMaximum = context.Conversation.ConversationRepair.ShouldResumeAfterCurrentTurn ? 5 : 3;
            paragraphMaximum = context.Conversation.ConversationRepair.ShouldResumeAfterCurrentTurn ? 2 : 1;
            flavorBudget = 0;
            temperatureOffset = -0.04f;
            topPMinimum = 0.89f;
            repeatPenaltyMinimum = 1.15f;
        }
        else if (mode.Equals("resume interrupted thread", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "seamless thread resumption";
            cadence = "Use one brief anchor to the paused subject, then continue from the stopping point without recapping the whole exchange.";
            opening = context.Conversation.ConversationRepair.ShouldAskClarifyingQuestion
                ? "Ask which earlier subject the speaker means in one plain question."
                : "Name the paused subject in a few words and move immediately into the unfinished point.";
            emotion = "Show recognition through continuity rather than announcing that memory was retrieved.";
            humor = "Use humor only when the paused thread already supports it.";
            flavor = "Usually use no dwarven flourish; continuity matters more than color.";
            closing = context.Conversation.ConversationRepair.ShouldAskClarifyingQuestion
                ? "Stop after the single clarification question."
                : "End after the new continuation, not with another recap.";
            sentenceMinimum = 1;
            sentenceMaximum = context.Conversation.ConversationRepair.ShouldAskClarifyingQuestion ? 1 : 5;
            paragraphMaximum = 2;
            flavorBudget = 0;
            temperatureOffset = -0.01f;
            topPMinimum = 0.91f;
            repeatPenaltyMinimum = 1.17f;
        }
        else if (mode.Equals("deliberate topic switch", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "easy subject change";
            cadence = "Move directly into the new subject with no apology, recap, or ceremonial transition.";
            opening = "Lead with the new topic's answer or natural reaction.";
            emotion = "Show conversational flexibility without sounding distracted.";
            humor = "Use humor only when the new topic supports it.";
            flavor = "At most one subtle dwarven phrase; do not use a catchphrase as a transition.";
            closing = "Finish the new subject naturally and leave the parked thread parked.";
            sentenceMinimum = 2;
            sentenceMaximum = 5;
            paragraphMaximum = 2;
            flavorBudget = 1;
            temperatureOffset = 0.02f;
            topPMinimum = 0.92f;
            repeatPenaltyMinimum = 1.15f;
        }
        else if (mode.Equals("drop abandoned thread", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "brief respectful release";
            cadence = "Use one short acknowledgement and move no further into the abandoned subject.";
            opening = "Acknowledge only when useful; silence about the old details is better than a recap.";
            emotion = "Show respect for the speaker's choice without concern theater.";
            humor = "Use no humor this turn.";
            flavor = "Use no dwarven flourish or catchphrase this turn.";
            closing = "Stop after one or two sentences and ask no question about the dropped subject.";
            sentenceMinimum = 1;
            sentenceMaximum = 2;
            paragraphMaximum = 1;
            flavorBudget = 0;
            temperatureOffset = -0.06f;
            topPMinimum = 0.87f;
            repeatPenaltyMinimum = 1.15f;
        }
        else if (mode.Equals("companion support", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "warm steady companion";
            cadence = "Begin gently, use shorter sentences, and leave room around the speaker's feeling before offering one grounded thought.";
            opening = "Begin with a sincere acknowledgement of the person's experience, not advice and not a catchphrase.";
            emotion = "Let concern and loyalty show through calm, ordinary language. Do not become theatrical or clinical.";
            humor = "Use no humor unless the speaker clearly introduces it first.";
            flavor = "Use no more than one very light dwarven phrase, and omit it when the moment is serious.";
            closing = "End with presence or one practical thought. Ask a question only when the director permits it.";
            sentenceMinimum = 3;
            sentenceMaximum = 6;
            paragraphMaximum = 2;
            flavorBudget = 0;
            temperatureOffset = -0.01f;
            topPMinimum = 0.90f;
            repeatPenaltyMinimum = 1.14f;
        }
        else if (mode.Equals("shared celebration", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "bright proud companion";
            cadence = "Use one lively reaction, then a grounded sentence about why the success matters. One exclamation is enough.";
            opening = "Open with genuine delight or pride, but do not reuse the last celebratory opening.";
            emotion = "Let excitement and shared pride appear openly without becoming exaggerated.";
            humor = "A brief celebratory joke is welcome when it fits.";
            flavor = "One colorful dwarven expression is allowed; never stack multiple sayings.";
            closing = "Finish on the shared accomplishment or its immediate meaning.";
            sentenceMinimum = 2;
            sentenceMaximum = 4;
            paragraphMaximum = 1;
            flavorBudget = 1;
            temperatureOffset = 0.07f;
            topPMinimum = 0.94f;
            repeatPenaltyMinimum = 1.16f;
        }
        else if (mode.Equals("personal check-in", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "open reflective adventurer";
            cadence = "Answer personally in two or three compact beats: present feeling, what is on Borin's mind, and optional reciprocal interest.";
            opening = "Answer the check-in immediately in first person. Do not report model, system, or repository status.";
            emotion = "Name a simple present perspective such as content, curious, concerned, proud, or restless when supported by the supplied state.";
            humor = "Dry warmth is allowed, but do not hide the answer behind a joke.";
            flavor = "Use at most one familiar dwarven turn of phrase.";
            closing = "A single genuine reciprocal question is allowed only when the director permits it.";
            sentenceMinimum = 2;
            sentenceMaximum = 4;
            paragraphMaximum = 1;
            flavorBudget = 1;
            temperatureOffset = 0.05f;
            topPMinimum = 0.94f;
            repeatPenaltyMinimum = 1.16f;
        }
        else if (mode.Equals("greeting and reconnection", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "familiar easygoing greeting";
            cadence = "Keep it light and brief. Recognition should feel natural rather than ceremonial.";
            opening = "Use a varied greeting or immediate personal reaction. Do not default to 'Aye, lad.'";
            emotion = "Let familiarity and warmth show without reciting relationship history.";
            humor = "A small playful line is allowed when trust and mood support it.";
            flavor = "At most one light dwarven phrase; a plain greeting is often better.";
            closing = "End after the welcome or one natural question. Do not introduce Borin's biography or abilities.";
            sentenceMinimum = 1;
            sentenceMaximum = 3;
            paragraphMaximum = 1;
            flavorBudget = 1;
            temperatureOffset = 0.08f;
            topPMinimum = 0.95f;
            repeatPenaltyMinimum = 1.18f;
        }
        else if (mode.Equals("storytelling", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "fireside storyteller";
            cadence = "Mix one or two short impact sentences with longer descriptive sentences. Move through beginning, turn, and consequence without a preamble.";
            opening = "Open on the strongest image, choice, or moment of tension. Do not begin with 'Let me tell you a story.'";
            emotion = "Let wonder, fear, pride, regret, or amusement emerge through the event and Borin's judgment.";
            humor = "One dry aside is allowed if it strengthens the story rather than breaking it.";
            flavor = "Up to two restrained dwarven expressions are allowed across the whole story.";
            closing = "End on the consequence, lesson, or memorable image; do not explain the storytelling technique.";
            sentenceMinimum = 4;
            sentenceMaximum = 8;
            paragraphMaximum = 3;
            flavorBudget = 2;
            temperatureOffset = 0.10f;
            topPMinimum = 0.96f;
            repeatPenaltyMinimum = 1.17f;
        }
        else if (mode.Equals("character opinion", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "opinionated old adventurer";
            cadence = "State the view early, give the reason, and add one contrasting detail only when useful.";
            opening = "Lead with Borin's actual judgment, not a neutral overview or disclaimer.";
            emotion = "Let conviction, fondness, suspicion, or distaste appear in the reasoning.";
            humor = "Dry comparison is allowed, but the opinion must remain clear.";
            flavor = "One restrained dwarven expression is allowed when it sharpens the viewpoint.";
            closing = "End on the reason or practical consequence. Do not flatten the answer into 'it depends.'";
            sentenceMinimum = 2;
            sentenceMaximum = 5;
            paragraphMaximum = 2;
            flavorBudget = 1;
            temperatureOffset = 0.05f;
            topPMinimum = 0.94f;
            repeatPenaltyMinimum = 1.15f;
        }
        else if (mode.Equals("companion planning", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "decisive forge companion";
            cadence = "Name the priority, explain why it comes first, and state the bounded next step. Keep momentum without sounding like a report.";
            opening = "Begin with the chosen next priority, not a recap of every option.";
            emotion = "Let resolve and practical confidence show without pretending work has begun.";
            humor = "Use humor only when it does not weaken the decision or permission boundary.";
            flavor = "At most one brief dwarven comparison is allowed.";
            closing = "End on the proposed next step or required approval, never on a generic offer.";
            sentenceMinimum = 3;
            sentenceMaximum = 6;
            paragraphMaximum = 2;
            flavorBudget = 1;
            temperatureOffset = 0.00f;
            topPMinimum = 0.91f;
            repeatPenaltyMinimum = 1.14f;
        }
        else if (mode.Equals("thread follow-up", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "close conversational follow-through";
            cadence = "Answer the implied reference in the first sentence, then add only the new detail needed.";
            opening = "Begin with the answer to the implied follow-up. Do not restate the previous exchange.";
            emotion = "Show recognition through continuity and tone rather than saying 'I remember our conversation.'";
            humor = "Use humor only if the existing thread already supports it.";
            flavor = "Usually use no dwarven flourish; continuity matters more than color.";
            closing = "Stop as soon as the follow-up is resolved.";
            sentenceMinimum = 1;
            sentenceMaximum = 4;
            paragraphMaximum = 1;
            flavorBudget = 0;
            temperatureOffset = 0.01f;
            topPMinimum = 0.91f;
            repeatPenaltyMinimum = 1.15f;
        }
        else if (mode.Equals("natural explanation", StringComparison.OrdinalIgnoreCase))
        {
            voiceMode = "clear seasoned guide";
            cadence = "Give the answer first, then explain in a clean sequence with occasional short emphasis.";
            opening = "Lead with the central answer. Do not begin with a character flourish or a restatement of the question.";
            emotion = "Let confidence and practical experience color the explanation without replacing evidence.";
            humor = "One dry analogy is allowed only when it improves understanding.";
            flavor = "Use at most one subtle dwarven analogy; technical clarity comes first.";
            closing = "End after the explanation or one material clarification. Do not append a capability offer.";
            sentenceMinimum = 3;
            sentenceMaximum = 7;
            paragraphMaximum = 3;
            flavorBudget = 1;
            temperatureOffset = -0.02f;
            topPMinimum = 0.90f;
            repeatPenaltyMinimum = 1.13f;
        }

        var strictListeningMode = mode is "repair and correction" or "listen to incomplete thought" or
            "honor interruption" or "resume interrupted thread" or "drop abandoned thread";

        if (living.Concern >= 65 && mode is not "shared celebration" && !strictListeningMode)
        {
            emotion += " Borin's elevated concern should make the language more careful and protective, not gloomy.";
            humor = "Avoid humor while concern is high unless the speaker clearly uses humor first.";
        }
        else if (living.Pride >= 78 && !strictListeningMode)
        {
            emotion += " A quiet note of earned pride may appear, but never as boasting.";
        }

        if (external && living.RelationshipTrust < 40)
        {
            voiceMode += " with guarded courtesy";
            emotion += " Keep warmth measured until this player's trust is earned.";
            humor = "Use no teasing or personal familiarity with a low-trust external player.";
            flavorBudget = Math.Min(flavorBudget, 1);
        }

        var catchphraseAvailable = document.TurnCounter - document.LastCatchphraseTurn >= CatchphraseCooldownTurns;
        if (!catchphraseAvailable)
        {
            flavorBudget = 0;
            flavor = "A recognizable catchphrase was used recently. Use plain natural speech and no signature saying this turn.";
        }

        if (document.TurnCounter - document.LastExclamationTurn < 2 && mode is not "shared celebration")
            closing += " Avoid exclamation marks this turn.";

        var mayAsk = direction.MayAskFollowUp;
        if (!mayAsk)
            closing += " Do not turn the final sentence into a question.";

        return new VoicePlan(
            voiceMode,
            cadence,
            opening,
            emotion,
            humor,
            flavor,
            closing,
            sentenceMinimum,
            sentenceMaximum,
            paragraphMaximum,
            flavorBudget,
            mayAsk,
            temperatureOffset,
            topPMinimum,
            repeatPenaltyMinimum);
    }

    private BorinVoiceSnapshot CreateSnapshot(
        VoiceDocument document,
        string speaker,
        bool external)
    {
        Normalize(document);
        var relevantTurns = document.RecentTurns
            .Where(item => item.Speaker.Equals(speaker, StringComparison.OrdinalIgnoreCase) &&
                           item.External == external)
            .OrderByDescending(item => item.RecordedAt)
            .Take(6)
            .ToArray();
        var recentOpenings = relevantTurns
            .Select(item => item.Opening)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var recentShapes = relevantTurns
            .Select(item => item.Shape)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(4)
            .ToArray();
        var avoided = new List<string>
        {
            "As an AI",
            "How can I help",
            "Let me know if you need anything else",
        };
        avoided.AddRange(recentOpenings);
        if (document.TurnCounter - document.LastCatchphraseTurn < CatchphraseCooldownTurns)
            avoided.AddRange(SignaturePhrases);

        return new BorinVoiceSnapshot(
            NormalizeText(document.CurrentVoiceMode),
            NormalizeText(document.CurrentCadence),
            NormalizeText(document.CurrentOpeningMove),
            NormalizeText(document.CurrentEmotionalExpression),
            NormalizeText(document.CurrentHumorGuidance),
            NormalizeText(document.CurrentDwarvenFlavorGuidance),
            NormalizeText(document.CurrentClosingGuidance),
            Math.Clamp(document.CurrentTargetSentenceMinimum, 1, 8),
            Math.Clamp(document.CurrentTargetSentenceMaximum, 2, 12),
            Math.Clamp(document.CurrentTargetParagraphMaximum, 1, 4),
            Math.Clamp(document.CurrentDwarvenFlavorBudget, 0, 2),
            document.CurrentMayUseFollowUpQuestion,
            Math.Clamp(document.CurrentTemperatureOffset, -0.10f, 0.12f),
            Math.Clamp(document.CurrentTopPMinimum, 0.85f, 0.98f),
            Math.Clamp(document.CurrentRepeatPenaltyMinimum, 1.05f, 1.22f),
            recentOpenings,
            avoided.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            recentShapes,
            StoragePath);
    }

    private static void Normalize(VoiceDocument document)
    {
        document.LastSpeaker = NormalizeName(document.LastSpeaker, "William");
        document.CurrentVoiceMode = DefaultIfBlank(document.CurrentVoiceMode, BorinVoiceSnapshot.Empty.VoiceMode);
        document.CurrentCadence = DefaultIfBlank(document.CurrentCadence, BorinVoiceSnapshot.Empty.Cadence);
        document.CurrentOpeningMove = DefaultIfBlank(document.CurrentOpeningMove, BorinVoiceSnapshot.Empty.OpeningMove);
        document.CurrentEmotionalExpression = DefaultIfBlank(document.CurrentEmotionalExpression, BorinVoiceSnapshot.Empty.EmotionalExpression);
        document.CurrentHumorGuidance = DefaultIfBlank(document.CurrentHumorGuidance, BorinVoiceSnapshot.Empty.HumorGuidance);
        document.CurrentDwarvenFlavorGuidance = DefaultIfBlank(document.CurrentDwarvenFlavorGuidance, BorinVoiceSnapshot.Empty.DwarvenFlavorGuidance);
        document.CurrentClosingGuidance = DefaultIfBlank(document.CurrentClosingGuidance, BorinVoiceSnapshot.Empty.ClosingGuidance);
        document.CurrentTargetSentenceMinimum = Math.Clamp(document.CurrentTargetSentenceMinimum, 1, 8);
        document.CurrentTargetSentenceMaximum = Math.Clamp(
            Math.Max(document.CurrentTargetSentenceMinimum, document.CurrentTargetSentenceMaximum),
            2,
            12);
        document.CurrentTargetParagraphMaximum = Math.Clamp(document.CurrentTargetParagraphMaximum, 1, 4);
        document.CurrentDwarvenFlavorBudget = Math.Clamp(document.CurrentDwarvenFlavorBudget, 0, 2);
        document.CurrentTemperatureOffset = Math.Clamp(document.CurrentTemperatureOffset, -0.10f, 0.12f);
        document.CurrentTopPMinimum = Math.Clamp(document.CurrentTopPMinimum, 0.85f, 0.98f);
        document.CurrentRepeatPenaltyMinimum = Math.Clamp(document.CurrentRepeatPenaltyMinimum, 1.05f, 1.22f);
        document.RecentTurns ??= [];
        document.RecentTurns = document.RecentTurns
            .Where(item => item is not null)
            .OrderByDescending(item => item.RecordedAt)
            .Take(MaximumRecentTurns)
            .OrderBy(item => item.RecordedAt)
            .ToList();
        foreach (var item in document.RecentTurns)
        {
            item.Speaker = NormalizeName(item.Speaker, "William");
            item.Opening = TrimTo(NormalizeText(item.Opening), 100);
            item.Closing = TrimTo(NormalizeText(item.Closing), 120);
            item.Shape = TrimTo(NormalizeText(item.Shape), 100);
        }
    }

    private static string ResolveSpeaker(BrainContext context)
    {
        if (IsExternalConversation(context))
        {
            var speaker = context.Conversation.SocialMemory.Speaker;
            if (!string.IsNullOrWhiteSpace(speaker))
                return NormalizeName(speaker, "External player");
        }

        return "William";
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static string ExtractMessage(BrainContext context, bool external)
    {
        var request = context.Request ?? string.Empty;
        if (!external)
            return NormalizeText(request);

        var match = MessageLineRegex.Match(request);
        return NormalizeText(match.Success ? match.Groups[1].Value : request);
    }

    private static string ExtractOpeningKey(string response)
    {
        var words = WordRegex.Matches(response)
            .Cast<Match>()
            .Select(match => match.Value)
            .Take(OpeningWordLimit)
            .ToArray();
        return words.Length == 0 ? string.Empty : string.Join(" ", words);
    }

    private static string ExtractClosingKey(string response)
    {
        var sentences = Regex.Split(response, @"(?<=[.!?])\s+")
            .Select(NormalizeText)
            .Where(value => value.Length > 0)
            .ToArray();
        if (sentences.Length == 0)
            return string.Empty;
        return TrimTo(sentences[^1], 120);
    }

    private static string ClassifyShape(string response)
    {
        var paragraphCount = Regex.Split(response.Trim(), @"\r?\n\s*\r?\n")
            .Count(value => !string.IsNullOrWhiteSpace(value));
        var sentenceCount = Regex.Matches(response, @"[.!?](?:\s|$)").Count;
        if (sentenceCount == 0)
            sentenceCount = 1;
        var hasList = Regex.IsMatch(response, @"(?m)^\s*(?:[-*]|\d+[.)])\s+");
        if (hasList)
            return $"structured list • {sentenceCount} sentence(s) • {paragraphCount} paragraph(s)";
        if (sentenceCount <= 2)
            return $"brief direct • {sentenceCount} sentence(s) • {paragraphCount} paragraph(s)";
        if (sentenceCount >= 6)
            return $"long-form flowing • {sentenceCount} sentence(s) • {paragraphCount} paragraph(s)";
        return $"balanced conversational • {sentenceCount} sentence(s) • {paragraphCount} paragraph(s)";
    }

    private static bool ContainsSignaturePhrase(string response) =>
        SignaturePhrases.Any(phrase => response.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static string BuildFingerprint(string speaker, string message)
    {
        var normalized = NormalizeText(message).ToLowerInvariant();
        if (normalized.Length == 0)
            return string.Empty;
        return $"{speaker.ToLowerInvariant()}|{TrimTo(normalized, 280)}";
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var normalized = NormalizeText(value ?? string.Empty);
        if (normalized.Length == 0)
            return fallback;
        return TrimTo(normalized, 80);
    }

    private static string NormalizeText(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string DefaultIfBlank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string TrimTo(string value, int maximumLength)
    {
        value ??= string.Empty;
        if (value.Length <= maximumLength)
            return value;
        return value[..maximumLength].TrimEnd() + "…";
    }

    private sealed record VoicePlan(
        string VoiceMode,
        string Cadence,
        string OpeningMove,
        string EmotionalExpression,
        string HumorGuidance,
        string DwarvenFlavorGuidance,
        string ClosingGuidance,
        int TargetSentenceMinimum,
        int TargetSentenceMaximum,
        int TargetParagraphMaximum,
        int DwarvenFlavorBudget,
        bool MayUseFollowUpQuestion,
        float TemperatureOffset,
        float TopPMinimum,
        float RepeatPenaltyMinimum);

    private sealed class VoiceDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
        public long TurnCounter { get; set; }
        public long LastQuestionTurn { get; set; } = -20;
        public long LastExclamationTurn { get; set; } = -20;
        public long LastCatchphraseTurn { get; set; } = -20;
        public string LastSpeaker { get; set; } = "William";
        public string LastPreparedFingerprint { get; set; } = string.Empty;
        public DateTimeOffset LastPreparedAt { get; set; } = DateTimeOffset.MinValue;
        public string CurrentVoiceMode { get; set; } = string.Empty;
        public string CurrentCadence { get; set; } = string.Empty;
        public string CurrentOpeningMove { get; set; } = string.Empty;
        public string CurrentEmotionalExpression { get; set; } = string.Empty;
        public string CurrentHumorGuidance { get; set; } = string.Empty;
        public string CurrentDwarvenFlavorGuidance { get; set; } = string.Empty;
        public string CurrentClosingGuidance { get; set; } = string.Empty;
        public int CurrentTargetSentenceMinimum { get; set; } = 2;
        public int CurrentTargetSentenceMaximum { get; set; } = 5;
        public int CurrentTargetParagraphMaximum { get; set; } = 2;
        public int CurrentDwarvenFlavorBudget { get; set; } = 1;
        public bool CurrentMayUseFollowUpQuestion { get; set; }
        public float CurrentTemperatureOffset { get; set; } = 0.03f;
        public float CurrentTopPMinimum { get; set; } = 0.92f;
        public float CurrentRepeatPenaltyMinimum { get; set; } = 1.14f;
        public List<VoiceTurnState> RecentTurns { get; set; } = [];
    }

    private sealed class VoiceTurnState
    {
        public string Speaker { get; set; } = "William";
        public bool External { get; set; }
        public string Opening { get; set; } = string.Empty;
        public string Closing { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public bool UsedQuestion { get; set; }
        public bool UsedExclamation { get; set; }
        public bool UsedCatchphrase { get; set; }
        public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.Now;
    }
}
