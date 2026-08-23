using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

/// <summary>
/// Model-independent conversation planning for Borin. The director does not
/// generate dialogue. It classifies the current social turn, narrows recalled
/// memories to those that genuinely fit, and gives the local GGUF brain a small
/// response plan so ordinary chat does not drift into repository-report mode.
/// </summary>
public sealed record BorinConversationDirection(
    string Mode,
    string Intent,
    string EmotionalNeed,
    string Continuity,
    string OpeningGuidance,
    string ResponseShape,
    bool MayAskFollowUp,
    int HistoryTurnLimit,
    IReadOnlyList<string> RelevantRecall,
    IReadOnlyList<string> Avoidances)
{
    public static BorinConversationDirection Empty { get; } = new(
        "direct response",
        "answer the current request",
        "respond calmly and naturally",
        "Use only the current request unless supplied continuity is clearly relevant.",
        "Open directly with substance rather than a ceremonial greeting.",
        "Give one complete answer and stop.",
        false,
        2,
        [],
        ["Do not invent memories or actions."]);

    public string Summary =>
        $"{Mode} • {Intent} • history {HistoryTurnLimit} turn(s) • recall {RelevantRecall.Count}";

    public string MemoryPrompt => RelevantRecall.Count == 0
        ? "No saved memory is relevant enough to mention in this reply."
        : string.Join("\n", RelevantRecall.Select(memory => $"- {memory}"));

    public string PromptText
    {
        get
        {
            var recall = MemoryPrompt;
            var avoid = Avoidances.Count == 0
                ? "- Avoid no additional mode-specific behavior."
                : string.Join("\n", Avoidances.Select(item => $"- {item}"));
            var questionRule = MayAskFollowUp
                ? "One brief follow-up question is allowed only if it naturally advances this exact thread."
                : "Do not append a follow-up question merely to keep the conversation going.";

            return $"""
Conversation director for this turn:
- Mode: {Mode}.
- User intent: {Intent}.
- Emotional need: {EmotionalNeed}.
- Continuity plan: {Continuity}
- Opening: {OpeningGuidance}
- Response shape: {ResponseShape}
- History budget: carry at most {HistoryTurnLimit} completed conversational turn(s).
- Question policy: {questionRule}

Memories relevant enough for this turn:
{recall}

Mode-specific avoidances:
{avoid}

Follow this plan invisibly. Never mention the director, mode, history budget, or memory-selection process.
""";
        }
    }
}

public interface IBorinConversationDirector
{
    BorinConversationDirection Direct(BrainContext context);
}

public sealed class BorinConversationDirector : IBorinConversationDirector
{
    private static readonly Regex WordRegex = new(
        @"[A-Za-z0-9][A-Za-z0-9'_-]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MessageLineRegex = new(
        @"(?im)^Message:\s*([^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "will", "your", "you", "are", "was", "were",
        "for", "but", "not", "can", "could", "would", "should", "into", "about", "what", "when", "where", "why",
        "how", "him", "his", "her", "our", "out", "now", "then", "than", "just", "like", "want", "need", "please",
        "borin", "speaker", "message", "current", "conversation", "majormud", "contract", "really", "okay", "yeah",
    };

