using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.App.Brains;

public enum BorinAcademyDomain { MajorMud, Reforged }
public enum BorinAcademyQuestionType { FactRecall, Relationship, MultiHop, SourceTrace, Comparison, Diagnostic, RuntimeVerification }
public enum BorinAcademyDifficulty { Basic, Intermediate, Advanced }
public enum BorinKnowledgeGapStatus { Open, Queued, Investigating, NeedsTestRealm, NeedsSourceAnalysis, NeedsNativeAnalysis, Resolved, Blocked }
public enum BorinAcademyFailureCause { None, MissingKnowledge, BadRetrieval, WrongRelationshipTraversal, IncorrectConfidence, ModelReasoningFailure, StaleIndex, ConflictingEvidence, Hallucination }

public static class BorinAcademyCurriculum
{
    public static IReadOnlyList<string> MajorMud { get; } =
        ["Commands", "Rooms", "Navigation", "Items", "Monsters", "Spells", "Classes", "Races", "Combat", "Quests", "Scripts", "Triggers", "Economy", "Timers", "Resets", "Advanced Mechanics"];
    public static IReadOnlyList<string> Reforged { get; } =
        ["Architecture", "Server", "Runtime", "Database", "Importers", "Editor", "Client", "Librarium", "Networking", "Persistence", "Commands", "Testing", "Build System", "Borin", "Deployment"];
}

public sealed record BorinAcademyQuestion(string Id, BorinAcademyDomain Domain, string Subject,
    BorinAcademyQuestionType Type, BorinAcademyDifficulty Difficulty, string Prompt,
    string EvidenceQuery, string ExpectedEvidenceLocation, string ExpectedEvidenceText,
    BorinKnowledgeConfidence ExpectedConfidence, string CatalogFingerprint);

public sealed record BorinAcademyScore(double Correctness, double Completeness, double SourceQuality,
    double RetrievalAccuracy, double ConfidenceAccuracy, double RelationshipReasoning, double HallucinationPenalty)
{
    public double Total => Math.Clamp((Correctness * .25) + (Completeness * .15) + (SourceQuality * .20) +
        (RetrievalAccuracy * .15) + (ConfidenceAccuracy * .10) + (RelationshipReasoning * .15) - HallucinationPenalty, 0, 100);
}

public sealed record BorinAcademyAttempt(string QuestionId, string Subject, string QuestionType, string Difficulty,
    string Model, BorinAcademyScore Score, bool Passed, string FailureReason, BorinAcademyFailureCause FailureCause,
    string EvidenceLocation, string BuildFingerprint, DateTimeOffset TestedUtc, string Answer = "", string Domain = "");

public sealed record BorinKnowledgeGap(string Id, string Subject, string Question, string KnownEvidence, string Missing,
    string RecommendedTest, string Priority, BorinKnowledgeGapStatus Status, string BuildFingerprint,
    DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, string ResolutionEvidence = "", string Domain = "");

public sealed record BorinAcademySubjectProgress(string Domain, string Subject, int Attempted, int Passed, int Failed,
    int VerifiedFacts, int KnowledgeGaps, int Conflicts, double? MasteryPercent, DateTimeOffset? LastTestedUtc);

public sealed record BorinAcademySnapshot(IReadOnlyList<BorinAcademySubjectProgress> Subjects, int VerifiedFacts,
    int OpenGaps, int Conflicts, int UnverifiedClaims, string CurrentTraining, string CurrentTask,
    string CurrentModel, int UltraEscalations, bool IsTraining, bool TrainWhileIdle, int IdleRequestsPerMinute,
    string BuildFingerprint, string StoragePath)
{
    public double? MajorMudMastery => Weighted(BorinAcademyDomain.MajorMud.ToString());
    public double? ReforgedMastery => Weighted(BorinAcademyDomain.Reforged.ToString());
    private double? Weighted(string domain)
    {
        var tested = Subjects.Where(item => item.Domain == domain && item.Attempted > 0).ToArray();
        return tested.Length == 0 ? null : tested.Sum(item => item.MasteryPercent!.Value * item.Attempted) / tested.Sum(item => item.Attempted);
    }
}

