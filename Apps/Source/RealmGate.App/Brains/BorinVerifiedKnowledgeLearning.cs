using System.IO;
using System.Text.Json;

namespace RealmGate.App.Brains;

public sealed record BorinRuntimeKnowledgeObservation(
    string Subject,
    string Observed,
    string Server,
    string World,
    string Character,
    string Room,
    string EvidencePath,
    DateTimeOffset Timestamp,
    IReadOnlyList<string>? RelatedEntityIds = null);

public sealed record BorinKnowledgeInvestigation(
    string Id,
    string Objective,
    string Result,
    string Evidence,
    string Status,
    IReadOnlyList<string> RelatedEntityIds,
    DateTimeOffset StartedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record BorinKnowledgeConflict(
    string Id,
    string Subject,
    string Expected,
    string Observed,
    string ExpectedSource,
    string ObservationSource,
    int ControlledAttempts,
    string Status,
    DateTimeOffset UpdatedUtc,
    string ResolutionNote = "",
    string ResolutionEvidence = "");

public sealed record BorinProceduralKnowledge(
    string Id,
    string Name,
    IReadOnlyList<string> Steps,
    string VerificationEvidence,
    DateTimeOffset VerifiedUtc);

public sealed record BorinVerifiedLearningStatus(int Observations, int Conflicts, int Procedures, string StoragePath, int Investigations = 0);

public interface IBorinVerifiedKnowledgeLearning
{
    Task RecordObservationAsync(BorinRuntimeKnowledgeObservation observation, CancellationToken token = default);
    Task<BorinKnowledgeConflict?> RecordComparisonAsync(string subject, string expected, string observed,
        string expectedSource, string observationSource, int controlledAttempts, CancellationToken token = default);
    Task<BorinKnowledgeConflict?> ObserveComparisonAsync(string subject, string expected, string observed,
        string expectedSource, string observationSource, int requiredAttempts = 3, CancellationToken token = default);
    Task<BorinProceduralKnowledge> PromoteProcedureAsync(string name, IReadOnlyList<string> steps,
        bool verificationPassed, string verificationEvidence, CancellationToken token = default);
    Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> SearchAsync(string query, int maximumResults = 8, CancellationToken token = default);
    Task<BorinVerifiedLearningStatus> GetStatusAsync(CancellationToken token = default);
    Task<IReadOnlyList<BorinKnowledgeConflict>> GetConflictsAsync(CancellationToken token = default);
    Task<BorinKnowledgeConflict> ResolveConflictAsync(string id, string resolution, string evidence, CancellationToken token = default);
    Task<BorinKnowledgeInvestigation> RecordInvestigationAsync(string objective, string result, string evidence,
        bool verificationPassed, IReadOnlyList<string>? relatedEntityIds = null, CancellationToken token = default);
    Task<IReadOnlyList<BorinKnowledgeInvestigation>> GetInvestigationsAsync(CancellationToken token = default);
}

/// <summary>Durable observations, conflicts, and verified procedures. It never overwrites generated authority.</summary>
public sealed class BorinVerifiedKnowledgeLearning : IBorinVerifiedKnowledgeLearning
{
    private const int MaximumObservations = 2000;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public BorinVerifiedKnowledgeLearning(string repositoryRoot) =>
        _path = Path.Combine(Path.GetFullPath(repositoryRoot), "AI", "Memory", "Knowledge", "borin-verified-learning.json");

    public async Task RecordObservationAsync(BorinRuntimeKnowledgeObservation observation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.IsNullOrWhiteSpace(observation.Subject) || string.IsNullOrWhiteSpace(observation.Observed) ||
            string.IsNullOrWhiteSpace(observation.EvidencePath)) return;
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            document.Observations.Add(observation with
            {
                Subject = Limit(observation.Subject.Trim(), 240),
                Observed = Limit(observation.Observed.Trim(), 800),
                EvidencePath = Limit(observation.EvidencePath.Trim(), 500),
                RelatedEntityIds = NormalizeEntityIds(observation.RelatedEntityIds),
            });
            document.Observations = document.Observations.OrderByDescending(item => item.Timestamp).Take(MaximumObservations).ToList();
            await SaveAsync(document, token);
        }
        finally { _gate.Release(); }
    }

    public async Task<BorinKnowledgeConflict?> RecordComparisonAsync(string subject, string expected, string observed,
        string expectedSource, string observationSource, int controlledAttempts, CancellationToken token = default)
    {
        if (string.Equals(expected?.Trim(), observed?.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(observed) ||
            string.IsNullOrWhiteSpace(expectedSource) || string.IsNullOrWhiteSpace(observationSource) || controlledAttempts < 1) return null;
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            var id = "conflict." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(subject.Trim().ToLowerInvariant())))[..12].ToLowerInvariant();
            var conflict = new BorinKnowledgeConflict(id, Limit(subject, 240), Limit(expected, 800), Limit(observed, 800),
                Limit(expectedSource, 500), Limit(observationSource, 500), controlledAttempts, "Investigation Required", DateTimeOffset.UtcNow);
            document.Conflicts.RemoveAll(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            document.Conflicts.Add(conflict);
            await SaveAsync(document, token);
            return conflict;
        }
        finally { _gate.Release(); }
    }

    public async Task<BorinKnowledgeConflict?> ObserveComparisonAsync(string subject, string expected, string observed,
        string expectedSource, string observationSource, int requiredAttempts = 3, CancellationToken token = default)
    {
        if (string.Equals(expected?.Trim(), observed?.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(observed) ||
            string.IsNullOrWhiteSpace(expectedSource) || string.IsNullOrWhiteSpace(observationSource)) return null;
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            var key = $"{subject.Trim()}|{expected.Trim()}|{observed.Trim()}";
            var pending = document.PendingComparisons.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (pending is null)
            {
                pending = new PendingComparison { Key = key, Subject = subject.Trim(), Expected = expected.Trim(), Observed = observed.Trim(), ExpectedSource = expectedSource.Trim() };
                document.PendingComparisons.Add(pending);
            }
            pending.Attempts++;
            pending.LastObservationSource = observationSource.Trim();
            pending.UpdatedUtc = DateTimeOffset.UtcNow;
            var threshold = Math.Clamp(requiredAttempts, 2, 20);
            BorinKnowledgeConflict? conflict = null;
            if (pending.Attempts >= threshold)
            {
                var id = "conflict." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(subject.Trim().ToLowerInvariant())))[..12].ToLowerInvariant();
                conflict = new BorinKnowledgeConflict(id, Limit(subject, 240), Limit(expected, 800), Limit(observed, 800),
                    Limit(expectedSource, 500), Limit(observationSource, 500), pending.Attempts, "Investigation Required", DateTimeOffset.UtcNow);
                document.Conflicts.RemoveAll(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                document.Conflicts.Add(conflict);
            }
            await SaveAsync(document, token);
            return conflict;
        }
        finally { _gate.Release(); }
    }

    public async Task<BorinProceduralKnowledge> PromoteProcedureAsync(string name, IReadOnlyList<string> steps,
        bool verificationPassed, string verificationEvidence, CancellationToken token = default)
    {
        if (!verificationPassed || string.IsNullOrWhiteSpace(verificationEvidence) || steps.Count == 0)
            throw new InvalidOperationException("Procedural knowledge requires a successful verification and evidence.");
        var id = "procedure." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant())))[..12].ToLowerInvariant();
        var procedure = new BorinProceduralKnowledge(id, Limit(name, 160), steps.Where(step => !string.IsNullOrWhiteSpace(step)).Select(step => Limit(step, 400)).Take(30).ToArray(),
            Limit(verificationEvidence, 800), DateTimeOffset.UtcNow);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            document.Procedures.RemoveAll(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            document.Procedures.Add(procedure);
            await SaveAsync(document, token);
            return procedure;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<BorinKnowledgeEvidenceItem>> SearchAsync(string query, int maximumResults = 8, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var results = new List<BorinKnowledgeEvidenceItem>();
            foreach (var item in document.Observations.Where(item => terms.Any(term => $"{item.Subject} {item.Observed}".Contains(term, StringComparison.OrdinalIgnoreCase))))
                results.Add(Evidence(item.EvidencePath, $"Runtime observation: {item.Subject} — {item.Observed} ({item.Server}/{item.World}, {item.Timestamp:O}){EntityText(item.RelatedEntityIds)}", "observed at runtime", 90));
            foreach (var item in document.Conflicts.Where(item => terms.Any(term => $"{item.Subject} {item.Expected} {item.Observed}".Contains(term, StringComparison.OrdinalIgnoreCase))))
                results.Add(Evidence(item.ObservationSource, $"KNOWLEDGE CONFLICT {item.Id}: {item.Subject}; expected={item.Expected}; observed={item.Observed}; attempts={item.ControlledAttempts}; status={item.Status}" +
                    (string.IsNullOrWhiteSpace(item.ResolutionNote) ? string.Empty : $"; resolution={item.ResolutionNote}; resolution evidence={item.ResolutionEvidence}"), "expected authority conflicts with observation", 140));
            foreach (var item in document.Procedures.Where(item => terms.Any(term => $"{item.Name} {string.Join(' ', item.Steps)}".Contains(term, StringComparison.OrdinalIgnoreCase))))
                results.Add(Evidence(_path, $"Verified procedure: {item.Name}\n{string.Join("\n", item.Steps.Select((step, i) => $"{i + 1}. {step}"))}\nVerification: {item.VerificationEvidence}", "verified procedural memory", 110));
            foreach (var item in document.Investigations.Where(item => terms.Any(term => $"{item.Objective} {item.Result} {item.Evidence} {string.Join(' ', item.RelatedEntityIds)}".Contains(term, StringComparison.OrdinalIgnoreCase))))
                results.Add(Evidence(_path, $"Investigation {item.Id}: {item.Objective}; result={item.Result}; status={item.Status}; evidence={item.Evidence}{EntityText(item.RelatedEntityIds)}", "evidence-backed investigation episode", item.Status == "Verified" ? 125 : 75));
            return results.OrderByDescending(item => item.Score).Take(Math.Clamp(maximumResults, 1, 20)).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<BorinVerifiedLearningStatus> GetStatusAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try { var d = await LoadAsync(token); return new(d.Observations.Count, d.Conflicts.Count, d.Procedures.Count, _path, d.Investigations.Count); }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<BorinKnowledgeConflict>> GetConflictsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try { return (await LoadAsync(token)).Conflicts.OrderByDescending(item => item.UpdatedUtc).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task<BorinKnowledgeConflict> ResolveConflictAsync(string id, string resolution, string evidence, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(resolution) || string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Conflict ID, resolution, and verification evidence are required.");
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            var index = document.Conflicts.FindIndex(item => item.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"Knowledge conflict '{id}' was not found.");
            var current = document.Conflicts[index];
            var resolved = current with
            {
                Status = "Resolved",
                UpdatedUtc = DateTimeOffset.UtcNow,
                ResolutionNote = Limit(resolution.Trim(), 800),
                ResolutionEvidence = Limit(evidence.Trim(), 500),
            };
            document.Conflicts[index] = resolved;
            await SaveAsync(document, token);
            return resolved;
        }
        finally { _gate.Release(); }
    }

    public async Task<BorinKnowledgeInvestigation> RecordInvestigationAsync(string objective, string result, string evidence,
        bool verificationPassed, IReadOnlyList<string>? relatedEntityIds = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(objective) || string.IsNullOrWhiteSpace(result) || string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("An investigation requires an objective, result, and evidence.");
        var now = DateTimeOffset.UtcNow;
        var id = "investigation." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{objective.Trim().ToLowerInvariant()}|{now:O}")))[..12].ToLowerInvariant();
        var investigation = new BorinKnowledgeInvestigation(id, Limit(objective.Trim(), 300), Limit(result.Trim(), 1200),
            Limit(evidence.Trim(), 1000), verificationPassed ? "Verified" : "Unverified", NormalizeEntityIds(relatedEntityIds), now, now);
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadAsync(token);
            document.Investigations.Add(investigation);
            document.Investigations = document.Investigations.OrderByDescending(item => item.UpdatedUtc).Take(1000).ToList();
            await SaveAsync(document, token);
            return investigation;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<BorinKnowledgeInvestigation>> GetInvestigationsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try { return (await LoadAsync(token)).Investigations.OrderByDescending(item => item.UpdatedUtc).ToArray(); }
        finally { _gate.Release(); }
    }

    private static BorinKnowledgeEvidenceItem Evidence(string path, string text, string relationship, double score) =>
        new(path, 1, 1, BorinKnowledgeAuthority.Runtime, text, score, BorinKnowledgeConfidence.Proven,
            [new BorinKnowledgeProvenance("Runtime/Test Realm observation", path, 1, 1, "recorded observation")], relationship);
    private static IReadOnlyList<string> NormalizeEntityIds(IReadOnlyList<string>? values) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => Limit(value.Trim(), 160))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray() ?? [];
    private static string EntityText(IReadOnlyList<string>? values) => values is { Count: > 0 }
        ? $"; authoritative entities={string.Join(", ", values)}" : string.Empty;

    private async Task<LearningDocument> LoadAsync(CancellationToken token)
    {
        if (!File.Exists(_path)) return new LearningDocument();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<LearningDocument>(stream, Json, token) ?? new LearningDocument();
    }

    private async Task SaveAsync(LearningDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, Json), token);
        File.Move(temporary, _path, true);
    }
    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";
    private sealed class LearningDocument
    {
        public List<BorinRuntimeKnowledgeObservation> Observations { get; set; } = [];
        public List<BorinKnowledgeConflict> Conflicts { get; set; } = [];
        public List<BorinProceduralKnowledge> Procedures { get; set; } = [];
        public List<BorinKnowledgeInvestigation> Investigations { get; set; } = [];
        public List<PendingComparison> PendingComparisons { get; set; } = [];
    }
    private sealed class PendingComparison
    {
        public string Key { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Observed { get; set; } = string.Empty;
        public string ExpectedSource { get; set; } = string.Empty;
        public string LastObservationSource { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
