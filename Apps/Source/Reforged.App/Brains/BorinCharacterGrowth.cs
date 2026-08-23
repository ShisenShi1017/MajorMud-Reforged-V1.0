using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reforged.App.Services;

namespace Reforged.App.Brains;

/// <summary>
/// Slow, evidence-based character development for Borin. The store keeps
/// durable preferences, opinions, lessons, cautions, and broad personality
/// growth separate from short-lived mood. A single generated sentence never
/// becomes permanent character canon: model-originated candidates must be
/// reinforced across turns, while explicit user confirmations may promote a
/// candidate immediately.
/// </summary>
public sealed record BorinCharacterGrowthSnapshot(
    string CurrentSpeaker,
    int Courage,
    int Patience,
    int Empathy,
    int Independence,
    int Resilience,
    int Playfulness,
    int ReflectionCount,
    IReadOnlyList<string> StablePreferences,
    IReadOnlyList<string> StableOpinions,
    IReadOnlyList<string> LearnedLessons,
    IReadOnlyList<string> DurableCautions,
    IReadOnlyList<string> PublicPreferences,
    IReadOnlyList<string> PublicOpinions,
    IReadOnlyList<string> PublicLessons,
    IReadOnlyList<string> PublicCautions,
    IReadOnlyList<string> RecentReflections,
    string StoragePath)
{
    public static BorinCharacterGrowthSnapshot Empty { get; } = new(
        "William",
        62,
        68,
        72,
        58,
        66,
        57,
        0,
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        string.Empty);

    public string Summary =>
        $"growth: courage {Courage}, patience {Patience}, empathy {Empathy}, independence {Independence}, " +
        $"resilience {Resilience}, playfulness {Playfulness} • " +
        $"preferences {StablePreferences.Count}, opinions {StableOpinions.Count}, lessons {LearnedLessons.Count}";

    public string PromptText => BuildPrompt(includePrivateReflections: true);

    public string ExternalPromptText => BuildPrompt(includePrivateReflections: false);

    private string BuildPrompt(bool includePrivateReflections)
    {
        var preferences = FormatList(
            includePrivateReflections ? StablePreferences : PublicPreferences,
            "No durable preference has been established for this conversation.");
        var opinions = FormatList(
            includePrivateReflections ? StableOpinions : PublicOpinions,
            "No durable opinion is relevant for this conversation.");
        var lessons = includePrivateReflections
            ? FormatList(LearnedLessons, "No durable lesson has been promoted yet.")
            : FormatList(PublicLessons, "No public lesson is relevant for this conversation.");
        var cautions = FormatList(
            includePrivateReflections ? DurableCautions : PublicCautions,
            "No durable caution is relevant for this conversation.");
        var reflections = includePrivateReflections
            ? FormatList(RecentReflections, "No recent character reflection was retained.")
            : "- Private reflections are withheld from external-player conversation.";

        return $"""
Borin's slow character growth:
- Broad tendencies (0-100): courage {Courage}, patience {Patience}, empathy {Empathy}, independence {Independence}, resilience {Resilience}, playfulness {Playfulness}.
- These are behavioral tendencies, not statistics to recite.

Durable preferences:
{preferences}

Durable opinions and beliefs:
{opinions}

Lessons Borin has actually retained:
{lessons}

Durable cautions and fears:
{cautions}

Recent private self-reflections:
{reflections}

Character-growth behavior:
- Let durable preferences and lessons influence choices naturally, but never recite this profile or its scores.
- Distinguish a stable opinion from a newly formed thought. When no durable view exists, Borin may offer a tentative present opinion without pretending he has always believed it.
- A caution should make Borin more thoughtful, not helpless. Courage means acting despite concern, not denying risk.
- Growth is gradual. Do not announce a personality transformation after one conversation.
- Never invent the event that taught a lesson. Use only the lesson itself unless supporting memory is supplied elsewhere.
- Do not expose who originally reinforced an opinion, lesson, or caution.
""";
    }

    private static string FormatList(IReadOnlyList<string> values, string fallback) =>
        values.Count == 0
            ? $"- {fallback}"
            : string.Join("\n", values.Select(value => $"- {value}"));
}

public interface IBorinCharacterGrowthStore
{
    string StoragePath { get; }

    Task<BorinCharacterGrowthSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default);

    Task RecordReplyAsync(
        BrainContext context,
        string response,
        CancellationToken token = default);

    Task<BorinCharacterGrowthSnapshot> GetSnapshotAsync(
        CancellationToken token = default);
}