public interface IBorinAcademy
{
    Task<BorinAcademySnapshot> GetSnapshotAsync(string repositoryRoot, CancellationToken token = default);
    Task<IReadOnlyList<BorinAcademyQuestion>> GenerateQuestionsAsync(string repositoryRoot, string scope, int count, CancellationToken token = default);
    Task<BorinAcademyAttempt> EvaluateAsync(string repositoryRoot, BorinAcademyQuestion question, string answer, string model, CancellationToken token = default);
    Task<BorinAcademySnapshot> ReplayHistoryAsync(string repositoryRoot, CancellationToken token = default);
    Task<IReadOnlyList<BorinKnowledgeGap>> GetGapsAsync(string repositoryRoot, CancellationToken token = default);
    Task<BorinKnowledgeGap> BeginGapInvestigationAsync(string repositoryRoot, string gapId, CancellationToken token = default);
    Task<BorinKnowledgeGap> ResolveGapAsync(string repositoryRoot, string gapId, string evidence, CancellationToken token = default);
    Task SetIdleTrainingAsync(string repositoryRoot, bool enabled, int requestsPerMinute, CancellationToken token = default);
    Task<BorinKnowledgeGap> RecordCorrectionCandidateAsync(string repositoryRoot, string previousClaim, string correction, bool userConfirmedAuthoritative, CancellationToken token = default);
    Task<BorinAcademySnapshot> MarkActivityAsync(string repositoryRoot, string training, string task, string model, bool running, CancellationToken token = default);
}

/// <summary>Curriculum and evaluation metadata over the shared Knowledge Core; never a second factual store.</summary>
public sealed class BorinAcademy : IBorinAcademy
{
    private const int Version = 6;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly Regex Terms = new("[A-Za-z0-9_.#-]{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IBorinKnowledgeCore _knowledge;
    private readonly IBorinVerifiedKnowledgeLearning _learning;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BorinAcademy(IBorinKnowledgeCore knowledge, IBorinVerifiedKnowledgeLearning learning)
    { _knowledge = knowledge; _learning = learning; }

    public async Task<IReadOnlyList<BorinAcademyQuestion>> GenerateQuestionsAsync(string root, string scope, int count, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token);
        await InvalidateIfStaleAsync(root, state, token);
        var requested = Math.Clamp(count, 1, 100);
        var subjects = SelectSubjects(scope, state).ToArray();
        var questions = new List<BorinAcademyQuestion>();
        var index = 0;
        foreach (var subject in subjects)
        {
            token.ThrowIfCancellationRequested();
            var type = (BorinAcademyQuestionType)(index++ % Enum.GetValues<BorinAcademyQuestionType>().Length);
            var evidenceQuery = ReforgedQuery(subject.Subject);
            if (subject.Domain == BorinAcademyDomain.MajorMud)
            {
                var family = MajorMudFamily(subject.Subject);
                var samples = await _knowledge.GetMajorMudSampleEntitiesAsync(root, family, 8, token);
                var offset = state.Attempts.Count(item => item.Subject.Equals(subject.Subject, StringComparison.OrdinalIgnoreCase)) % Math.Max(1, samples.Count);
                var sample = samples.ElementAtOrDefault(offset);
                if (sample is null)
                {
                    await AddGapUnsafeAsync(state, subject.Subject, $"Produce authoritative evidence for {subject.Subject}.", "No authoritative family record exists.",
                        $"Generated MajorMUD family '{family}'", "Inspect imported/decoded generation coverage.", "High", token, subject.Domain.ToString());
                    continue;
                }
                evidenceQuery = sample.Id;
            }
            var mode = type switch
            {
                BorinAcademyQuestionType.FactRecall or BorinAcademyQuestionType.SourceTrace => BorinKnowledgeSearchMode.Exact,
                BorinAcademyQuestionType.Relationship or BorinAcademyQuestionType.MultiHop or BorinAcademyQuestionType.Comparison => BorinKnowledgeSearchMode.Relationship,
                _ => BorinKnowledgeSearchMode.Semantic,
            };
            var evidence = await _knowledge.SearchAsync(root, new BorinKnowledgeQuery(evidenceQuery, mode, 12), token);
            var proof = evidence.Matches.FirstOrDefault(item => IsSubjectAuthority(subject.Domain, item) && item.Confidence >= BorinKnowledgeConfidence.StronglyCorrelated && item.Sources is { Count: > 0 });
            if (proof is null && mode != BorinKnowledgeSearchMode.Hybrid)
            {
                evidence = await _knowledge.SearchAsync(root, new BorinKnowledgeQuery(evidenceQuery, BorinKnowledgeSearchMode.Hybrid, 20), token);
                proof = evidence.Matches.FirstOrDefault(item => IsSubjectAuthority(subject.Domain, item) && item.Confidence >= BorinKnowledgeConfidence.StronglyCorrelated && item.Sources is { Count: > 0 });
            }
            if (proof is null)
            {
                // Failure to construct a Reforged quiz question is index/retrieval health,
                // not evidence that Borin lacks the underlying repository knowledge.
                if (subject.Domain == BorinAcademyDomain.MajorMud)
                    await AddGapUnsafeAsync(state, subject.Subject, $"Produce authoritative evidence for {subject.Subject}.", "No qualifying evidence retrieved.",
                        "Authoritative exact/relationship evidence", "Reindex and inspect source/data authority.", "High", token, subject.Domain.ToString());
                continue;
            }
            var difficulty = type == BorinAcademyQuestionType.FactRecall ? BorinAcademyDifficulty.Basic : type is BorinAcademyQuestionType.Relationship or BorinAcademyQuestionType.SourceTrace ? BorinAcademyDifficulty.Intermediate : BorinAcademyDifficulty.Advanced;
            var focus = subject.Domain == BorinAcademyDomain.MajorMud
                ? $"the authoritative `{evidenceQuery}` record"
                : $"the implementation evidence retrieved for `{evidenceQuery}`";
            var prompt = type switch
            {
                BorinAcademyQuestionType.Relationship => $"For the {subject.Subject} curriculum, what authoritative relationships are established for {focus}? Cite the local evidence.",
                BorinAcademyQuestionType.MultiHop => $"For the {subject.Subject} curriculum, trace only the supported multi-hop dependencies for {focus}; cite every supported hop.",
                BorinAcademyQuestionType.SourceTrace => $"For the {subject.Subject} curriculum, where is {focus} implemented or defined, and what calls or consumes it? Cite exact source locations.",
                BorinAcademyQuestionType.Comparison => $"For the {subject.Subject} curriculum, compare the supplied imported/decoded authority, RealmGate implementation, tests, and runtime evidence for {focus}. Identify unsupported stages.",
                BorinAcademyQuestionType.RuntimeVerification => $"For the {subject.Subject} curriculum, what does the supplied evidence establish about {focus}, and what still requires controlled runtime or Test Realm verification?",
                BorinAcademyQuestionType.Diagnostic => $"For the {subject.Subject} curriculum, which supplied authoritative evidence diagnoses {focus}, and why?",
                _ => $"For the {subject.Subject} curriculum, what does the supplied authoritative local knowledge establish about {focus}? Cite the exact source."
            };
            var id = Id("question", $"{state.BuildFingerprint}|{subject.Domain}|{subject.Subject}|{type}|{proof.Location}");
            questions.Add(new(id, subject.Domain, subject.Subject, type, difficulty, prompt, evidenceQuery,
                proof.Location, proof.Text, proof.Confidence, state.BuildFingerprint));
            if (questions.Count >= requested) break;
        }
        await SaveAsync(root, state, token);
        return questions;
    }