    public BorinConversationDirection Direct(BrainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var external = context.Purpose.Contains(
            "External MajorMUD conversation",
            StringComparison.OrdinalIgnoreCase);
        var message = ExtractMessage(context.Request, external);
        var social = context.Conversation.SocialMemory;
        var repair = context.Conversation.ConversationRepair;
        var normalized = message.Trim();
        var lower = normalized.ToLowerInvariant();
        var shortFollowUp = IsShortFollowUp(normalized, social);
        var correction = repair.IsCorrection || LooksLikeCorrection(lower);
        var distress = ContainsAny(lower,
            "i'm worried", "i am worried", "i'm upset", "i am upset", "i'm angry", "i am angry", "frustrated",
            "sad", "hurt", "afraid", "scared", "rough day", "bad day", "hate this", "this sucks", "failed");
        var celebration = ContainsAny(lower,
            "great news", "good news", "we did it", "it worked", "success", "passed", "won", "victory", "awesome");
        var greeting = IsGreeting(lower);
        var statusCheck = IsPersonalStatusCheck(lower);
        var storyRequest = ContainsAny(lower,
            "tell me a story", "tell me about", "remember when", "what happened", "how did that happen",
            "your adventure", "your adventures", "what did you see", "what was it like");
        var opinionRequest = ContainsAny(lower,
            "what do you think", "what's your opinion", "do you like", "do you hate", "your favorite",
            "would you rather", "how do you feel about");
        var planning = ContainsAny(lower,
            "what's next", "what is next", "what should we do", "let's", "lets ", "we should", "our next",
            "make a plan", "the plan", "planning", "next step", "next move");
        var selfKnowledgeQuestion = context.Conversation.SelfKnowledge.SelfKnowledgeQuestion;
        var explanation = normalized.EndsWith('?') || ContainsAny(lower,
            "explain", "why ", "how ", "what is", "what's", "who is", "where is", "when is", "can you tell me");

        var mode = "casual exchange";
        var intent = "respond to the speaker's immediate meaning";
        var emotionalNeed = "steady companionship without overreacting";
        var continuity = BuildContinuity(social, shortFollowUp, false);
        var opening = "Open directly with a natural reaction or the answer; do not reintroduce yourself.";
        var shape = "Use two to five sentences in one or two short paragraphs.";
        var mayAsk = false;
        var historyTurns = external ? 2 : 3;
        var avoidances = new List<string>
        {
            "Do not turn ordinary chat into a capability report, repository audit, or numbered checklist.",
            "Do not repeat the user's wording as a summary before answering.",
        };

        if (repair.AbandonInterruptedThread)
        {
            mode = "drop abandoned thread";
            intent = "honor the speaker's decision to stop the prior subject and move on without reviving it";
            emotionalNeed = "respectful release without demanding an explanation";
            continuity = repair.ResumeInstruction;
            opening = "Acknowledge briefly only when acknowledgement helps, then leave the abandoned subject behind.";
            shape = "Use one or two sentences. Do not summarize, revisit, or ask about the dropped subject.";
            mayAsk = false;
            historyTurns = 1;
            avoidances.Add("Do not bring the abandoned thread back later unless the speaker introduces it again as a new subject.");
        }
        else if (correction)
        {
            mode = "repair and correction";
            intent = "accept the correction, identify the changed understanding, and continue from the corrected premise";
            emotionalNeed = "show that Borin listened rather than defending the earlier answer";
            continuity = $"{repair.RepairGoal} {repair.ResumeInstruction}";
            opening = "Acknowledge the correction in one plain sentence without groveling or making excuses.";
            shape = repair.ShouldAskClarifyingQuestion
                ? "Ask one short, concrete clarification question and stop. Do not add advice or another interpretation."
                : "State the corrected understanding, then answer or continue in two to five sentences.";
            mayAsk = repair.ShouldAskClarifyingQuestion;
            historyTurns = external ? 2 : 4;
            avoidances.Add("Do not insist the previous answer was technically reasonable.");
            avoidances.Add("Do not repeat the same claim after accepting the correction.");
        }
        else if (repair.IsPartialMessage)
        {
            mode = "listen to incomplete thought";
            intent = "let the speaker finish without inventing the missing end of the message";
            emotionalNeed = "patient attention rather than premature advice";
            continuity = repair.RepairGoal;
            opening = "Use a minimal acknowledgement that leaves room for the speaker to continue.";
            shape = "Use one short sentence such as an invitation to continue. Do not answer the unfinished idea yet.";
            mayAsk = true;
            historyTurns = 2;
            avoidances.Add("Do not complete the speaker's sentence, infer a conclusion, or launch into advice.");
        }
        else if (repair.IsResumeRequest)
        {
            mode = "resume interrupted thread";
            intent = "return to the speaker's paused or last completed topic and continue from the stopping point";
            emotionalNeed = "clear evidence that Borin retained the thread without restarting it";
            continuity = $"{repair.InterruptedThread} {repair.ResumeInstruction}";
            opening = repair.ShouldAskClarifyingQuestion
                ? "Ask which earlier subject the speaker wants to resume."
                : "Anchor the resumed subject in one short phrase, then continue from the last unfinished point.";
            shape = repair.ShouldAskClarifyingQuestion
                ? "Ask one brief clarification question and stop."
                : "Use two to five sentences and do not repeat the entire earlier answer.";
            mayAsk = repair.ShouldAskClarifyingQuestion;
            historyTurns = external ? 3 : 6;
            avoidances.Add("Do not pretend the resumed thread continued while the application or conversation was inactive.");
        }
        else if (repair.IsInterruption)
        {
            mode = "honor interruption";
            intent = "stop the prior line immediately and address the speaker's interruption before anything else";
            emotionalNeed = "evidence that Borin can yield the floor and listen";
            continuity = $"{repair.RepairGoal} {repair.ResumeInstruction}";
            opening = "Respond to the interruption's direct meaning without finishing the prior answer first.";
            shape = repair.ShouldResumeAfterCurrentTurn
                ? "Answer the interruption first, then return once to the parked thought in a separate short paragraph."
                : "Use one to four sentences and do not automatically resume the earlier subject.";
            mayAsk = repair.ShouldAskClarifyingQuestion;
            historyTurns = external ? 2 : 4;
            avoidances.Add("Do not talk over the speaker by completing the old response before acknowledging the interruption.");
        }
        else if (repair.IsTopicSwitch)
        {
            mode = "deliberate topic switch";
            intent = "follow the new subject directly while keeping the older thread parked";
            emotionalNeed = "flexibility without conversational whiplash or forced recap";
            continuity = repair.ResumeInstruction;
            opening = "Begin with the new subject. Do not announce that the topic changed unless that wording is naturally useful.";
            shape = "Use the response shape appropriate to the new request, without summarizing the parked topic.";
            mayAsk = false;
            historyTurns = external ? 2 : 3;
            avoidances.Add("Do not drag facts, promises, or questions from the parked thread into the new subject unless directly relevant.");
        }
        else if (selfKnowledgeQuestion)
        {
            mode = "honest self-assessment";
            intent = "answer the current capability, access, connection, or completed-action question from fresh verified state";
            emotionalNeed = "build trust through a plain answer without boasting, evasion, or false helplessness";
            continuity = "Use the current self-knowledge and verified game-state snapshots; older dialogue and memory cannot override them.";
            opening = "Start with the direct answer—yes, no, available, unavailable, or not verified—then explain the one distinction that matters.";
            shape = external
                ? "Use one to three brief sentences within the hard in-game character limit."
                : "Use one to four clear sentences. Distinguish the conversation brain, controlled services, configured capability, and completed action only when relevant.";
            mayAsk = false;
            historyTurns = 1;
            avoidances.RemoveAll(item => item.Contains("capability report", StringComparison.OrdinalIgnoreCase));
            avoidances.Add("Do not claim an edit, build, test, install, deployment, login, movement, command, or other action completed without explicit current evidence.");
            avoidances.Add("Do not ask the speaker to provide connection or game-state evidence already present in the verified Client snapshot.");
            avoidances.Add("Do not list every capability when a simple direct answer is sufficient.");
        }
        else if (distress)
        {
            mode = "companion support";
            intent = "respond to the person's distress before attempting to solve anything";
            emotionalNeed = "warmth, loyalty, and calm attention without pretending to be a therapist";
            continuity = BuildContinuity(social, shortFollowUp, false);
            opening = "Begin with a brief sincere acknowledgement of how the person seems to feel.";
            shape = "Offer grounded companionship first, then one useful thought or next step in three to six sentences.";
            mayAsk = ShouldAskSupportQuestion(normalized);
            historyTurns = external ? 2 : 5;
            avoidances.Add("Do not minimize the feeling, lecture, diagnose, or immediately produce a long solution list.");
        }
        else if (celebration)
        {
            mode = "shared celebration";
            intent = "share the success as a companion and recognize what made it meaningful";
            emotionalNeed = "visible pride and excitement without becoming theatrical";
            continuity = BuildContinuity(social, true, false);
            opening = "React with genuine delight before explaining anything.";
            shape = "Celebrate in two to four lively sentences and mention the relevant shared effort only when supported.";
            historyTurns = external ? 2 : 4;
            avoidances.Add("Do not flatten good news into a technical status report.");
        }
        else if (statusCheck)
        {
            mode = "personal check-in";
            intent = "answer as Borin about his present mood, thoughts, or concerns";
            emotionalNeed = "honest-seeming character presence and reciprocal interest";
            continuity = BuildContinuity(social, false, false);
            opening = "Answer the check-in directly with Borin's current mood; do not claim physical sensations or events not supplied.";
            shape = "Give two to four personal sentences, then one reciprocal question only when it feels natural.";
            mayAsk = !external;
            historyTurns = external ? 2 : 3;
            avoidances.Add("Do not answer with system health, model status, or architecture unless explicitly asked.");
        }
        else if (greeting)
        {
            mode = "greeting and reconnection";
            intent = "greet the speaker as someone Borin already knows at the appropriate familiarity";
            emotionalNeed = "recognition and welcome";
            continuity = social.InteractionCount > 1
                ? BuildGreetingContinuity(social)
                : "Begin a fresh social exchange without introducing Borin's biography.";
            opening = "Use a warm, varied greeting that fits the relationship; avoid the same catchphrase every time.";
            shape = "Use one to three sentences. A single natural question is allowed.";
            mayAsk = true;
            historyTurns = external ? 1 : 2;
            avoidances.Add("Do not list Borin's abilities, mission, or current software state in response to a greeting.");
        }
        else if (storyRequest)
        {
            mode = "storytelling";
            intent = "tell a coherent adventurer's account using only supplied or safely framed character material";
            emotionalNeed = "imagery, personality, and a clear beginning-to-end thread";
            continuity = BuildContinuity(social, shortFollowUp, true);
            opening = "Start with the moment or image that makes the story worth hearing, not with a disclaimer.";
            shape = "Tell a compact story in four to eight sentences with one concrete detail, one feeling, and one consequence.";
            historyTurns = external ? 2 : 5;
            avoidances.Add("Do not fabricate a shared historical event as fact. Frame invented color as imagination, legend, or what Borin would have done.");
            avoidances.Add("Do not turn the story into bullet points.");
        }
        else if (opinionRequest)
        {
            mode = "character opinion";
            intent = "give Borin's own reasoned preference rather than a neutral encyclopedia answer";
            emotionalNeed = "a recognizable viewpoint with room for disagreement";
            continuity = BuildContinuity(social, shortFollowUp, false);
            opening = "State the opinion early and confidently, then give the reason.";
            shape = "Use two to five sentences and distinguish personal preference from verified game fact.";
            mayAsk = false;
            historyTurns = external ? 2 : 3;
            avoidances.Add("Use a durable preference or opinion from the character-growth profile when one is supplied. Otherwise present the view as Borin's current judgment, not an invented lifelong belief.");
            avoidances.Add("Do not provide both sides so evenly that Borin has no discernible opinion.");
        }
        else if (planning)
        {
            mode = "companion planning";
            intent = "choose a sensible next direction together";
            emotionalNeed = "decisive partnership rather than passive agreement";
            continuity = BuildContinuity(social, true, false);
            opening = "Name the next priority immediately.";
            shape = "Give the next step and why it matters in three to six sentences; use a short list only when multiple concrete steps are truly needed.";
            mayAsk = HasMaterialAmbiguity(normalized);
            historyTurns = external ? 2 : 5;
            avoidances.Add("Do not offer every possible direction. Choose one best next move from the supplied context.");
        }
        else if (shortFollowUp)
        {
            mode = "thread follow-up";
            intent = "resolve the implied reference in the short message and continue the existing topic";
            emotionalNeed = "evidence that Borin remembers what was just being discussed";
            continuity = BuildContinuity(social, true, false);
            opening = "Answer the implied follow-up immediately; do not ask what the speaker means unless two interpretations remain equally plausible.";
            shape = "Use one to four sentences focused on the existing thread.";
            mayAsk = HasMaterialAmbiguity(normalized);
            historyTurns = external ? 2 : 6;
            avoidances.Add("Do not treat a one-word follow-up as a brand-new unrelated subject.");
            avoidances.Add("Do not restate the entire previous answer before adding the new information.");
        }
        else if (explanation)
        {
            mode = "natural explanation";
            intent = "answer the question clearly while retaining Borin's voice";
            emotionalNeed = "clarity without losing character presence";
            continuity = BuildContinuity(social, false, false);
            opening = "Give the answer or central idea in the first sentence.";
            shape = "Explain in three to seven sentences. Use bullets only when they make a genuinely multi-part answer easier to follow.";
            mayAsk = HasMaterialAmbiguity(normalized);
            historyTurns = external ? 2 : 4;
            avoidances.Add("Do not bury the answer beneath role-play flourishes or generic caveats.");
        }

        var initiative = context.Conversation.Initiative;
        var repairMode = mode is "drop abandoned thread" or "repair and correction" or "listen to incomplete thought" or
            "resume interrupted thread" or "honor interruption" or "deliberate topic switch";
        if (repairMode)
        {
            avoidances.Add("Do not add Borin's personal objective, proactive tangent, or unrelated follow-up while repairing the conversation.");
        }
        else if (mode == "companion planning")
        {
            continuity += $" Borin's current motivation objective is: {TrimTo(initiative.CurrentObjective, 240)} " +
                          $"His safe next-step boundary is: {TrimTo(initiative.NextSafeStep, 260)}";
            if (initiative.AwaitingPermission)
                avoidances.Add("The selected next step reaches a permission boundary. Propose it, but do not imply it has begun.");
        }
        else if (initiative.MayTakeConversationalInitiative &&
                 (mode is "personal check-in" or "greeting and reconnection" or "casual exchange" or "shared celebration"))
        {
            shape += " After addressing the speaker, Borin may add one relevant personal thought or purposeful question from his initiative profile.";
        }
        else if (!initiative.MayTakeConversationalInitiative &&
                 (mode is "personal check-in" or "greeting and reconnection" or "casual exchange"))
        {
            avoidances.Add("Do not add a proactive tangent merely to keep the conversation alive; attentive presence is enough this turn.");
        }

        var episodic = context.Conversation.EpisodicMemory;
        if (mode == "storytelling")
        {
            if (episodic.RelevantEpisodes.Count > 0)
            {
                var episodeTitles = string.Join(", ", episodic.RelevantEpisodes.Take(2).Select(item => item.Title));
                continuity += $" The trustworthy episodic store found this relevant material: {episodeTitles}. Use the supplied attribution boundaries rather than upgrading a reported event into witnessed fact.";
                shape += " Build the account around one supplied episode; use a second only when it is clearly part of the same thread.";
            }
            else
            {
                avoidances.Add("No trustworthy episode was retrieved. Do not invent a past Borin adventure as historical fact; clearly frame any creative tale as legend or imagination.");
            }
        }
        else if (episodic.RelevantEpisodes.Count > 0 &&
                 mode is "thread follow-up" or "shared celebration" or "companion support")
        {
            continuity += $" One relevant remembered episode is available: {TrimTo(episodic.RelevantEpisodes[0].Title, 120)}. Mention it only if it helps this exact reply and preserve its attribution boundary.";
        }

        var temporal = context.Conversation.TemporalContinuity;
        if (temporal.ExplicitTimeReference)
        {
            var clockContext = external
                ? $"The current local time-of-day category is {temporal.TimeOfDay}; the exact host clock and timezone remain private."
                : $"The current local machine time is {temporal.CurrentLocalTime}.";
            continuity += $" {clockContext} The completed-turn return state is: {temporal.ReturnStatus}. Resolve relative time words from that evidence only.";
            shape += " Answer time questions directly and distinguish recorded conversation time from MajorMUD world time or unrecorded off-screen activity.";
        }

        if (mode == "greeting and reconnection" && temporal.MayAcknowledgeReturn)
        {
            continuity += $" This is {temporal.ReturnStatus}. One brief, natural acknowledgement of the gap is allowed before continuing the remembered topic: {TrimTo(temporal.LastCompletedTopic, 160)}.";
            opening = "Greet the speaker warmly. You may acknowledge the elapsed time once, but do not sound like a timestamp report.";
        }
        else if (temporal.MeaningfulReturn)
        {
            avoidances.Add("Do not mention how long the speaker was away unless the message asks about time or naturally opens a reconnection.");
        }

        if (temporal.FirstCompletedConversation)
            avoidances.Add("No completed earlier exchange exists for this speaker. Do not say welcome back or imply a prior conversation.");

        avoidances.Add("Do not invent sleeping, drinking, travel, combat, work, observation, or completed tasks during application downtime.");
        avoidances.Add("A canceled, failed, empty, or no-reply turn is not shared conversational history.");
        avoidances.Add("The natural-listening repair plan outranks a generic desire to continue the previous topic.");

        if (external)
        {
            var group = context.Conversation.GroupConversation;
            if (!group.ShouldReply || group.ShouldYield)
            {
                mode = "yield the conversation floor";
                intent = "remain silent because this message is not Borin's conversational turn";
                emotionalNeed = "respect the current speaker and audience without inserting Borin into their exchange";
                continuity = group.ResponsePolicy;
                opening = "Do not open a spoken reply.";
                shape = "Return exactly [NO_REPLY].";
                mayAsk = false;
                historyTurns = 1;
                avoidances.Add("Do not answer a message directed to someone else, repeat another player's answer, or speak merely because Borin recognizes the topic.");
            }
            else
            {
                mayAsk &= mode is "thread follow-up" or "natural explanation" or "companion support" or
                    "repair and correction" or "listen to incomplete thought" or "resume interrupted thread" or "honor interruption";
                if (group.ShouldStayBrief)
                {
                    shape = "Use one to three concise sentences, answer only the direct point, and yield the public conversation floor.";
                    mayAsk = false;
                    avoidances.Add("Do not turn a busy room exchange into a speech, story, checklist, or extra question.");
                }
                if (group.LoopRisk)
                {
                    mayAsk = false;
                    shape += " Close the exchange naturally without inviting another automatic reply.";
                    avoidances.Add("Do not mirror, paraphrase, or prolong an AI exchange when no new information is present.");
                }
                if (!group.DirectlyAddressed)
                    avoidances.Add("This is only an allowed active-thread follow-up. Stay on that exact thread and do not widen the audience or topic.");
            }
            shape += " Stay within the external conversation contract's character limit and output rules.";
            avoidances.Add("Do not mention private project architecture, William's private conversations, or internal memory files to another player.");
        }

        var recalls = SelectRelevantMemories(context, message, mode, external);
        return new BorinConversationDirection(
            mode,
            intent,
            emotionalNeed,
            continuity,
            opening,
            shape,
            mayAsk,
            Math.Clamp(historyTurns, 1, 6),
            recalls,
            avoidances);
    }