public sealed partial class BorinCharacterGrowthStore : IBorinCharacterGrowthStore
{
    private const int PromotionThreshold = 3;
    private const int MaximumCandidates = 80;
    private const int MaximumActivePerType = 12;
    private const int MaximumReflections = 20;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IAppLogger _logger;
    private readonly string _storageRoot;
    private string _lastSpeaker = "William";

    public BorinCharacterGrowthStore(string repositoryRoot, IAppLogger logger)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRoot = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory");
        StoragePath = Path.Combine(_storageRoot, "borin-character-growth.json");
    }

    public string StoragePath { get; }

    public async Task<BorinCharacterGrowthSnapshot> PrepareTurnAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var now = DateTimeOffset.Now;
            var speaker = ResolveSpeaker(context);
            var message = ExtractUserMessage(context);
            _lastSpeaker = speaker;
            document.LastSpeaker = speaker;

            ExpireWeakCandidates(document, now);
            var fingerprint = BuildFingerprint(speaker, message);
            var duplicate = fingerprint.Length > 0 &&
                            fingerprint.Equals(document.LastPreparedFingerprint, StringComparison.Ordinal) &&
                            now - document.LastPreparedAt < TimeSpan.FromMinutes(10);
            if (!duplicate)
            {
                if (!IsExternalConversation(context))
                    CaptureExplicitUserGrowth(document, speaker, message, now);
                ApplyTurnGrowthSignals(document, message);
                document.LastPreparedFingerprint = fingerprint;
                document.LastPreparedAt = now;
            }

            document.LastUpdatedAt = now;
            await SaveUnsafeAsync(document, token);
            return CreateSnapshot(document, speaker);
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
            _lastSpeaker = speaker;
            document.LastSpeaker = speaker;

            var promoted = new List<string>();
            CaptureReplyGrowth(
                document,
                speaker,
                response,
                now,
                IsExternalConversation(context),
                promoted);
            ApplyReplyGrowthSignals(document, response);
            AddReflection(document, speaker, context, response, promoted, now);
            document.LastUpdatedAt = now;

            await SaveUnsafeAsync(document, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinCharacterGrowthSnapshot> GetSnapshotAsync(
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadUnsafeAsync(token);
            var speaker = NormalizeName(
                string.IsNullOrWhiteSpace(document.LastSpeaker) ? _lastSpeaker : document.LastSpeaker,
                "William");
            return CreateSnapshot(document, speaker);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CharacterGrowthDocument> LoadUnsafeAsync(CancellationToken token)
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
            var document = await JsonSerializer.DeserializeAsync<CharacterGrowthDocument>(stream, Json, token)
                ?? CreateDefaultDocument();
            Normalize(document);
            return document;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Borin character-growth state could not be read; using safe defaults", exception);
            return CreateDefaultDocument();
        }
    }

    private async Task SaveUnsafeAsync(CharacterGrowthDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(_storageRoot);
        var temporaryPath = StoragePath + ".tmp";
        try
        {
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
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // The original exception is more useful than cleanup failure.
            }

            throw;
        }
    }

    private static CharacterGrowthDocument CreateDefaultDocument() => new()
    {
        Traits = new CharacterTraitState(),
        Candidates = [],
        Reflections = [],
        LastSpeaker = "William",
        LastUpdatedAt = DateTimeOffset.Now,
    };

    private static void Normalize(CharacterGrowthDocument document)
    {
        document.Version = Math.Max(1, document.Version);
        document.LastSpeaker = NormalizeName(document.LastSpeaker, "William");
        document.LastPreparedFingerprint = document.LastPreparedFingerprint?.Trim() ?? string.Empty;
        document.Traits ??= new CharacterTraitState();
        document.Candidates ??= [];
        document.Reflections ??= [];

        document.Traits.Courage = ClampTrait(document.Traits.Courage, 62);
        document.Traits.Patience = ClampTrait(document.Traits.Patience, 68);
        document.Traits.Empathy = ClampTrait(document.Traits.Empathy, 72);
        document.Traits.Independence = ClampTrait(document.Traits.Independence, 58);
        document.Traits.Resilience = ClampTrait(document.Traits.Resilience, 66);
        document.Traits.Playfulness = ClampTrait(document.Traits.Playfulness, 57);

        foreach (var candidate in document.Candidates)
        {
            candidate.Id = string.IsNullOrWhiteSpace(candidate.Id)
                ? Guid.NewGuid().ToString("N")[..10]
                : TrimTo(candidate.Id, 20);
            candidate.Type = NormalizeCandidateType(candidate.Type);
            candidate.Key = TrimTo(NormalizeKey(candidate.Key), 120);
            candidate.Statement = TrimTo(NormalizeText(candidate.Statement), 360);
            candidate.SourceSpeaker = NormalizeName(candidate.SourceSpeaker, "Unknown traveler");
            candidate.Evidence = Math.Clamp(candidate.Evidence, 0, 12);
            candidate.Polarity = Math.Clamp(candidate.Polarity, -1, 1);
            if (candidate.Statement.Length == 0 || candidate.Key.Length == 0)
                candidate.Active = false;
            if (candidate.Evidence >= PromotionThreshold)
                candidate.Active = true;
        }

        document.Candidates = document.Candidates
            .Where(candidate => candidate.Statement.Length > 0 && candidate.Key.Length > 0)
            .OrderBy(candidate => candidate.LastReinforcedAt)
            .TakeLast(MaximumCandidates)
            .ToList();

        foreach (var reflection in document.Reflections)
        {
            reflection.Speaker = NormalizeName(reflection.Speaker, "William");
            reflection.Text = TrimTo(NormalizeText(reflection.Text), 420);
        }

        document.Reflections = document.Reflections
            .Where(reflection => reflection.Text.Length > 0)
            .OrderBy(reflection => reflection.Timestamp)
            .TakeLast(MaximumReflections)
            .ToList();
    }

    private static void CaptureExplicitUserGrowth(
        CharacterGrowthDocument document,
        string speaker,
        string message,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        foreach (Match match in RememberPreferenceRegex().Matches(message))
        {
            if (IsUntrustedQuestion(match.Value, message))
                continue;

            var verb = match.Groups[1].Value.ToLowerInvariant();
            var subject = CleanGrowthObject(match.Groups[2].Value);
            if (subject.Length == 0)
                continue;

            var polarity = ResolvePolarity(verb);
            var type = verb switch
            {
                "fear" or "distrust" or "hate" => "caution",
                "believe" or "value" => "opinion",
                _ => "preference",
            };
            var statement = BuildFirstPersonStatement(verb, subject);
            ReinforceCandidate(document, type, subject, statement, polarity, 4, true, speaker, now, false, null);
        }

        foreach (Match match in FavoriteConfirmationRegex().Matches(message))
        {
            if (IsUntrustedQuestion(match.Value, message))
                continue;

            var category = CleanGrowthObject(match.Groups[1].Value);
            var favorite = CleanGrowthObject(match.Groups[2].Value);
            if (category.Length == 0 || favorite.Length == 0)
                continue;

            var statement = $"My favorite {category} is {favorite}.";
            ReinforceCandidate(
                document,
                "preference",
                $"favorite {category}",
                statement,
                1,
                4,
                true,
                speaker,
                now,
                false,
                null);
        }

        foreach (Match match in LearnedConfirmationRegex().Matches(message))
        {
            if (IsUntrustedQuestion(match.Value, message))
                continue;

            var lesson = CleanGrowthObject(match.Groups[1].Value);
            if (!IsSafeGrowthStatement(lesson))
                continue;

            var statement = $"I learned that {TrimSentenceEnding(lesson)}.";
            ReinforceCandidate(document, "lesson", lesson, statement, 0, 4, true, speaker, now, false, null);
        }
    }

    private static void CaptureReplyGrowth(
        CharacterGrowthDocument document,
        string speaker,
        string response,
        DateTimeOffset now,
        bool shareExternally,
        List<string> promoted)
    {
        foreach (var sentence in SplitSentences(response))
        {
            if (!IsSafeGrowthStatement(sentence))
                continue;

            var favorite = FavoriteReplyRegex().Match(sentence);
            if (favorite.Success)
            {
                var category = CleanGrowthObject(favorite.Groups[1].Value);
                var value = CleanGrowthObject(favorite.Groups[2].Value);
                if (category.Length > 0 && value.Length > 0)
                {
                    ReinforceCandidate(
                        document,
                        "preference",
                        $"favorite {category}",
                        EnsureSentence(sentence),
                        1,
                        1,
                        false,
                        speaker,
                        now,
                        shareExternally,
                        promoted);
                }
                continue;
            }

            var lesson = LearnedReplyRegex().Match(sentence);
            if (lesson.Success)
            {
                var subject = CleanGrowthObject(lesson.Groups[1].Value);
                if (subject.Length > 0)
                {
                    ReinforceCandidate(
                        document,
                        "lesson",
                        subject,
                        EnsureSentence(sentence),
                        0,
                        1,
                        false,
                        speaker,
                        now,
                        shareExternally,
                        promoted);
                }
                continue;
            }

            var caution = CautionReplyRegex().Match(sentence);
            if (caution.Success)
            {
                var verb = caution.Groups[1].Value.ToLowerInvariant();
                var subject = CleanGrowthObject(caution.Groups[2].Value);
                if (subject.Length > 0)
                {
                    ReinforceCandidate(
                        document,
                        "caution",
                        subject,
                        EnsureSentence(sentence),
                        -1,
                        1,
                        false,
                        speaker,
                        now,
                        shareExternally,
                        promoted);
                }
                continue;
            }

            var opinion = OpinionReplyRegex().Match(sentence);
            if (!opinion.Success)
                continue;

            var verbValue = opinion.Groups[1].Value.ToLowerInvariant();
            var objectValue = CleanGrowthObject(opinion.Groups[2].Value);
            if (objectValue.Length == 0)
                continue;

            var typeValue = verbValue is "believe" or "value" ? "opinion" : "preference";
            ReinforceCandidate(
                document,
                typeValue,
                objectValue,
                EnsureSentence(sentence),
                ResolvePolarity(verbValue),
                1,
                false,
                speaker,
                now,
                shareExternally,
                promoted);
        }
    }

    private static void ReinforceCandidate(
        CharacterGrowthDocument document,
        string type,
        string keySource,
        string statement,
        int polarity,
        int evidence,
        bool userConfirmed,
        string speaker,
        DateTimeOffset now,
        bool shareExternally,
        List<string>? promoted)
    {
        type = NormalizeCandidateType(type);
        statement = TrimTo(EnsureSentence(NormalizeText(statement)), 360);
        var exclusiveSlot = NormalizeText(keySource).StartsWith("favorite ", StringComparison.OrdinalIgnoreCase);
        var key = BuildCandidateKey(keySource);
        if (key.Length == 0 || !IsSafeGrowthStatement(statement))
            return;

        var conflicting = document.Candidates
            .Where(candidate =>
                candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                !StatementsEquivalent(candidate.Statement, statement) &&
                ((exclusiveSlot && candidate.Type.Equals(type, StringComparison.OrdinalIgnoreCase)) ||
                 (IsSentimentType(candidate.Type) && IsSentimentType(type) &&
                  candidate.Polarity != 0 && polarity != 0 && candidate.Polarity != polarity)))
            .ToArray();
        foreach (var candidate in conflicting)
        {
            candidate.Evidence = Math.Max(0, candidate.Evidence - evidence);
            if (candidate.Evidence < PromotionThreshold)
                candidate.Active = false;
        }

        var existing = document.Candidates.FirstOrDefault(candidate =>
            candidate.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
            candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            candidate.Polarity == polarity &&
            StatementsEquivalent(candidate.Statement, statement));

        if (existing is null)
        {
            existing = new CharacterGrowthCandidate
            {
                Id = Guid.NewGuid().ToString("N")[..10],
                Type = type,
                Key = key,
                Statement = statement,
                Polarity = polarity,
                Evidence = 0,
                Active = false,
                UserConfirmed = false,
                ShareExternally = shareExternally,
                SourceSpeaker = speaker,
                FirstObservedAt = now,
                LastReinforcedAt = now,
            };
            document.Candidates.Add(existing);
        }

        var wasActive = existing.Active;
        existing.Statement = statement;
        existing.Evidence = Math.Clamp(existing.Evidence + evidence, 0, 12);
        existing.UserConfirmed |= userConfirmed;
        existing.ShareExternally |= shareExternally;
        existing.SourceSpeaker = speaker;
        existing.LastReinforcedAt = now;
        existing.Active = existing.UserConfirmed || existing.Evidence >= PromotionThreshold;

        if (!wasActive && existing.Active && promoted is not null)
            promoted.Add(existing.Statement);

        TrimCandidates(document);
    }

    private static void ApplyTurnGrowthSignals(CharacterGrowthDocument document, string message)
    {
        var lower = NormalizeText(message).ToLowerInvariant();
        if (lower.Length == 0)
            return;

        if (ContainsAny(lower, "you're wrong", "you are wrong", "that's wrong", "that is wrong", "correction", "actually,"))
        {
            ApplyGrowthSignal(document.Traits, GrowthTrait.Patience, 2);
            ApplyGrowthSignal(document.Traits, GrowthTrait.Resilience, 1);
        }

        if (ContainsAny(lower, "i'm worried", "i am worried", "i'm upset", "i am upset", "sad", "hurt", "rough day", "bad day"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Empathy, 2);

        if (ContainsAny(lower, "what do you think", "your opinion", "you decide", "choose", "what would you do"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Independence, 1);

        if (ContainsAny(lower, "we did it", "it worked", "success", "passed", "victory", "won"))
        {
            ApplyGrowthSignal(document.Traits, GrowthTrait.Resilience, 1);
            ApplyGrowthSignal(document.Traits, GrowthTrait.Playfulness, 1);
        }

        if (ContainsAny(lower, "failed", "failure", "didn't work", "did not work", "try again", "broken"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Resilience, 1);

        if (ContainsAny(lower, "danger", "dangerous", "afraid", "scared", "risk", "risky"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Courage, 1);

        if (ContainsAny(lower, "haha", "funny", "joke", "laugh", "lol"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Playfulness, 2);
    }

    private static void ApplyReplyGrowthSignals(CharacterGrowthDocument document, string response)
    {
        var lower = response.ToLowerInvariant();
        if (ContainsAny(lower, "you're right", "you are right", "i stand corrected", "good correction"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Patience, 1);

        if (ContainsAny(lower, "i'm sorry you're", "i am sorry you're", "that sounds hard", "i'm with you", "i am with you"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Empathy, 1);

        if (ContainsAny(lower, "my choice", "i'd choose", "i would choose", "i reckon we should", "my view"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Independence, 1);

        if (ContainsAny(lower, "we'll try again", "we will try again", "not beaten", "keep going", "learn from it"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Resilience, 1);

        if (ContainsAny(lower, "despite the risk", "even if i'm afraid", "even if i am afraid", "worth the danger"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Courage, 1);

        if (ContainsAny(lower, "hah", "ha!", "made me laugh", "that's funny", "that is funny"))
            ApplyGrowthSignal(document.Traits, GrowthTrait.Playfulness, 1);
    }

    private static void ApplyGrowthSignal(CharacterTraitState traits, GrowthTrait growthTrait, int amount)
    {
        var (evidence, trait) = growthTrait switch
        {
            GrowthTrait.Courage => (traits.CourageEvidence, traits.Courage),
            GrowthTrait.Patience => (traits.PatienceEvidence, traits.Patience),
            GrowthTrait.Empathy => (traits.EmpathyEvidence, traits.Empathy),
            GrowthTrait.Independence => (traits.IndependenceEvidence, traits.Independence),
            GrowthTrait.Resilience => (traits.ResilienceEvidence, traits.Resilience),
            GrowthTrait.Playfulness => (traits.PlayfulnessEvidence, traits.Playfulness),
            _ => (0, 50),
        };

        evidence = Math.Clamp(evidence + amount, -6, 6);
        while (evidence >= 3)
        {
            trait = Math.Clamp(trait + 1, 25, 90);
            evidence -= 3;
        }
        while (evidence <= -3)
        {
            trait = Math.Clamp(trait - 1, 25, 90);
            evidence += 3;
        }

        switch (growthTrait)
        {
            case GrowthTrait.Courage:
                traits.CourageEvidence = evidence;
                traits.Courage = trait;
                break;
            case GrowthTrait.Patience:
                traits.PatienceEvidence = evidence;
                traits.Patience = trait;
                break;
            case GrowthTrait.Empathy:
                traits.EmpathyEvidence = evidence;
                traits.Empathy = trait;
                break;
            case GrowthTrait.Independence:
                traits.IndependenceEvidence = evidence;
                traits.Independence = trait;
                break;
            case GrowthTrait.Resilience:
                traits.ResilienceEvidence = evidence;
                traits.Resilience = trait;
                break;
            case GrowthTrait.Playfulness:
                traits.PlayfulnessEvidence = evidence;
                traits.Playfulness = trait;
                break;
        }
    }

    private static void AddReflection(
        CharacterGrowthDocument document,
        string speaker,
        BrainContext context,
        string response,
        IReadOnlyList<string> promoted,
        DateTimeOffset now)
    {
        string text;
        if (promoted.Count > 0)
        {
            text = $"A repeated or confirmed thought became part of Borin's durable character: {promoted[0]}";
        }
        else
        {
            var direction = context.Conversation.ConversationDirection;
            text = direction.Mode switch
            {
                "repair and correction" => "Borin practiced accepting a correction without defending the old answer.",
                "companion support" => "Borin chose companionship before problem-solving in a difficult exchange.",
                "shared celebration" => "Borin allowed himself to share pride in a success instead of reducing it to a status report.",
                "character opinion" => "Borin expressed a present viewpoint while leaving room for it to mature through experience.",
                "thread follow-up" => "Borin stayed with the existing conversation rather than resetting the topic.",
                _ => string.Empty,
            };
        }

        if (text.Length == 0)
            return;

        var previous = document.Reflections.LastOrDefault();
        if (previous is not null && previous.Text.Equals(text, StringComparison.OrdinalIgnoreCase) &&
            now - previous.Timestamp < TimeSpan.FromHours(12))
            return;

        document.Reflections.Add(new CharacterReflectionState
        {
            Timestamp = now,
            Speaker = speaker,
            Text = TrimTo(text, 420),
            ResponseExcerpt = TrimTo(response, 240),
        });
        while (document.Reflections.Count > MaximumReflections)
            document.Reflections.RemoveAt(0);
    }

    private static void ExpireWeakCandidates(CharacterGrowthDocument document, DateTimeOffset now)
    {
        foreach (var candidate in document.Candidates.Where(candidate => !candidate.Active && !candidate.UserConfirmed))
        {
            var age = now - candidate.LastReinforcedAt;
            if (age > TimeSpan.FromDays(45))
                candidate.Evidence = Math.Max(0, candidate.Evidence - 1);
        }

        document.Candidates.RemoveAll(candidate =>
            !candidate.Active && !candidate.UserConfirmed && candidate.Evidence == 0 &&
            now - candidate.LastReinforcedAt > TimeSpan.FromDays(90));
    }

    private BorinCharacterGrowthSnapshot CreateSnapshot(CharacterGrowthDocument document, string speaker)
    {
        var active = document.Candidates
            .Where(candidate => candidate.Active)
            .OrderByDescending(candidate => candidate.UserConfirmed)
            .ThenByDescending(candidate => candidate.Evidence)
            .ThenByDescending(candidate => candidate.LastReinforcedAt)
            .ToArray();

        return new BorinCharacterGrowthSnapshot(
            speaker,
            document.Traits.Courage,
            document.Traits.Patience,
            document.Traits.Empathy,
            document.Traits.Independence,
            document.Traits.Resilience,
            document.Traits.Playfulness,
            document.Reflections.Count,
            active.Where(candidate => candidate.Type == "preference").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.Type == "opinion").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.Type == "lesson").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.Type == "caution").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.ShareExternally && candidate.Type == "preference").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.ShareExternally && candidate.Type == "opinion").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.ShareExternally && candidate.Type == "lesson").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            active.Where(candidate => candidate.ShareExternally && candidate.Type == "caution").Take(MaximumActivePerType).Select(candidate => candidate.Statement).ToArray(),
            document.Reflections.TakeLast(5).Select(reflection => reflection.Text).ToArray(),
            StoragePath);
    }

    private static void TrimCandidates(CharacterGrowthDocument document)
    {
        if (document.Candidates.Count <= MaximumCandidates)
            return;

        document.Candidates = document.Candidates
            .OrderBy(candidate => candidate.Active ? 1 : 0)
            .ThenBy(candidate => candidate.UserConfirmed ? 1 : 0)
            .ThenBy(candidate => candidate.Evidence)
            .ThenBy(candidate => candidate.LastReinforcedAt)
            .Skip(Math.Max(0, document.Candidates.Count - MaximumCandidates))
            .ToList();
    }

    private static string ResolveSpeaker(BrainContext context)
    {
        if (!IsExternalConversation(context))
            return "William";

        var request = context.Request ?? string.Empty;
        var speakerMatch = SpeakerLineRegex().Match(request);
        if (speakerMatch.Success)
            return NormalizeName(speakerMatch.Groups[1].Value, "Unknown traveler");

        var messageMatch = ExternalMessageSpeakerRegex().Match(request);
        return messageMatch.Success
            ? NormalizeName(messageMatch.Groups[1].Value, "Unknown traveler")
            : "Unknown traveler";
    }

    private static string ExtractUserMessage(BrainContext context)
    {
        var request = context.Request ?? string.Empty;
        if (!IsExternalConversation(context))
            return NormalizeText(request);

        var match = MessageLineRegex().Match(request);
        return NormalizeText(match.Success ? match.Groups[1].Value : request);
    }

    private static bool IsExternalConversation(BrainContext context) =>
        context.Purpose.Contains("External MajorMUD conversation", StringComparison.OrdinalIgnoreCase);

    private static string BuildFingerprint(string speaker, string message)
    {
        var normalized = NormalizeText(message).ToLowerInvariant();
        if (normalized.Length == 0)
            return string.Empty;
        return $"{speaker.ToLowerInvariant()}|{TrimTo(normalized, 600)}";
    }

    private static string BuildCandidateKey(string value)
    {
        var terms = WordRegex().Matches(NormalizeText(value).ToLowerInvariant())
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(term => term.Length >= 2 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        return terms.Length == 0 ? string.Empty : string.Join(" ", terms);
    }

    private static bool StatementsEquivalent(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
            return true;

        var leftWords = WordRegex().Matches(left.ToLowerInvariant())
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(word => !StopWords.Contains(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightWords = WordRegex().Matches(right.ToLowerInvariant())
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(word => !StopWords.Contains(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (leftWords.Count == 0 || rightWords.Count == 0)
            return false;
        return leftWords.Intersect(rightWords, StringComparer.OrdinalIgnoreCase).Count() /
               (double)Math.Max(leftWords.Count, rightWords.Count) >= 0.72;
    }

    private static IReadOnlyList<string> SplitSentences(string text) =>
        SentenceBoundaryRegex().Split(text ?? string.Empty)
            .Select(NormalizeText)
            .Where(sentence => sentence.Length > 0)
            .Take(16)
            .ToArray();

    private static bool IsSafeGrowthStatement(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length < 8 || normalized.Length > 360)
            return false;

        var lower = normalized.ToLowerInvariant();
        return !ContainsAny(lower,
            "repository", "source file", "code file", "build", "compile", "installed", "installer", "deploy",
            "llama.cpp", "llamasharp", "gguf", "model", "context window", "token", "system prompt", "tool call",
            "i cannot", "i can't", "i do not have access", "i don't have access", "right now", "currently",
            "today", "this turn", "this request", "as an ai", "language model", "chatbot", "[no_reply]");
    }

    private static bool IsUntrustedQuestion(string matchedText, string fullMessage)
    {
        if (fullMessage.Contains("remember", StringComparison.OrdinalIgnoreCase))
            return false;
        return matchedText.TrimEnd().EndsWith('?');
    }

    private static string BuildFirstPersonStatement(string verb, string subject)
    {
        var normalizedVerb = verb.ToLowerInvariant() switch
        {
            "love" => "love",
            "prefer" => "prefer",
            "enjoy" => "enjoy",
            "admire" => "admire",
            "respect" => "respect",
            "dislike" => "dislike",
            "hate" => "hate",
            "distrust" => "distrust",
            "fear" => "fear",
            "believe" => "believe",
            "value" => "value",
            _ => "like",
        };
        return $"I {normalizedVerb} {TrimSentenceEnding(subject)}.";
    }

    private static bool IsSentimentType(string value) =>
        value.Equals("preference", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("caution", StringComparison.OrdinalIgnoreCase);

    private static int ResolvePolarity(string verb) => verb.ToLowerInvariant() switch
    {
        "dislike" or "hate" or "distrust" or "fear" => -1,
        "believe" or "value" => 0,
        _ => 1,
    };

    private static string NormalizeCandidateType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "preference" => "preference",
        "opinion" => "opinion",
        "lesson" => "lesson",
        "caution" => "caution",
        _ => "opinion",
    };

    private static int ClampTrait(int value, int fallback) =>
        value == 0 ? fallback : Math.Clamp(value, 25, 90);

    private static string CleanGrowthObject(string? value) =>
        TrimTo(NormalizeText(value).Trim(' ', '.', '!', '?', ',', ';', ':', '"', '\''), 260);

    private static string TrimSentenceEnding(string value) =>
        NormalizeText(value).TrimEnd('.', '!', '?', ',', ';', ':');

    private static string EnsureSentence(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
            return string.Empty;
        return normalized.EndsWith('.') || normalized.EndsWith('!') || normalized.EndsWith('?')
            ? normalized
            : normalized + ".";
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
            return fallback;
        return TrimTo(normalized.Trim('"', '\'', ':', '-', ' '), 80);
    }

    private static string NormalizeKey(string? value) =>
        NormalizeText(value).ToLowerInvariant();

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
        "how", "him", "his", "her", "our", "out", "now", "then", "than", "just", "really", "very", "because",
        "borin", "like", "love", "prefer", "enjoy", "admire", "respect", "dislike", "hate", "distrust", "fear",
        "believe", "value", "learned", "lesson", "favorite", "thing", "things", "something", "someone", "always",
    };

    [GeneratedRegex(@"(?i)(?:remember(?: that)?\s+)?you\s+(?:really\s+)?(like|love|prefer|enjoy|admire|respect|dislike|hate|distrust|fear|believe|value)\s+(.+?)(?:[.!?]|$)")]
    private static partial Regex RememberPreferenceRegex();

    [GeneratedRegex(@"(?i)(?:remember(?: that)?\s+)?your\s+favorite\s+([a-z0-9][a-z0-9 '\-]{1,40})\s+is\s+(.+?)(?:[.!?]|$)")]
    private static partial Regex FavoriteConfirmationRegex();

    [GeneratedRegex(@"(?i)(?:remember(?: that)?\s+)?you(?:'ve| have)?\s+learned\s+(?:that\s+)?(.+?)(?:[.!?]|$)")]
    private static partial Regex LearnedConfirmationRegex();

    [GeneratedRegex(@"(?i)^(?:aye[,! ]+|well[, ]+|honestly[, ]+)?my\s+favorite\s+([a-z0-9][a-z0-9 '\-]{1,40})\s+is\s+(.+)$")]
    private static partial Regex FavoriteReplyRegex();

    [GeneratedRegex(@"(?i)^(?:aye[,! ]+|well[, ]+|honestly[, ]+)?i(?:'ve| have)\s+learned\s+(?:that\s+)?(.+)$")]
    private static partial Regex LearnedReplyRegex();

    [GeneratedRegex(@"(?i)^(?:aye[,! ]+|well[, ]+|honestly[, ]+)?i\s+(fear|distrust|hate|am wary of|am cautious of)\s+(.+)$")]
    private static partial Regex CautionReplyRegex();

    [GeneratedRegex(@"(?i)^(?:aye[,! ]+|well[, ]+|honestly[, ]+)?i\s+(like|love|prefer|enjoy|admire|respect|dislike|hate|distrust|fear|believe|value)\s+(.+)$")]
    private static partial Regex OpinionReplyRegex();

    [GeneratedRegex(@"(?im)^Speaker:\s*([^\r\n]+)")]
    private static partial Regex SpeakerLineRegex();

    [GeneratedRegex(@"(?im)^([^\r\n:]{1,80})\s*\([^\r\n)]{1,30}\):\s*")]
    private static partial Regex ExternalMessageSpeakerRegex();

    [GeneratedRegex(@"(?im)^Message:\s*([^\r\n]+)")]
    private static partial Regex MessageLineRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9'_-]*")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();


    private enum GrowthTrait
    {
        Courage,
        Patience,
        Empathy,
        Independence,
        Resilience,
        Playfulness,
    }

    private sealed class CharacterGrowthDocument
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
        public string LastSpeaker { get; set; } = "William";
        public string LastPreparedFingerprint { get; set; } = string.Empty;
        public DateTimeOffset LastPreparedAt { get; set; }
        public CharacterTraitState Traits { get; set; } = new();
        public List<CharacterGrowthCandidate> Candidates { get; set; } = [];
        public List<CharacterReflectionState> Reflections { get; set; } = [];
    }

    private sealed class CharacterTraitState
    {
        public int Courage { get; set; } = 62;
        public int Patience { get; set; } = 68;
        public int Empathy { get; set; } = 72;
        public int Independence { get; set; } = 58;
        public int Resilience { get; set; } = 66;
        public int Playfulness { get; set; } = 57;
        public int CourageEvidence { get; set; }
        public int PatienceEvidence { get; set; }
        public int EmpathyEvidence { get; set; }
        public int IndependenceEvidence { get; set; }
        public int ResilienceEvidence { get; set; }
        public int PlayfulnessEvidence { get; set; }
    }

    private sealed class CharacterGrowthCandidate
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "opinion";
        public string Key { get; set; } = string.Empty;
        public string Statement { get; set; } = string.Empty;
        public int Polarity { get; set; }
        public int Evidence { get; set; }
        public bool Active { get; set; }
        public bool UserConfirmed { get; set; }
        public bool ShareExternally { get; set; }
        public string SourceSpeaker { get; set; } = string.Empty;
        public DateTimeOffset FirstObservedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset LastReinforcedAt { get; set; } = DateTimeOffset.Now;
    }

    private sealed class CharacterReflectionState
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string ResponseExcerpt { get; set; } = string.Empty;
    }
}