    public async Task<BorinAcademyAttempt> EvaluateAsync(string root, BorinAcademyQuestion question, string answer, string model, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token);
        await InvalidateIfStaleAsync(root, state, token);
        var current = await _knowledge.SearchAsync(root, new BorinKnowledgeQuery(question.EvidenceQuery, BorinKnowledgeSearchMode.Hybrid, 8), token);
        var proof = current.Matches.FirstOrDefault(item => item.Location.Equals(question.ExpectedEvidenceLocation, StringComparison.OrdinalIgnoreCase))
                    ?? current.Matches.FirstOrDefault(item => item.Sources is { Count: > 0 });
        var expectedTerms = Significant(question.ExpectedEvidenceText).Take(20).ToArray();
        var answerTerms = Significant(answer).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = expectedTerms.Length == 0 ? 0 : expectedTerms.Count(answerTerms.Contains) * 100d / expectedTerms.Length;
        var cites = proof is not null && (answer.Contains(proof.RelativePath, StringComparison.OrdinalIgnoreCase) || answer.Contains(proof.Location, StringComparison.OrdinalIgnoreCase));
        var hasEvidence = proof is not null && proof.Sources is { Count: > 0 };
        var relationship = question.Type is BorinAcademyQuestionType.Relationship or BorinAcademyQuestionType.MultiHop
            ? (answer.Contains("→", StringComparison.Ordinal) || answer.Contains("because", StringComparison.OrdinalIgnoreCase) || overlap >= 35 ? 100 : 25) : 100;
        var confidenceAccurate = answer.Contains(question.ExpectedConfidence.ToDisplayText(), StringComparison.OrdinalIgnoreCase) || !answer.Contains("PROVEN", StringComparison.OrdinalIgnoreCase);
        var hallucination = DetectHallucination(answer, current.Matches) ? 25 : 0;
        var score = new BorinAcademyScore(Math.Min(100, overlap * 2), Math.Min(100, overlap * 1.5), cites && hasEvidence ? 100 : hasEvidence ? 55 : 0,
            proof is null ? 0 : 100, confidenceAccurate ? 100 : 0, relationship, hallucination);
        var passed = score.Total >= 70 && hasEvidence && hallucination == 0;
        var cause = passed ? BorinAcademyFailureCause.None : proof is null ? BorinAcademyFailureCause.BadRetrieval : hallucination > 0 ? BorinAcademyFailureCause.Hallucination : !cites ? BorinAcademyFailureCause.MissingKnowledge : question.Type == BorinAcademyQuestionType.Relationship && relationship < 70 ? BorinAcademyFailureCause.WrongRelationshipTraversal : BorinAcademyFailureCause.ModelReasoningFailure;
        var attempt = new BorinAcademyAttempt(question.Id, question.Subject, question.Type.ToString(), question.Difficulty.ToString(), Limit(model, 120), score, passed,
            passed ? "Evidence-backed answer passed all gates." : cause.ToString(), cause, proof?.Location ?? string.Empty, state.BuildFingerprint, DateTimeOffset.UtcNow, Limit(answer, 4000), question.Domain.ToString());
        state.Attempts.Add(attempt);
        if (!passed)
            await AddGapUnsafeAsync(state, question.Subject, question.Prompt, proof?.Display ?? "No evidence", cause.ToString(), Recommendation(cause), cause == BorinAcademyFailureCause.Hallucination ? "Critical" : "Normal", token, question.Domain.ToString());
        state.CurrentTask = $"Evaluated {question.Id}: {(passed ? "PASS" : "FAIL")}";
        state.CurrentModel = model;
        await SaveAsync(root, state, token);
        return attempt;
    }

    public async Task<BorinAcademySnapshot> ReplayHistoryAsync(string root, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token);
        var added = 0;
        var patterns = new[] { "Reports/BorinBrain/RuntimeLogs/**/*.json", "Reports/BorinBrain/RuntimeLogs/**/*.log", "Reports/BorinBrain/RuntimeLogs/**/*.txt", "Reports/**/*.md", "Reports/**/*.json", "Release/Logs/**/*.json", "Release/Logs/**/*.log", "Release/Logs/**/*.txt", "AI/Workspaces/Borin/Jobs/**/*.json", "AI/Workspaces/Borin/Jobs/**/*.log" };
        foreach (var pattern in patterns)
        {
            foreach (var file in Glob(root, pattern).Take(500))
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                var key = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (state.ReplayedEvidence.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                var text = await File.ReadAllTextAsync(file, token);
                if (!LooksVerified(text)) continue;
                await _learning.RecordInvestigationAsync($"Historical evidence replay: {Path.GetFileName(file)}",
                    "Recovered a completed build, test, repair, or runtime verification episode from existing evidence.", key, true, ExtractEntityIds(text), token);
                state.ReplayedEvidence.Add(key);
                added++;
            }
        }
        if (added > 0)
            await _learning.PromoteProcedureAsync("Learn from verified historical evidence",
                ["Locate completed build, test, repair, or Test Realm evidence.", "Require an explicit successful verification marker.", "Retain the original evidence path and canonical entity references.", "Create a durable investigation episode without copying or replacing authority.", "Use the lesson in later retrieval and retest affected mastery subjects."],
                true, $"{added} newly replayed verified evidence file(s); ledger={PathFor(root)}", token);
        state.CurrentTask = $"Historical replay complete: {state.ReplayedEvidence.Count:N0} verified evidence files retained by reference.";
        await SaveAsync(root, state, token);
        return await SnapshotAsync(root, state, token);
    }

    public async Task<IReadOnlyList<BorinKnowledgeGap>> GetGapsAsync(string root, CancellationToken token = default) =>
        (await LoadAsync(root, token)).Gaps.OrderBy(g => g.Status == BorinKnowledgeGapStatus.Resolved).ThenByDescending(g => g.Priority).ThenBy(g => g.CreatedUtc).ToArray();

    public async Task<BorinKnowledgeGap> BeginGapInvestigationAsync(string root, string id, CancellationToken token = default) =>
        await UpdateGapAsync(root, id, BorinKnowledgeGapStatus.Investigating, string.Empty, token);

    public async Task<BorinKnowledgeGap> ResolveGapAsync(string root, string id, string evidence, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(evidence)) throw new ArgumentException("Resolution evidence is required.");
        return await UpdateGapAsync(root, id, BorinKnowledgeGapStatus.Resolved, evidence, token);
    }

    public async Task SetIdleTrainingAsync(string root, bool enabled, int rpm, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token); state.TrainWhileIdle = enabled; state.IdleRequestsPerMinute = Math.Clamp(rpm, 1, 10); await SaveAsync(root, state, token);
    }

    public async Task<BorinKnowledgeGap> RecordCorrectionCandidateAsync(string root, string previousClaim, string correction, bool userConfirmedAuthoritative, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(previousClaim) || string.IsNullOrWhiteSpace(correction)) throw new ArgumentException("A previous claim and correction are required.");
        var state = await LoadAsync(root, token); await InvalidateIfStaleAsync(root, state, token);
        var question = $"Verify correction: {Limit(correction, 300)}";
        await AddGapUnsafeAsync(state, "User correction", question,
            $"Previous claim: {Limit(previousClaim, 500)}; user statement: {Limit(correction, 500)}",
            userConfirmedAuthoritative ? "Independent corroboration while retaining user-confirmed provenance" : "Authoritative verification",
            "Search local source/data/runtime evidence; do not overwrite authority from conversation alone.", userConfirmedAuthoritative ? "High" : "Normal", token);
        var gapId = Id("gap", $"User correction|{question}|{state.BuildFingerprint}");
        var gap = state.Gaps.First(item => item.Id == gapId); await SaveAsync(root, state, token); return gap;
    }

    public async Task<BorinAcademySnapshot> MarkActivityAsync(string root, string training, string task, string model, bool running, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token); state.CurrentTraining = Limit(training, 160); state.CurrentTask = Limit(task, 300); state.CurrentModel = Limit(model, 120); state.IsTraining = running; await SaveAsync(root, state, token); return await SnapshotAsync(root, state, token);
    }

    public async Task<BorinAcademySnapshot> GetSnapshotAsync(string root, CancellationToken token = default)
    {
        var state = await LoadAsync(root, token);
        await InvalidateIfStaleAsync(root, state, token);
        foreach (var conflict in await _learning.GetConflictsAsync(token))
            if (conflict.Status != "Resolved")
                await AddGapUnsafeAsync(state, conflict.Subject, $"Resolve knowledge conflict {conflict.Id}.",
                    $"Expected: {conflict.Expected}; observed: {conflict.Observed}", "Conflicting authoritative/runtime evidence",
                    "Compare imported data, decoded/native behavior, RealmGate source, tests, and controlled Test Realm observations.", "High", token);
        await SaveAsync(root, state, token);
        return await SnapshotAsync(root, state, token);
    }

    private async Task<BorinAcademySnapshot> SnapshotAsync(string root, AcademyDocument state, CancellationToken token)
    {
        var conflicts = await _learning.GetConflictsAsync(token);
        var learning = await _learning.GetStatusAsync(token);
        var subjects = AllSubjects().Select(subject =>
        {
            var attempts = state.Attempts.Where(a => a.Subject.Equals(subject.Subject, StringComparison.OrdinalIgnoreCase) &&
                                                     a.BuildFingerprint == state.BuildFingerprint &&
                                                     BelongsToDomain(a, subject.Domain)).ToArray();
            var gaps = state.Gaps.Count(g => g.Subject.Equals(subject.Subject, StringComparison.OrdinalIgnoreCase) && g.Status != BorinKnowledgeGapStatus.Resolved);
            return new BorinAcademySubjectProgress(subject.Domain.ToString(), subject.Subject, attempts.Length, attempts.Count(a => a.Passed), attempts.Count(a => !a.Passed),
                attempts.Where(a => a.Passed).Select(a => a.EvidenceLocation).Distinct(StringComparer.OrdinalIgnoreCase).Count(), gaps,
                conflicts.Count(c => c.Subject.Contains(subject.Subject, StringComparison.OrdinalIgnoreCase) && c.Status != "Resolved"),
                attempts.Length == 0 ? null : attempts.Average(a => a.Score.Total), attempts.Length == 0 ? null : attempts.Max(a => a.TestedUtc));
        }).ToArray();
        return new(subjects, learning.Observations + subjects.Sum(s => s.VerifiedFacts), state.Gaps.Count(g => g.Status != BorinKnowledgeGapStatus.Resolved),
            conflicts.Count(c => c.Status != "Resolved"), state.Gaps.Count(g => g.Status != BorinKnowledgeGapStatus.Resolved && g.Missing.Contains("confidence", StringComparison.OrdinalIgnoreCase)),
            state.CurrentTraining, state.CurrentTask, state.CurrentModel, state.UltraEscalations, state.IsTraining, state.TrainWhileIdle, state.IdleRequestsPerMinute, state.BuildFingerprint, PathFor(root));
    }

    private async Task InvalidateIfStaleAsync(string root, AcademyDocument state, CancellationToken token)
    {
        var status = await _knowledge.GetStatusAsync(root, token);
        var major = await _knowledge.GetMajorMudStatusAsync(root, token);
        var fingerprint = $"{status.UpdatedUtc:O}|{status.IndexedFiles}|{status.IndexedChunks}|{major.UpdatedUtc:O}|{major.Records}|{major.Relationships}";
        if (fingerprint == state.BuildFingerprint) return;
        state.BuildFingerprint = fingerprint;
        state.CurrentTask = "Knowledge changed; prior mastery attempts were retained historically but excluded from current scores.";
    }

    private async Task<BorinKnowledgeGap> UpdateGapAsync(string root, string id, BorinKnowledgeGapStatus status, string evidence, CancellationToken token)
    {
        var state = await LoadAsync(root, token); var index = state.Gaps.FindIndex(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new KeyNotFoundException($"Academy gap '{id}' was not found.");
        var gap = state.Gaps[index] with { Status = status, UpdatedUtc = DateTimeOffset.UtcNow, ResolutionEvidence = Limit(evidence, 800) };
        state.Gaps[index] = gap; await SaveAsync(root, state, token); return gap;
    }

    private static Task AddGapUnsafeAsync(AcademyDocument state, string subject, string question, string known, string missing, string test, string priority, CancellationToken token, string domain = "")
    {
        token.ThrowIfCancellationRequested();
        var identity = string.IsNullOrWhiteSpace(domain) ? $"{subject}|{question}|{state.BuildFingerprint}" : $"{domain}|{subject}|{question}|{state.BuildFingerprint}";
        var id = Id("gap", identity); var now = DateTimeOffset.UtcNow;
        if (state.Gaps.Any(g => g.Id == id && g.Status != BorinKnowledgeGapStatus.Resolved)) return Task.CompletedTask;
        state.Gaps.Add(new(id, Limit(subject, 160), Limit(question, 500), Limit(known, 1200), Limit(missing, 300), Limit(test, 500), priority,
            BorinKnowledgeGapStatus.Open, state.BuildFingerprint, now, now, Domain: Limit(domain, 40))); return Task.CompletedTask;
    }

    private async Task<AcademyDocument> LoadAsync(string root, CancellationToken token)
    {
        await _gate.WaitAsync(token); try
        {
            var path = PathFor(root); if (!File.Exists(path)) return Default(); await using var stream = File.OpenRead(path);
            var state = await JsonSerializer.DeserializeAsync<AcademyDocument>(stream, Json, token) ?? Default();
            if (state.Version < 2)
            {
                var now = DateTimeOffset.UtcNow;
                state.Gaps = state.Gaps.Select(g => g.KnownEvidence == "No qualifying evidence retrieved." && g.Missing == "Authoritative exact/relationship evidence"
                    ? g with { Status = BorinKnowledgeGapStatus.Resolved, UpdatedUtc = now, ResolutionEvidence = "Academy v2 replaced the prematurely bounded specialized search with full-subject hybrid fallback." }
                    : g).ToList();
                state.Version = 2;
            }
            if (state.Version < 3)
            {
                var now = DateTimeOffset.UtcNow;
                state.Attempts = state.Attempts.Select(a => a with
                {
                    BuildFingerprint = $"academy-v2-obsolete|{a.BuildFingerprint}"
                }).ToList();
                state.Gaps = state.Gaps.Select(g =>
                    g.Status != BorinKnowledgeGapStatus.Resolved &&
                    !g.Subject.Equals("User correction", StringComparison.OrdinalIgnoreCase) &&
                    !g.Question.StartsWith("Resolve knowledge conflict", StringComparison.OrdinalIgnoreCase)
                        ? g with
                        {
                            Status = BorinKnowledgeGapStatus.Resolved,
                            UpdatedUtc = now,
                            ResolutionEvidence = "Academy v3 invalidated this gap because the former evaluator could pair a curriculum subject with unrelated evidence. Retest against subject-specific authority."
                        }
                        : g).ToList();
                state.CurrentTask = "Academy v3 corrected subject/evidence pairing; earlier scores are historical and excluded until retested.";
                state.Version = 3;
            }
            if (state.Version < 4)
            {
                var now = DateTimeOffset.UtcNow;
                var supersededSubjects = state.Attempts
                    .Where(a => a.BuildFingerprint == state.BuildFingerprint &&
                                a.Model.StartsWith("Direct GGUF", StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.Subject)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                state.Attempts = state.Attempts.Select(a =>
                    a.BuildFingerprint == state.BuildFingerprint &&
                    a.Model.StartsWith("Direct GGUF", StringComparison.OrdinalIgnoreCase)
                        ? a with { BuildFingerprint = $"academy-v3-local-routing-obsolete|{a.BuildFingerprint}" }
                        : a).ToList();
                state.Gaps = state.Gaps.Select(g =>
                    g.BuildFingerprint == state.BuildFingerprint &&
                    g.Status != BorinKnowledgeGapStatus.Resolved &&
                    g.Missing.Equals(BorinAcademyFailureCause.Hallucination.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    supersededSubjects.Contains(g.Subject)
                        ? g with
                        {
                            Status = BorinKnowledgeGapStatus.Resolved,
                            UpdatedUtc = now,
                            ResolutionEvidence = "Academy v4 superseded this local-GGUF evaluation after routing evidence-based Academy questions through NVIDIA Lightning/Ultra. Retest against the same authority."
                        }
                        : g).ToList();
                state.CurrentTask = "Academy v4 routes evidence questions through Lightning/Ultra; superseded local-GGUF scores are historical until retested.";
                state.Version = 4;
            }
            if (state.Version < 5)
            {
                state.Attempts = state.Attempts.Select(a =>
                    a.BuildFingerprint == state.BuildFingerprint &&
                    string.IsNullOrWhiteSpace(a.Domain) &&
                    a.Subject.Equals("Commands", StringComparison.OrdinalIgnoreCase)
                        ? a with { BuildFingerprint = $"academy-v4-ambiguous-domain-obsolete|{a.BuildFingerprint}" }
                        : a).ToList();
                state.CurrentTask = "Academy v5 separated MajorMUD Commands from RealmGate Commands; ambiguous legacy command scores require an honest retest.";
                state.Version = 5;
            }
            if (state.Version < 6)
            {
                var now = DateTimeOffset.UtcNow;
                state.Gaps = state.Gaps.Select(g =>
                    g.Status != BorinKnowledgeGapStatus.Resolved &&
                    g.KnownEvidence.Equals("No qualifying evidence retrieved.", StringComparison.OrdinalIgnoreCase) &&
                    g.Missing.Equals("Authoritative exact/relationship evidence", StringComparison.OrdinalIgnoreCase) &&
                    BorinAcademyCurriculum.Reforged.Contains(g.Subject, StringComparer.OrdinalIgnoreCase)
                        ? g with
                        {
                            Status = BorinKnowledgeGapStatus.Resolved,
                            UpdatedUtc = now,
                            ResolutionEvidence = "Academy v6 reclassified RealmGate question-generation misses as index/retrieval health rather than permanent knowledge gaps."
                        }
                        : g).ToList();
                state.CurrentTask = "Academy v6 removed false RealmGate question-generation gaps; only failed answers and genuine authority gaps remain open.";
                state.Version = Version;
            }
            return state;
        }
        finally { _gate.Release(); }
    }
    private async Task SaveAsync(string root, AcademyDocument state, CancellationToken token)
    {
        await _gate.WaitAsync(token); try { var path = PathFor(root); Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(state, Json), token); File.Move(temp, path, true); }
        finally { _gate.Release(); }
    }
    private static AcademyDocument Default() => new() { Version = Version };
    private static string PathFor(string root) => Path.Combine(Path.GetFullPath(root), "AI", "Memory", "Knowledge", "borin-academy.json");
    private static IEnumerable<(BorinAcademyDomain Domain, string Subject)> AllSubjects() =>
        BorinAcademyCurriculum.MajorMud.Select(s => (BorinAcademyDomain.MajorMud, s)).Concat(BorinAcademyCurriculum.Reforged.Select(s => (BorinAcademyDomain.Reforged, s)));
    private static IEnumerable<(BorinAcademyDomain Domain, string Subject)> SelectSubjects(string scope, AcademyDocument state)
    {
        var all = AllSubjects();
        if (scope.Contains("MajorMUD", StringComparison.OrdinalIgnoreCase)) all = all.Where(s => s.Domain == BorinAcademyDomain.MajorMud);
        else if (scope.Contains("RealmGate", StringComparison.OrdinalIgnoreCase) || scope.Contains("Reforged", StringComparison.OrdinalIgnoreCase)) all = all.Where(s => s.Domain == BorinAcademyDomain.Reforged);
        else if (scope.Contains("gap", StringComparison.OrdinalIgnoreCase)) all = all.Where(s => state.Gaps.Any(g => g.Subject.Equals(s.Subject, StringComparison.OrdinalIgnoreCase) && g.Status != BorinKnowledgeGapStatus.Resolved));
        return all.OrderBy(s => state.Attempts.Count(a => a.Subject.Equals(s.Subject, StringComparison.OrdinalIgnoreCase) &&
                                                       a.BuildFingerprint == state.BuildFingerprint &&
                                                       BelongsToDomain(a, s.Domain)));
    }
    private static bool BelongsToDomain(BorinAcademyAttempt attempt, BorinAcademyDomain domain)
    {
        if (!string.IsNullOrWhiteSpace(attempt.Domain))
            return attempt.Domain.Equals(domain.ToString(), StringComparison.OrdinalIgnoreCase);
        // Every current curriculum label except Commands belongs to one domain only.
        return !attempt.Subject.Equals("Commands", StringComparison.OrdinalIgnoreCase) &&
               (domain == BorinAcademyDomain.MajorMud
                   ? BorinAcademyCurriculum.MajorMud.Contains(attempt.Subject, StringComparer.OrdinalIgnoreCase)
                   : BorinAcademyCurriculum.Reforged.Contains(attempt.Subject, StringComparer.OrdinalIgnoreCase));
    }
    private static string MajorMudFamily(string subject) => subject switch
    {
        "Commands" => "commands", "Rooms" => "rooms", "Navigation" => "exits", "Items" => "items",
        "Monsters" => "npcs", "Spells" => "spells", "Classes" => "classes", "Races" => "races",
        "Combat" => "abilities", "Quests" => "quests", "Scripts" => "scripts", "Triggers" => "triggers",
        "Economy" => "economy_rules", "Timers" => "status_effects", "Resets" => "spawns", _ => "abilities",
    };
    private static string ReforgedQuery(string subject) => subject switch
    {
        "Architecture" => "ServiceRegistry",
        "Server" => "ServerRuntimeHost",
        "Runtime" => "RuntimeCommandSystem",
        "Database" => "RuntimeCharacterPersistence",
        "Importers" => "ImporterApplication",
        "Editor" => "EditorViewModel",
        "Client" => "ClientSessionService",
        "Librarium" => "LibrariumCatalogReader",
        "Networking" => "TelnetConnection",
        "Persistence" => "RuntimeCharacterPersistence",
        "Commands" => "RuntimeCommandSystem",
        "Testing" => "BorinCharacterTestHarness",
        "Build System" => "RealmGate.App.csproj",
        "Borin" => "BorinKnowledgeCore",
        "Deployment" => "PublishDir",
        _ => subject,
    };
    private static bool IsSubjectAuthority(BorinAcademyDomain domain, BorinKnowledgeEvidenceItem item)
    {
        var path = item.RelativePath.Replace('\\', '/');
        if (domain == BorinAcademyDomain.MajorMud)
            return path.StartsWith("Data/Classic/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Librarium/", StringComparison.OrdinalIgnoreCase);
        return !path.StartsWith("Data/Classic/", StringComparison.OrdinalIgnoreCase) &&
               (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Docs/", StringComparison.OrdinalIgnoreCase));
    }
    private static IEnumerable<string> Significant(string text) => Terms.Matches(text).Select(m => m.Value).Where(v => v.Length > 3 && !Stop.Contains(v));
    private static readonly HashSet<string> Stop = new(["this", "that", "with", "from", "string", "public", "private", "return", "record", "source", "evidence"], StringComparer.OrdinalIgnoreCase);
    private static bool DetectHallucination(string answer, IReadOnlyList<BorinKnowledgeEvidenceItem> evidence)
    {
        var claimedPaths = Regex.Matches(answer, @"[A-Za-z0-9_.-]+(?:[/\\][A-Za-z0-9_.-]+)+\.(?:cs|xaml|json|md)").Select(m => m.Value.Replace('\\', '/')).ToArray();
        return claimedPaths.Any(path => !evidence.Any(item => item.RelativePath.Replace('\\', '/').EndsWith(path, StringComparison.OrdinalIgnoreCase)));
    }
    private static string Recommendation(BorinAcademyFailureCause cause) => cause switch { BorinAcademyFailureCause.BadRetrieval => "Reindex affected authority and test exact/relationship retrieval.", BorinAcademyFailureCause.Hallucination => "Validate every claimed path/ID against local authority before answering.", BorinAcademyFailureCause.WrongRelationshipTraversal => "Trace canonical relationships and verify each hop.", _ => "Retrieve stronger evidence and retest the question." };
    private static bool LooksVerified(string text) => (text.Contains("PASS", StringComparison.OrdinalIgnoreCase) || text.Contains("verified", StringComparison.OrdinalIgnoreCase)) && !text.Contains("0 passed", StringComparison.OrdinalIgnoreCase);
    private static IReadOnlyList<string> ExtractEntityIds(string text) => Regex.Matches(text, @"\b(?:room\.map\d+\.\d+|item\.\d+|npc\.\d+|spell\.\d+|quest\.\d+)\b", RegexOptions.IgnoreCase).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
    private static IEnumerable<string> Glob(string root, string pattern)
    {
        var normalized = pattern.Replace('\\', '/'); var marker = normalized.IndexOf("/**/", StringComparison.Ordinal); if (marker < 0) yield break;
        var baseDir = Path.Combine(root, normalized[..marker].Replace('/', Path.DirectorySeparatorChar)); if (!Directory.Exists(baseDir)) yield break;
        var filePattern = normalized[(marker + 4)..]; foreach (var file in Directory.EnumerateFiles(baseDir, filePattern, SearchOption.AllDirectories)) yield return file;
    }
    private static string Id(string prefix, string value) => prefix + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant())))[..12].ToLowerInvariant();
    private static string Limit(string? value, int max) { value ??= string.Empty; return value.Length <= max ? value : value[..max] + "…"; }

    private sealed class AcademyDocument
    {
        public int Version { get; set; }
        public string BuildFingerprint { get; set; } = string.Empty;
        public List<BorinAcademyAttempt> Attempts { get; set; } = [];
        public List<BorinKnowledgeGap> Gaps { get; set; } = [];
        public List<string> ReplayedEvidence { get; set; } = [];
        public string CurrentTraining { get; set; } = "Idle";
        public string CurrentTask { get; set; } = "No Academy session has run.";
        public string CurrentModel { get; set; } = "Local";
        public int UltraEscalations { get; set; }
        public bool IsTraining { get; set; }
        public bool TrainWhileIdle { get; set; }
        public int IdleRequestsPerMinute { get; set; } = 5;
    }
}

