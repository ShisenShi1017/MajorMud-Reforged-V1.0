using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeRankingMetric { Level, Wealth, Kills, Quests, PvPKills, PlaytimeMinutes, Deaths, CompletionCount }
public enum RuntimeRankingVisibility { Public, ParticipantsOnly, AdministratorOnly }
public enum RuntimeRankingResetPolicy { Never, Daily, Weekly, Monthly, Manual }
public enum RuntimeRankingSortDirection { Descending, Ascending }

public sealed record RuntimeRankingDefinition(
    string RankingId,
    RuntimeRankingMetric Metric,
    string OwnerSubsystem,
    RuntimeRankingVisibility Visibility,
    RuntimeRankingResetPolicy ResetPolicy,
    RuntimeRankingSortDirection SortDirection = RuntimeRankingSortDirection.Descending,
    bool ExcludeAdministrators = true,
    int MaximumEntries = 100,
    bool Enabled = true,
    bool Persist = true);

public sealed record RuntimeRankingActor(string ActorId, bool Authenticated, bool IsAdministrator, IReadOnlyCollection<string>? ParticipantRankingIds = null);
public sealed record RuntimeRankingSubject(string SubjectId, string DisplayName, bool IsAdministrator = false, bool Hidden = false);
public sealed record RuntimeRankingEntry(string SubjectId, string DisplayName, long Value, int Rank, bool Tied);
public sealed record RuntimeRankingSnapshot(string RankingId, long Revision, DateTimeOffset? LastResetAt, IReadOnlyList<RuntimeRankingEntry> Entries);
public sealed record RuntimeRankingResult(bool Success, string Code, RuntimeRankingSnapshot? Snapshot = null);

public sealed class RuntimeRankingsStatisticsService
{
    private const int SchemaVersion = 1;
    private readonly Dictionary<string, RuntimeRankingDefinition> _definitions;
    private readonly Dictionary<string, Dictionary<string, ScoreState>> _scores;
    private readonly Dictionary<string, DateTimeOffset?> _lastReset;
    private readonly Dictionary<string, long> _revisions;

    private sealed record ScoreState(RuntimeRankingSubject Subject, long Value);
    private sealed record PersistedState(int SchemaVersion, Dictionary<string, Dictionary<string, PersistedScore>> Scores, Dictionary<string, DateTimeOffset?> LastReset, Dictionary<string, long> Revisions);
    private sealed record PersistedScore(string DisplayName, long Value, bool IsAdministrator, bool Hidden);