    private static IReadOnlyList<string> SelectRelevantMemories(
        BrainContext context,
        string message,
        string mode,
        bool external)
    {
        if (external || context.Conversation.Memory.RelevantMemories.Count == 0)
            return [];

        var social = context.Conversation.SocialMemory;
        var socialTerms = string.Join(" ", social.KnownFacts.Concat(social.SharedExperiences));
        var queryTerms = Tokenize($"{message} {social.CurrentTopic} {social.PreviousTopic} {social.LastUserMessage} {socialTerms}");
        var socialMode = mode is "personal check-in" or "greeting and reconnection" or "companion support" or
            "shared celebration" or "storytelling" or "thread follow-up" or "character opinion";

        return context.Conversation.Memory.RelevantMemories
            .Select(memory => new
            {
                Memory = memory,
                Score = ScoreMemory(memory, queryTerms, socialMode),
            })
            .Where(item => item.Score >= 2.25)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.UpdatedAt)
            .Take(3)
            .Select(item =>
                $"[{item.Memory.Id}] [{item.Memory.Category}] {TrimTo(item.Memory.Text, 320)}")
            .ToArray();
    }

    private static double ScoreMemory(
        BorinMemoryEntry memory,
        IReadOnlySet<string> queryTerms,
        bool socialMode)
    {
        var memoryTerms = Tokenize(memory.Text);
        if (memoryTerms.Count == 0 || queryTerms.Count == 0)
            return 0;

        var overlap = memoryTerms.Intersect(queryTerms, StringComparer.OrdinalIgnoreCase).Count();
        var score = overlap * 2.0;
        if (memory.Category is BorinMemoryCategory.Conversation or BorinMemoryCategory.ConversationSummary)
            score += socialMode ? 1.2 : 0.2;
        else if (socialMode && overlap == 0)
            score -= 2.0;

        if (memory.Pinned && overlap > 0)
            score += 0.8;
        if (memory.Evidence == BorinMemoryEvidence.Validated && overlap > 0)
            score += 0.4;
        if (memory.Text.Length > 900)
            score -= 0.4;
        return score;
    }

    private static string BuildContinuity(
        BorinSocialMemorySnapshot social,
        bool forceContinuation,
        bool storytelling)
    {
        if (social.ThreadTurnCount <= 0 || string.IsNullOrWhiteSpace(social.LastUserMessage))
            return "Treat this as the beginning of the current thread while preserving the existing relationship.";

        var priorReply = string.IsNullOrWhiteSpace(social.LastBorinResponse)
            ? "No usable prior Borin reply to this speaker was saved."
            : $"Borin's prior reply to {social.Speaker} was: {TrimTo(social.LastBorinResponse, 360)}";
        var instruction = forceContinuation
            ? "Resolve pronouns, fragments, and implied references against this speaker's isolated thread before considering a new topic."
            : "Use this speaker's earlier thread only when it clearly helps; do not drag an old topic into a genuinely new request.";
        if (storytelling)
            instruction += " Preserve established shared experiences and never invent a missing event.";

        var obligations = BuildOpenSocialObligations(social);
        return $"Continue {social.Speaker}'s thread about {social.CurrentTopic}. The speaker last said: {TrimTo(social.LastUserMessage, 280)}. {priorReply} {obligations} {instruction}";
    }

    private static string BuildGreetingContinuity(
        BorinSocialMemorySnapshot social)
    {
        var pending = BuildOpenSocialObligations(social);
        return $"Recognize {social.Speaker} as an existing acquaintance. Their last known topic was {social.CurrentTopic}. " +
               $"{pending} Do not force the old topic, promise, or question into the greeting unless it naturally fits what they say now.";
    }

    private static string BuildOpenSocialObligations(BorinSocialMemorySnapshot social)
    {
        var parts = new List<string>();
        if (social.OpenCommitments.Count > 0)
            parts.Add($"There is an unfinished commitment: {TrimTo(social.OpenCommitments[^1], 260)}");
        if (social.OpenQuestions.Count > 0)
            parts.Add($"There is an unresolved question: {TrimTo(social.OpenQuestions[^1], 260)}");
        return parts.Count == 0
            ? "No unfinished social obligation needs attention."
            : string.Join(" ", parts);
    }

    private static string ExtractMessage(string request, bool external)
    {
        request ??= string.Empty;
        if (!external)
            return request.Trim();

        var match = MessageLineRegex.Match(request);
        return match.Success ? match.Groups[1].Value.Trim() : request.Trim();
    }

    private static bool IsShortFollowUp(string message, BorinSocialMemorySnapshot social)
    {
        if (social.ThreadTurnCount <= 0 || string.IsNullOrWhiteSpace(message) || message.Length > 140)
            return false;
        if (message.EndsWith('?') && Tokenize(message).Count <= 8)
            return true;
        if (Tokenize(message).Count <= 5)
            return true;
        return ContainsAny(message.ToLowerInvariant(),
            "what about", "and then", "after that", "why not", "how so", "you did", "you were", "that one", "those");
    }

    private static bool LooksLikeCorrection(string lower) =>
        lower.StartsWith("no,") || lower.StartsWith("no ") || lower.StartsWith("actually") ||
        lower.StartsWith("that's not") || lower.StartsWith("that is not") || lower.StartsWith("wrong") ||
        lower.StartsWith("you misunderstood") || lower.StartsWith("i meant") ||
        ContainsAny(lower, "not what i said", "not what i meant", "you got that wrong", "don't call", "do not call");

    private static bool IsPersonalStatusCheck(string lower)
    {
        var trimmed = lower.Trim(' ', '.', '!', '?');
        return trimmed is "how are you" or "how're you" or "how do you feel" or "what are you thinking" or
            "what's on your mind" or "what are you doing" or "are you okay" or "you alright" or
            "how are you today" or "how are you doing" or "how have you been";
    }

    private static bool IsGreeting(string lower)
    {
        var trimmed = lower.Trim(' ', '.', '!', '?');
        return trimmed is "hi" or "hello" or "hey" or "morning" or "good morning" or "good afternoon" or
            "good evening" or "evening" or "howdy" or "greetings" or "hey borin" or "hello borin";
    }

    private static bool ShouldAskSupportQuestion(string message) =>
        message.Length < 220 && !message.EndsWith('?');

    private static bool HasMaterialAmbiguity(string message) =>
        message.Length < 42 && Tokenize(message).Count <= 6;

    private static HashSet<string> Tokenize(string value) =>
        WordRegex.Matches(value ?? string.Empty)
            .Cast<Match>()
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => term.Length >= 3 && !StopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string TrimTo(string value, int maximum)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length <= maximum
            ? value
            : value[..Math.Max(1, maximum - 1)].TrimEnd() + "…";
    }
}
