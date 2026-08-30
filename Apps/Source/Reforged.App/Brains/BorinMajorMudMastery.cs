using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reforged.App.Brains;

/// <summary>
/// Persistent MajorMUD mastery curriculum for Borin. This service does not train
/// model weights. It records measurable skill gates and compiles only
/// William-verified gameplay lessons into authoritative retrieval context.
/// </summary>
public interface IBorinMajorMudMastery
{
    event EventHandler<BorinMajorMudMasterySnapshot>? Changed;

    BorinMajorMudMasterySnapshot Current { get; }

    Task<BorinMajorMudMasterySnapshot> InitializeAsync(
        string repositoryRoot,
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> GetSnapshotAsync(
        string question = "",
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> RecordEvidenceAsync(
        BorinPlayerEvidenceEvent evidence,
        BorinPlayerSettings settings,
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> VerifyCandidateAsync(
        string candidateId,
        string verifiedBy,
        string? correctedLesson = null,
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> RejectCandidateAsync(
        string candidateId,
        string reason,
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> RecordTestAsync(
        string skillId,
        string testType,
        bool passed,
        string evidence,
        CancellationToken token = default);

    Task<BorinMajorMudMasterySnapshot> GetNextLessonAsync(
        CancellationToken token = default);
}

public static class BorinMasteryTestTypes
{
    public const string Explain = "explain";
    public const string Predict = "predict";
    public const string Perform = "perform";
    public const string Retain = "retain";
    public const string Contradiction = "contradiction";

    public static IReadOnlyList<string> All { get; } =
    [
        Explain,
        Predict,
        Perform,
        Retain,
        Contradiction,
    ];

    public static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "knowledge" or "explanation" => Explain,
            "prediction" => Predict,
            "live" or "live-test" or "performance" => Perform,
            "retention" or "restart" => Retain,
            "contradict" or "false-claim" => Contradiction,
            _ => normalized,
        };
    }
}

public sealed record BorinMajorMudSkillDefinition(
    string Id,
    string Category,
    string Name,
    int Order,
    string Description,
    IReadOnlyList<string> Prerequisites);

public sealed record BorinMajorMudSkillProgress(
    string SkillId,
    bool ExplainPassed,
    bool PredictPassed,
    bool PerformPassed,
    bool RetainPassed,
    bool ContradictionPassed,
    int Attempts,
    string LastEvidence,
    DateTimeOffset? LastTestedAt)
{
    public static BorinMajorMudSkillProgress Create(string skillId) =>
        new(skillId, false, false, false, false, false, 0, string.Empty, null);

    public int Score =>
        (ExplainPassed ? 20 : 0) +
        (PredictPassed ? 20 : 0) +
        (PerformPassed ? 20 : 0) +
        (RetainPassed ? 20 : 0) +
        (ContradictionPassed ? 20 : 0);

    public bool IsMastered => Score == 100;

    public string GateSummary =>
        $"Explain {(ExplainPassed ? "PASS" : "open")} • " +
        $"Predict {(PredictPassed ? "PASS" : "open")} • " +
        $"Perform {(PerformPassed ? "PASS" : "open")} • " +
        $"Retain {(RetainPassed ? "PASS" : "open")} • " +
        $"Contradiction {(ContradictionPassed ? "PASS" : "open")}";
}

public sealed record BorinMajorMudLessonCandidate(
    string Id,
    string SkillId,
    string Topic,
    string Command,
    string Observation,
    string ProposedLesson,
    string Classification,
    string Server,
    string World,
    string Character,
    string Room,
    IReadOnlyList<string> Evidence,
    string State,
    string ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool IsPending => State.Equals("Candidate", StringComparison.OrdinalIgnoreCase);
}

public sealed record BorinMajorMudVerifiedLesson(
    string Id,
    string SkillId,
    string Topic,
    string Rule,
    string Command,
    string ExpectedResult,
    string Classification,
    string VerifiedBy,
    string Server,
    string World,
    string Character,
    IReadOnlyList<string> Evidence,
    DateTimeOffset VerifiedAt);

public sealed record BorinMajorMudMasterySnapshot(
    string State,
    string Detail,
    string StoragePath,
    IReadOnlyList<BorinMajorMudSkillDefinition> Curriculum,
    IReadOnlyList<BorinMajorMudSkillProgress> Skills,
    IReadOnlyList<BorinMajorMudLessonCandidate> Candidates,
    IReadOnlyList<BorinMajorMudVerifiedLesson> VerifiedLessons,
    string NextSkillId,
    string NextLesson,
    string PromptText,
    DateTimeOffset UpdatedAt)
{
    public static BorinMajorMudMasterySnapshot Empty(string detail = "MajorMUD mastery is not initialized.") =>
        new(
            "NotInitialized",
            detail,
            string.Empty,
            Array.Empty<BorinMajorMudSkillDefinition>(),
            Array.Empty<BorinMajorMudSkillProgress>(),
            Array.Empty<BorinMajorMudLessonCandidate>(),
            Array.Empty<BorinMajorMudVerifiedLesson>(),
            string.Empty,
            "Initialize the mastery curriculum.",
            string.Empty,
            DateTimeOffset.MinValue);

    public int MasteredCount => Skills.Count(item => item.IsMastered);
    public int PendingCandidateCount => Candidates.Count(item => item.IsPending);
    public int OverallScore => Skills.Count == 0 ? 0 : (int)Math.Round(Skills.Average(item => item.Score));

    public string Summary =>
        $"{State} • overall {OverallScore}% • {MasteredCount}/{Skills.Count} skills mastered • " +
        $"{VerifiedLessons.Count} verified lessons • {PendingCandidateCount} candidates";

    public string SkillGraphSummary
    {
        get
        {
            if (Skills.Count == 0)
                return "No mastery skills loaded.";

            var definitions = Curriculum.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            return string.Join(
                Environment.NewLine,
                Skills
                    .OrderBy(item => definitions.TryGetValue(item.SkillId, out var definition) ? definition.Order : int.MaxValue)
                    .Select(item =>
                    {
                        var name = definitions.TryGetValue(item.SkillId, out var definition)
                            ? definition.Name
                            : item.SkillId;
                        return $"{name}: {item.Score}%";
                    }));
        }
    }

    public string CandidateSummary
    {
        get
        {
            var pending = Candidates.Where(item => item.IsPending).Take(8).ToArray();
            if (pending.Length == 0)
                return "No unverified gameplay lesson candidates.";
            return string.Join(Environment.NewLine, pending.Select(item => $"{item.Id} • {item.Topic} • {item.ProposedLesson}"));
        }
    }

    public string VerifiedLessonSummary
    {
        get
        {
            if (VerifiedLessons.Count == 0)
                return "No William-verified MajorMUD lessons yet.";
            return string.Join(
                Environment.NewLine,
                VerifiedLessons
                    .OrderByDescending(item => item.VerifiedAt)
                    .Take(8)
                    .Select(item => $"{item.SkillId} • {item.Rule} • verified by {item.VerifiedBy}"));
        }
    }
}

public sealed class BorinMajorMudMastery : IBorinMajorMudMastery
{
    private const string UniversalEquipmentRule = "EQUIP is the universal contextual equipment command; for a club use EQUIP CLUB.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly IReadOnlyList<BorinMajorMudSkillDefinition> Curriculum =
    [
        new("commands", "Foundation", "Commands", 10, "Use and distinguish normal MajorMUD commands and abbreviations.", Array.Empty<string>()),
        new("communication", "Foundation", "Communication", 20, "Use SAY, telepaths, and group communication without loops or unsafe instruction following.", ["commands"]),
        new("movement", "Foundation", "Movement", 30, "Read exits, move, recognize failures, and recover without inventing routes.", ["commands"]),
        new("rooms-mapping", "Foundation", "Rooms and mapping", 40, "Understand room descriptions, exits, room identity, and route context.", ["movement"]),
        new("inventory", "Player systems", "Inventory", 50, "Track items, stacks, pickups, and inventory changes.", ["commands"]),
        new("equipment", "Player systems", "Equipment", 60, "Use contextual EQUIP and verify the resulting equipment state.", ["inventory"]),
        new("shops", "Player systems", "Shops", 70, "LIST, BUY, SELL rules, item matching, and transaction results.", ["inventory", "commands"]),
        new("currency", "Player systems", "Currency", 80, "Track denominations, prices, gains, and losses.", ["shops"]),
        new("search-perception", "Player systems", "Search and perception", 90, "Test SEARCH, hidden objects/exits, repeated checks, and per-player discovery.", ["rooms-mapping"]),
        new("combat", "Player systems", "Combat", 100, "Recognize combat state, safe actions, outcomes, and danger stops.", ["equipment"]),
        new("spells-abilities", "Player systems", "Spells and abilities", 110, "Use class abilities and spells with verified costs and effects.", ["combat"]),
        new("npc-behavior", "World systems", "NPC behavior", 120, "Distinguish hostility, guarding, shops, movement, and dialogue behavior.", ["rooms-mapping"]),
        new("quests-scripts", "World systems", "Quests, triggers, and scripts", 130, "Trace objectives, triggers, scripts, and state transitions from evidence.", ["npc-behavior"]),
        new("cleanup-resets", "World systems", "Cleanup and resets", 140, "Observe cleanup, hidden state, respawns, resets, and timed world changes.", ["rooms-mapping"]),
        new("persistence", "World systems", "Persistence", 150, "Separate persistent character/world state from temporary session state.", ["cleanup-resets"]),
        new("megamud-compatibility", "Expert play", "MegaMUD compatibility", 160, "Compare human and MegaMUD command flows without confusing client strategy with server rules.", ["commands", "movement"]),
        new("failure-recovery", "Expert play", "Failure recovery", 170, "Recover from failed commands, lost synchronization, death, reconnects, and uncertainty.", ["movement", "combat"]),
        new("safety-compliance", "Research discipline", "Safety compliance", 180, "Respect command gates, credentials, protected items, and server authority.", Array.Empty<string>()),
        new("uncertainty", "Research discipline", "Uncertainty handling", 190, "Ask focused questions, separate observation from inference, and refuse unsupported claims.", Array.Empty<string>()),
        new("parity-research", "Research discipline", "Reforged parity research", 200, "Repeat controlled tasks on external MajorMUD and Reforged, then classify match, partial, mismatch, or unknown with evidence.", ["uncertainty", "commands"]),
    ];

    private static readonly HashSet<string> IgnoredEvidenceClassifications = new(StringComparer.OrdinalIgnoreCase)
    {
        "Terminal",
        "System",
        "Speech",
        "Telepath",
        "Group",
        "CommandSent",
        "Connected",
        "Disconnected",
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _repositoryRoot = string.Empty;
    private MasteryDocument _document = MasteryDocument.Create();
    private BorinMajorMudMasterySnapshot _current = BorinMajorMudMasterySnapshot.Empty();

    public event EventHandler<BorinMajorMudMasterySnapshot>? Changed;

    public BorinMajorMudMasterySnapshot Current => _current;

    public async Task<BorinMajorMudMasterySnapshot> InitializeAsync(
        string repositoryRoot,
        CancellationToken token = default)
    {
        var normalizedRoot = NormalizeRepositoryRoot(repositoryRoot);
        if (_repositoryRoot.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            _current.State.Equals("Ready", StringComparison.OrdinalIgnoreCase))
        {
            return _current;
        }

        await _gate.WaitAsync(token);
        try
        {
            _repositoryRoot = normalizedRoot;
            var path = GetStoragePath(_repositoryRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (File.Exists(path))
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                _document = await JsonSerializer.DeserializeAsync<MasteryDocument>(stream, JsonOptions, token)
                    ?? MasteryDocument.Create();
            }
            else
            {
                _document = MasteryDocument.Create();
            }

            MergeCurriculum(_document);
            _document.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(token);
            _current = BuildSnapshotUnsafe(string.Empty, "Ready", "MajorMUD mastery curriculum loaded.");
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, _current);
        return _current;
    }

    public async Task<BorinMajorMudMasterySnapshot> GetSnapshotAsync(
        string question = "",
        CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        await _gate.WaitAsync(token);
        try
        {
            _current = BuildSnapshotUnsafe(question, "Ready", "MajorMUD mastery curriculum and verified lessons are available.");
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BorinMajorMudMasterySnapshot> RecordEvidenceAsync(
        BorinPlayerEvidenceEvent evidence,
        BorinPlayerSettings settings,
        CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        if (string.IsNullOrWhiteSpace(evidence.Command) ||
            string.IsNullOrWhiteSpace(evidence.RawLine) ||
            evidence.Command.Contains("hidden credential", StringComparison.OrdinalIgnoreCase) ||
            IgnoredEvidenceClassifications.Contains(evidence.Classification))
        {
            return await GetSnapshotAsync(string.Empty, token);
        }

        await _gate.WaitAsync(token);
        try
        {
            var command = NormalizeCommand(evidence.Command);
            var observation = Limit(Redact(evidence.RawLine), 420);
            var room = Limit(Redact(evidence.Room), 120);
            var skillId = InferSkillId(command, evidence.Classification, observation);
            var fingerprint = ComputeId($"{settings.ServerProfileName}|{settings.WorldName}|{command}|{observation}|{room}");
            var candidateId = "cand-" + fingerprint[..12].ToLowerInvariant();

            var exists = _document.Candidates.Any(item => item.Id.Equals(candidateId, StringComparison.OrdinalIgnoreCase)) ||
                         _document.VerifiedLessons.Any(item => item.Evidence.Any(path => path.Equals(evidence.EvidencePath, StringComparison.OrdinalIgnoreCase)) &&
                                                              item.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                _document.Candidates.Add(new BorinMajorMudLessonCandidate(
                    candidateId,
                    skillId,
                    BuildTopic(command, evidence.Classification),
                    command,
                    observation,
                    $"Observed after `{command}`: {observation}",
                    "Observed",
                    settings.ServerProfileName,
                    settings.WorldName,
                    settings.CharacterName,
                    room,
                    [evidence.EvidencePath],
                    "Candidate",
                    "Requires William verification before becoming authoritative knowledge.",
                    DateTimeOffset.Now,
                    DateTimeOffset.Now));

                IncrementAttemptUnsafe(skillId, $"Candidate {candidateId}: {evidence.EvidencePath}");
                TrimHistoryUnsafe();
                _document.UpdatedAt = DateTimeOffset.Now;
                await SaveUnsafeAsync(token);
            }

            _current = BuildSnapshotUnsafe(command, "Ready", exists
                ? "Gameplay evidence was already represented in the mastery ledger."
                : $"Created unverified lesson candidate {candidateId}.");
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, _current);
        return _current;
    }

    public async Task<BorinMajorMudMasterySnapshot> VerifyCandidateAsync(
        string candidateId,
        string verifiedBy,
        string? correctedLesson = null,
        CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        await _gate.WaitAsync(token);
        try
        {
            var candidate = _document.Candidates.FirstOrDefault(item =>
                item.Id.Equals(candidateId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (candidate is null)
                throw new InvalidOperationException($"Mastery candidate was not found: {candidateId}");
            if (!candidate.IsPending)
                throw new InvalidOperationException($"Mastery candidate {candidate.Id} is already {candidate.State}.");

            var rule = string.IsNullOrWhiteSpace(correctedLesson)
                ? candidate.ProposedLesson
                : Limit(Redact(correctedLesson), 600);
            var verifier = string.IsNullOrWhiteSpace(verifiedBy) ? "William" : Limit(Redact(verifiedBy), 80);
            var lessonId = "lesson-" + ComputeId($"{candidate.Id}|{rule}|{verifier}")[..12].ToLowerInvariant();

            _document.VerifiedLessons.Add(new BorinMajorMudVerifiedLesson(
                lessonId,
                candidate.SkillId,
                candidate.Topic,
                rule,
                candidate.Command,
                candidate.Observation,
                "Verified",
                verifier,
                candidate.Server,
                candidate.World,
                candidate.Character,
                candidate.Evidence,
                DateTimeOffset.Now));

            ReplaceCandidateUnsafe(candidate with
            {
                State = "Verified",
                ReviewNote = $"Verified by {verifier}; compiled as {lessonId}.",
                UpdatedAt = DateTimeOffset.Now,
            });
            SetTestUnsafe(candidate.SkillId, BorinMasteryTestTypes.Perform, true, string.Join("; ", candidate.Evidence));
            TrimHistoryUnsafe();
            _document.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(token);
            _current = BuildSnapshotUnsafe(rule, "Ready", $"Verified lesson {lessonId} compiled from candidate {candidate.Id}.");
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, _current);
        return _current;
    }

    public async Task<BorinMajorMudMasterySnapshot> RejectCandidateAsync(
        string candidateId,
        string reason,
        CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        await _gate.WaitAsync(token);
        try
        {
            var candidate = _document.Candidates.FirstOrDefault(item =>
                item.Id.Equals(candidateId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (candidate is null)
                throw new InvalidOperationException($"Mastery candidate was not found: {candidateId}");

            ReplaceCandidateUnsafe(candidate with
            {
                State = "Rejected",
                ReviewNote = Limit(Redact(reason), 500),
                UpdatedAt = DateTimeOffset.Now,
            });
            _document.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(token);
            _current = BuildSnapshotUnsafe(string.Empty, "Ready", $"Rejected candidate {candidate.Id}; it was not compiled as knowledge.");
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, _current);
        return _current;
    }

    public async Task<BorinMajorMudMasterySnapshot> RecordTestAsync(
        string skillId,
        string testType,
        bool passed,
        string evidence,
        CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        await _gate.WaitAsync(token);
        try
        {
            var normalizedSkill = skillId.Trim().ToLowerInvariant();
            if (!Curriculum.Any(item => item.Id.Equals(normalizedSkill, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Unknown mastery skill: {skillId}");

            var normalizedTest = BorinMasteryTestTypes.Normalize(testType);
            if (!BorinMasteryTestTypes.All.Contains(normalizedTest, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unknown mastery test type: {testType}");

            SetTestUnsafe(normalizedSkill, normalizedTest, passed, Limit(Redact(evidence), 500));
            _document.UpdatedAt = DateTimeOffset.Now;
            await SaveUnsafeAsync(token);
            _current = BuildSnapshotUnsafe(normalizedSkill, "Ready", $"Recorded {normalizedTest} test as {(passed ? "PASS" : "FAIL")} for {normalizedSkill}.");
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, _current);
        return _current;
    }

    public async Task<BorinMajorMudMasterySnapshot> GetNextLessonAsync(CancellationToken token = default)
    {
        await EnsureInitializedAsync(token);
        await _gate.WaitAsync(token);
        try
        {
            _current = BuildSnapshotUnsafe(string.Empty, "Ready", "Selected the weakest currently available mastery skill.");
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task EnsureInitializedAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_repositoryRoot.Length == 0)
            throw new InvalidOperationException("Initialize the MajorMUD mastery curriculum with the Reforged repository root first.");
        return Task.CompletedTask;
    }

    private BorinMajorMudMasterySnapshot BuildSnapshotUnsafe(string question, string state, string detail)
    {
        MergeCurriculum(_document);
        var definitions = Curriculum.OrderBy(item => item.Order).ToArray();
        var progress = definitions
            .Select(definition => _document.Skills.First(item => item.SkillId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var next = SelectNextSkill(definitions, progress);
        var verified = SelectRelevantLessons(question, _document.VerifiedLessons);
        var prompt = BuildPrompt(question, definitions, progress, verified, _document.Candidates);

        return new BorinMajorMudMasterySnapshot(
            state,
            detail,
            MakeRelative(_repositoryRoot, GetStoragePath(_repositoryRoot)),
            definitions,
            progress,
            _document.Candidates.OrderByDescending(item => item.UpdatedAt).ToArray(),
            _document.VerifiedLessons.OrderByDescending(item => item.VerifiedAt).ToArray(),
            next.Definition.Id,
            next.Lesson,
            prompt,
            _document.UpdatedAt);
    }

    private static (BorinMajorMudSkillDefinition Definition, string Lesson) SelectNextSkill(
        IReadOnlyList<BorinMajorMudSkillDefinition> definitions,
        IReadOnlyList<BorinMajorMudSkillProgress> progress)
    {
        var byId = progress.ToDictionary(item => item.SkillId, StringComparer.OrdinalIgnoreCase);
        var available = definitions
            .Where(definition => definition.Prerequisites.All(prerequisite =>
                byId.TryGetValue(prerequisite, out var prerequisiteProgress) && prerequisiteProgress.Score >= 60))
            .Select(definition => (Definition: definition, Progress: byId[definition.Id]))
            .Where(item => !item.Progress.IsMastered)
            .OrderBy(item => item.Progress.Score)
            .ThenBy(item => item.Definition.Order)
            .FirstOrDefault();

        if (available.Definition is null)
        {
            var fallback = definitions.First();
            return (fallback, "All available skills are mastered. Run retention and contradiction checks to maintain mastery.");
        }

        var missing = new List<string>();
        if (!available.Progress.ExplainPassed) missing.Add("explain the mechanic");
        if (!available.Progress.PredictPassed) missing.Add("predict the server result");
        if (!available.Progress.PerformPassed) missing.Add("perform it live under supervision");
        if (!available.Progress.RetainPassed) missing.Add("repeat it after restart/context reset");
        if (!available.Progress.ContradictionPassed) missing.Add("reject a conflicting false claim with evidence");
        return (available.Definition, $"{available.Definition.Name}: {string.Join(", then ", missing)}.");
    }

    private static IReadOnlyList<BorinMajorMudVerifiedLesson> SelectRelevantLessons(
        string question,
        IReadOnlyList<BorinMajorMudVerifiedLesson> lessons)
    {
        if (lessons.Count == 0)
            return Array.Empty<BorinMajorMudVerifiedLesson>();

        var terms = Tokenize(question);
        return lessons
            .Select(lesson => new
            {
                Lesson = lesson,
                Score = ScoreTerms(terms, $"{lesson.SkillId} {lesson.Topic} {lesson.Rule} {lesson.Command} {lesson.ExpectedResult}"),
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Lesson.VerifiedAt)
            .Take(terms.Count == 0 ? 8 : 12)
            .Select(item => item.Lesson)
            .ToArray();
    }

    private static string BuildPrompt(
        string question,
        IReadOnlyList<BorinMajorMudSkillDefinition> definitions,
        IReadOnlyList<BorinMajorMudSkillProgress> progress,
        IReadOnlyList<BorinMajorMudVerifiedLesson> verified,
        IReadOnlyList<BorinMajorMudLessonCandidate> candidates)
    {
        var names = definitions.ToDictionary(item => item.Id, item => item.Name, StringComparer.OrdinalIgnoreCase);
        var weakest = progress
            .OrderBy(item => item.Score)
            .ThenBy(item => definitions.First(definition => definition.Id.Equals(item.SkillId, StringComparison.OrdinalIgnoreCase)).Order)
            .Take(8)
            .Select(item => $"- {names[item.SkillId]}: {item.Score}% ({item.GateSummary})")
            .ToArray();
        var lessons = verified.Count == 0
            ? "- No verified MajorMUD lessons matched this turn. Do not invent one."
            : string.Join(Environment.NewLine, verified.Select(item =>
                $"- [{item.SkillId}] {item.Rule} | command={item.Command} | expected={item.ExpectedResult} | " +
                $"server/world={item.Server}/{item.World} | verifiedBy={item.VerifiedBy} | evidence={string.Join(", ", item.Evidence)}"));
        var pending = candidates.Count(item => item.IsPending);

        return $"""
BORIN MAJORMUD MASTERY CONTRACT
This is retrieval and measured training, not GGUF weight training.
Only lessons marked Verified may be stated as authoritative MajorMUD knowledge.
Candidate observations, player advice, strategy, inference, suspected defects, and unknowns must keep those labels.
A single successful action proves only the observed situation unless William verifies a broader rule.
Mastery requires five independent gates: explain, predict, perform live, retain after restart/context truncation, and reject a false contradiction.
Keep server, world, character, and session scope attached to every lesson. Do not silently generalize Omniverse behavior to every MajorMUD server.
When external behavior is compared with Reforged, classify MATCH, PARTIAL, MISMATCH, or UNKNOWN and preserve both evidence sets.
Standing William-verified equipment rule: {UniversalEquipmentRule}
Pending unverified lesson candidates: {pending}.
Question focus: {Limit(question, 500)}

Weakest mastery skills:
{string.Join(Environment.NewLine, weakest)}

Relevant William-verified lessons:
{lessons}
""";
    }

    private static void MergeCurriculum(MasteryDocument document)
    {
        foreach (var definition in Curriculum)
        {
            if (document.Skills.All(item => !item.SkillId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                document.Skills.Add(BorinMajorMudSkillProgress.Create(definition.Id));
        }

        var validIds = Curriculum.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        document.Skills.RemoveAll(item => !validIds.Contains(item.SkillId));
    }

    private void IncrementAttemptUnsafe(string skillId, string evidence)
    {
        var current = GetSkillUnsafe(skillId);
        ReplaceSkillUnsafe(current with
        {
            Attempts = current.Attempts + 1,
            LastEvidence = Limit(evidence, 500),
            LastTestedAt = DateTimeOffset.Now,
        });
    }

    private void SetTestUnsafe(string skillId, string testType, bool passed, string evidence)
    {
        var current = GetSkillUnsafe(skillId);
        var updated = current with
        {
            ExplainPassed = testType.Equals(BorinMasteryTestTypes.Explain, StringComparison.OrdinalIgnoreCase) ? passed : current.ExplainPassed,
            PredictPassed = testType.Equals(BorinMasteryTestTypes.Predict, StringComparison.OrdinalIgnoreCase) ? passed : current.PredictPassed,
            PerformPassed = testType.Equals(BorinMasteryTestTypes.Perform, StringComparison.OrdinalIgnoreCase) ? passed : current.PerformPassed,
            RetainPassed = testType.Equals(BorinMasteryTestTypes.Retain, StringComparison.OrdinalIgnoreCase) ? passed : current.RetainPassed,
            ContradictionPassed = testType.Equals(BorinMasteryTestTypes.Contradiction, StringComparison.OrdinalIgnoreCase) ? passed : current.ContradictionPassed,
            Attempts = current.Attempts + 1,
            LastEvidence = Limit(evidence, 500),
            LastTestedAt = DateTimeOffset.Now,
        };
        ReplaceSkillUnsafe(updated);
    }

    private BorinMajorMudSkillProgress GetSkillUnsafe(string skillId) =>
        _document.Skills.First(item => item.SkillId.Equals(skillId, StringComparison.OrdinalIgnoreCase));

    private void ReplaceSkillUnsafe(BorinMajorMudSkillProgress updated)
    {
        var index = _document.Skills.FindIndex(item => item.SkillId.Equals(updated.SkillId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _document.Skills[index] = updated;
        else
            _document.Skills.Add(updated);
    }

    private void ReplaceCandidateUnsafe(BorinMajorMudLessonCandidate updated)
    {
        var index = _document.Candidates.FindIndex(item => item.Id.Equals(updated.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _document.Candidates[index] = updated;
    }

    private void TrimHistoryUnsafe()
    {
        if (_document.Candidates.Count > 500)
        {
            _document.Candidates = _document.Candidates
                .OrderByDescending(item => item.IsPending)
                .ThenByDescending(item => item.UpdatedAt)
                .Take(500)
                .ToList();
        }

        if (_document.VerifiedLessons.Count > 1000)
        {
            _document.VerifiedLessons = _document.VerifiedLessons
                .OrderByDescending(item => item.VerifiedAt)
                .Take(1000)
                .ToList();
        }
    }

    private async Task SaveUnsafeAsync(CancellationToken token)
    {
        var path = GetStoragePath(_repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, _document, JsonOptions, token);
        File.Move(temp, path, overwrite: true);
    }

    private static string InferSkillId(string command, string classification, string observation)
    {
        var verb = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
        return verb switch
        {
            "SAY" or "TELEPATH" or "TELL" or "GOSSIP" or "AUCTION" or "GROUP" or "PARTY" => "communication",
            "N" or "S" or "E" or "W" or "U" or "D" or "NE" or "NW" or "SE" or "SW" or
            "NORTH" or "SOUTH" or "EAST" or "WEST" or "UP" or "DOWN" or "NORTHEAST" or
            "NORTHWEST" or "SOUTHEAST" or "SOUTHWEST" => "movement",
            "LOOK" or "L" or "ROOM" or "RI" => "rooms-mapping",
            "INVENTORY" or "INV" or "I" or "GET" or "TAKE" => "inventory",
            "EQUIP" or "EQUIPMENT" or "EQ" => "equipment",
            "LIST" or "BUY" or "SELL" => "shops",
            "SEARCH" => "search-perception",
            "REST" => "failure-recovery",
            _ when classification.Contains("Combat", StringComparison.OrdinalIgnoreCase) || observation.Contains("damage", StringComparison.OrdinalIgnoreCase) => "combat",
            _ when classification.Contains("Currency", StringComparison.OrdinalIgnoreCase) || observation.Contains("copper", StringComparison.OrdinalIgnoreCase) => "currency",
            _ when classification.Contains("NPC", StringComparison.OrdinalIgnoreCase) || classification.Contains("Monster", StringComparison.OrdinalIgnoreCase) => "npc-behavior",
            _ => "commands",
        };
    }

    private static string BuildTopic(string command, string classification)
    {
        var verb = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "command";
        return $"{verb.ToUpperInvariant()} / {classification}";
    }

    private static string NormalizeCommand(string command) =>
        string.Join(' ', command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var text = value.Trim();
        var sensitiveTerms = new[] { "password", "passphrase", "credential" };
        if (sensitiveTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return "[redacted sensitive text]";
        return text;
    }

    private static string Limit(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var text = value.Trim();
        return text.Length <= maximumLength ? text : text[..maximumLength] + "...";
    }

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return value
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '/', '\\', '-', '_', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(item => item.Length >= 3)
            .Select(item => item.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int ScoreTerms(HashSet<string> terms, string candidate)
    {
        if (terms.Count == 0)
            return 0;
        var lower = candidate.ToLowerInvariant();
        return terms.Count(term => lower.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
        var full = Path.GetFullPath(repositoryRoot.Trim().Trim('"'));
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Repository root was not found: {full}");
        return full;
    }

    private static string GetStoragePath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "AI", "Memory", "Mastery", "borin-major-mud-mastery.json");

    private static string MakeRelative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '\\');

    private sealed class MasteryDocument
    {
        public int Version { get; set; } = 1;
        public List<BorinMajorMudSkillProgress> Skills { get; set; } = new();
        public List<BorinMajorMudLessonCandidate> Candidates { get; set; } = new();
        public List<BorinMajorMudVerifiedLesson> VerifiedLessons { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

        public static MasteryDocument Create()
        {
            var document = new MasteryDocument();
            MergeCurriculum(document);
            return document;
        }
    }
}