    public RuntimeRankingsStatisticsService(IEnumerable<RuntimeRankingDefinition> definitions)
    {
        _definitions = new(StringComparer.OrdinalIgnoreCase);
        _scores = new(StringComparer.OrdinalIgnoreCase);
        _lastReset = new(StringComparer.OrdinalIgnoreCase);
        _revisions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.RankingId, definition)) throw new ArgumentException($"duplicate-ranking:{definition.RankingId}", nameof(definitions));
            _scores[definition.RankingId] = new(StringComparer.OrdinalIgnoreCase);
            _lastReset[definition.RankingId] = null;
            _revisions[definition.RankingId] = 0;
        }
    }

    public RuntimeRankingResult SetValue(string ownerSubsystem, string rankingId, RuntimeRankingSubject subject, long value)
    {
        var gate = ValidateMutation(ownerSubsystem, rankingId, subject);
        if (gate is not null) return gate;
        _scores[rankingId][subject.SubjectId] = new(subject, value);
        _revisions[rankingId]++;
        return new(true, "ranking-value-set", BuildSnapshot(_definitions[rankingId], new RuntimeRankingActor(ownerSubsystem, true, true)));
    }

    public RuntimeRankingResult Increment(string ownerSubsystem, string rankingId, RuntimeRankingSubject subject, long delta)
    {
        var gate = ValidateMutation(ownerSubsystem, rankingId, subject);
        if (gate is not null) return gate;
        var current = _scores[rankingId].TryGetValue(subject.SubjectId, out var state) ? state.Value : 0L;
        long next;
        try { next = checked(current + delta); }
        catch (OverflowException) { return new(false, "ranking-value-overflow"); }
        _scores[rankingId][subject.SubjectId] = new(subject, next);
        _revisions[rankingId]++;
        return new(true, "ranking-value-incremented", BuildSnapshot(_definitions[rankingId], new RuntimeRankingActor(ownerSubsystem, true, true)));
    }

    public RuntimeRankingResult Remove(string ownerSubsystem, string rankingId, string subjectId)
    {
        if (!_definitions.TryGetValue(rankingId, out var definition)) return new(false, "ranking-not-found");
        if (!string.Equals(definition.OwnerSubsystem, ownerSubsystem, StringComparison.OrdinalIgnoreCase)) return new(false, "ranking-owner-mismatch");
        if (string.IsNullOrWhiteSpace(subjectId)) return new(false, "ranking-subject-required");
        if (!_scores[rankingId].Remove(subjectId)) return new(false, "ranking-subject-not-found");
        _revisions[rankingId]++;
        return new(true, "ranking-subject-removed");
    }

    public RuntimeRankingResult Query(RuntimeRankingActor actor, string rankingId)
    {
        if (!actor.Authenticated) return new(false, "ranking-authentication-required");
        if (string.IsNullOrWhiteSpace(actor.ActorId)) return new(false, "ranking-actor-required");
        if (!_definitions.TryGetValue(rankingId, out var definition) || !definition.Enabled) return new(false, "ranking-not-found");
        if (!CanView(actor, definition)) return new(false, "ranking-access-denied");
        return new(true, "ranking-resolved", BuildSnapshot(definition, actor));
    }

    public RuntimeRankingResult Reset(RuntimeRankingActor actor, string rankingId, DateTimeOffset now)
    {
        if (!actor.Authenticated || !actor.IsAdministrator) return new(false, "ranking-reset-not-authorized");
        if (!_definitions.TryGetValue(rankingId, out var definition)) return new(false, "ranking-not-found");
        if (definition.ResetPolicy == RuntimeRankingResetPolicy.Never) return new(false, "ranking-reset-disabled");
        _scores[rankingId].Clear();
        _lastReset[rankingId] = now;
        _revisions[rankingId]++;
        return new(true, "ranking-reset", BuildSnapshot(definition, actor));
    }

    public IReadOnlyList<string> ValidateState()
    {
        var issues = new List<string>();
        foreach (var definition in _definitions.Values)
        {
            if (!_scores.ContainsKey(definition.RankingId)) issues.Add($"missing-score-state:{definition.RankingId}");
            if (!_revisions.ContainsKey(definition.RankingId)) issues.Add($"missing-revision:{definition.RankingId}");
        }
        return issues;
    }

    public void Save(string path)
    {
        var scores = new Dictionary<string, Dictionary<string, PersistedScore>>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in _definitions.Values.Where(d => d.Persist))
            scores[definition.RankingId] = _scores[definition.RankingId].ToDictionary(
                pair => pair.Key,
                pair => new PersistedScore(pair.Value.Subject.DisplayName, pair.Value.Value, pair.Value.Subject.IsAdministrator, pair.Value.Subject.Hidden),
                StringComparer.OrdinalIgnoreCase);
        var state = new PersistedState(SchemaVersion, scores,
            _lastReset.Where(p => _definitions[p.Key].Persist).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase),
            _revisions.Where(p => _definitions[p.Key].Persist).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase));
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }

    public bool Load(string path)
    {
        if (!File.Exists(path)) return true;
        try
        {
            var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(path));
            if (state is null || state.SchemaVersion != SchemaVersion) return false;
            foreach (var ranking in state.Scores)
            {
                if (!_definitions.TryGetValue(ranking.Key, out var definition) || !definition.Persist) continue;
                _scores[ranking.Key].Clear();
                foreach (var pair in ranking.Value)
                    _scores[ranking.Key][pair.Key] = new(new RuntimeRankingSubject(pair.Key, pair.Value.DisplayName, pair.Value.IsAdministrator, pair.Value.Hidden), pair.Value.Value);
            }
            foreach (var pair in state.LastReset) if (_lastReset.ContainsKey(pair.Key)) _lastReset[pair.Key] = pair.Value;
            foreach (var pair in state.Revisions) if (_revisions.ContainsKey(pair.Key)) _revisions[pair.Key] = pair.Value;
            return ValidateState().Count == 0;
        }
        catch { return false; }
    }

    private RuntimeRankingResult? ValidateMutation(string ownerSubsystem, string rankingId, RuntimeRankingSubject subject)
    {
        if (!_definitions.TryGetValue(rankingId, out var definition) || !definition.Enabled) return new(false, "ranking-not-found");
        if (!string.Equals(definition.OwnerSubsystem, ownerSubsystem, StringComparison.OrdinalIgnoreCase)) return new(false, "ranking-owner-mismatch");
        if (string.IsNullOrWhiteSpace(subject.SubjectId) || string.IsNullOrWhiteSpace(subject.DisplayName)) return new(false, "ranking-subject-invalid");
        return null;
    }

    private RuntimeRankingSnapshot BuildSnapshot(RuntimeRankingDefinition definition, RuntimeRankingActor actor)
    {
        var ordered = _scores[definition.RankingId].Values
            .Where(s => !(definition.ExcludeAdministrators && s.Subject.IsAdministrator))
            .Where(s => !s.Subject.Hidden || actor.IsAdministrator || string.Equals(s.Subject.SubjectId, actor.ActorId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => 0)
            .ThenBy(s => s.Subject.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ordered = definition.SortDirection == RuntimeRankingSortDirection.Descending
            ? ordered.OrderByDescending(s => s.Value).ThenBy(s => s.Subject.SubjectId, StringComparer.OrdinalIgnoreCase).ToList()
            : ordered.OrderBy(s => s.Value).ThenBy(s => s.Subject.SubjectId, StringComparer.OrdinalIgnoreCase).ToList();
        var entries = new List<RuntimeRankingEntry>();
        long? previousValue = null;
        var rank = 0;
        for (var index = 0; index < ordered.Count && entries.Count < definition.MaximumEntries; index++)
        {
            if (previousValue is null || ordered[index].Value != previousValue.Value) rank = index + 1;
            var tied = (index > 0 && ordered[index - 1].Value == ordered[index].Value) || (index + 1 < ordered.Count && ordered[index + 1].Value == ordered[index].Value);
            entries.Add(new(ordered[index].Subject.SubjectId, ordered[index].Subject.DisplayName, ordered[index].Value, rank, tied));
            previousValue = ordered[index].Value;
        }
        return new(definition.RankingId, _revisions[definition.RankingId], _lastReset[definition.RankingId], entries);
    }

    private static bool CanView(RuntimeRankingActor actor, RuntimeRankingDefinition definition) => definition.Visibility switch
    {
        RuntimeRankingVisibility.Public => true,
        RuntimeRankingVisibility.AdministratorOnly => actor.IsAdministrator,
        RuntimeRankingVisibility.ParticipantsOnly => actor.IsAdministrator || (actor.ParticipantRankingIds?.Contains(definition.RankingId, StringComparer.OrdinalIgnoreCase) ?? false),
        _ => false
    };

    private static void ValidateDefinition(RuntimeRankingDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.RankingId)) throw new ArgumentException("ranking-id-required");
        if (string.IsNullOrWhiteSpace(definition.OwnerSubsystem)) throw new ArgumentException($"ranking-owner-required:{definition.RankingId}");
        if (definition.MaximumEntries is < 1 or > 10_000) throw new ArgumentException($"ranking-maximum-entries-invalid:{definition.RankingId}");
    }
}
