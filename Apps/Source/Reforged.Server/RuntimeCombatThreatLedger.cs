namespace Reforged.Server;

public enum RuntimeCombatThreatEventKind
{
    PhysicalDamage,
    SpellDamage,
    Healing,
    Buff,
    Debuff,
    Taunt,
    Summon,
    CrowdControl,
    Dispel,
    Resurrection
}

public enum RuntimeCombatThreatCleanupReason
{
    Death,
    Defeat,
    Fled,
    RoomExit,
    Disconnected,
    Removed,
    CombatEnded
}

public sealed record RuntimeCombatThreatPolicy(
    string PolicyId,
    RuntimeCombatThreatEventKind EventKind,
    int MultiplierBasisPoints,
    int FlatThreat,
    long MaximumThreatPerMutation,
    bool AllowZeroMagnitude,
    IReadOnlyList<string> EvidenceReferences,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeCombatThreatMutationRequest(
    string MutationId,
    string CombatId,
    string SourceActorId,
    IReadOnlyList<string> ObserverNpcIds,
    RuntimeCombatThreatEventKind EventKind,
    long Magnitude,
    long WorldTick);

public sealed record RuntimeCombatLedgerThreatEntry(
    string CombatId,
    string ObserverNpcId,
    string SourceActorId,
    long Threat,
    long LastMutationTick);

public sealed record RuntimeCombatThreatMutation(
    string MutationId,
    string CombatId,
    string ObserverNpcId,
    string SourceActorId,
    RuntimeCombatThreatEventKind EventKind,
    long PreviousThreat,
    long Delta,
    long FinalThreat,
    long WorldTick,
    string PolicyId);

public sealed record RuntimeCombatThreatCleanupResult(
    string CombatId,
    string EntityId,
    RuntimeCombatThreatCleanupReason Reason,
    IReadOnlyList<RuntimeCombatLedgerThreatEntry> RemovedEntries);

public sealed class RuntimeCombatThreatLedger
{
    private readonly Dictionary<RuntimeCombatThreatEventKind, RuntimeCombatThreatPolicy> _policies;
    private readonly Dictionary<string, RuntimeCombatLedgerThreatEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedMutations = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedCleanup = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatThreatLedger(IEnumerable<RuntimeCombatThreatPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _policies = policies.ToDictionary(x => x.EventKind);
        if (_policies.Count == 0) throw new InvalidOperationException("COMBAT_THREAT_POLICY_REQUIRED");
        foreach (var policy in _policies.Values) ValidatePolicy(policy);
    }

    public IReadOnlyCollection<RuntimeCombatLedgerThreatEntry> Entries => _entries.Values
        .OrderBy(x => x.CombatId, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.ObserverNpcId, StringComparer.OrdinalIgnoreCase)
        .ThenByDescending(x => x.Threat)
        .ThenBy(x => x.SourceActorId, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<RuntimeCombatThreatMutation> Apply(RuntimeCombatThreatMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireCanonical(request.MutationId, "threatmutation.", "COMBAT_THREAT_MUTATION_ID_REQUIRED");
        RequireCanonical(request.CombatId, "combat.", "COMBAT_THREAT_COMBAT_ID_REQUIRED");
        RequireActor(request.SourceActorId);
        if (request.ObserverNpcIds is null || request.ObserverNpcIds.Count == 0)
            throw new InvalidOperationException("COMBAT_THREAT_OBSERVER_REQUIRED");
        if (request.ObserverNpcIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.ObserverNpcIds.Count)
            throw new InvalidOperationException("COMBAT_THREAT_DUPLICATE_OBSERVER");
        foreach (var observer in request.ObserverNpcIds)
            RequireCanonical(observer, "npc.", "COMBAT_THREAT_OBSERVER_NPC_ID_REQUIRED");
        if (request.Magnitude < 0) throw new InvalidOperationException("COMBAT_THREAT_NEGATIVE_MAGNITUDE");
        if (!_appliedMutations.Add(request.MutationId))
            throw new InvalidOperationException("COMBAT_THREAT_MUTATION_ALREADY_APPLIED");

        try
        {
            if (!_policies.TryGetValue(request.EventKind, out var policy))
                throw new InvalidOperationException("COMBAT_THREAT_POLICY_UNRESOLVED");
            if (request.Magnitude == 0 && !policy.AllowZeroMagnitude)
                throw new InvalidOperationException("COMBAT_THREAT_ZERO_MAGNITUDE_REJECTED");

            var scaled = checked((request.Magnitude * policy.MultiplierBasisPoints) / 10_000L);
            var delta = checked(scaled + policy.FlatThreat);
            if (delta < 0) throw new InvalidOperationException("COMBAT_THREAT_NEGATIVE_DELTA");
            if (delta > policy.MaximumThreatPerMutation) delta = policy.MaximumThreatPerMutation;

            var mutations = new List<RuntimeCombatThreatMutation>(request.ObserverNpcIds.Count);
            var snapshots = new Dictionary<string, RuntimeCombatLedgerThreatEntry?>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var observer in request.ObserverNpcIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var key = Key(request.CombatId, observer, request.SourceActorId);
                    snapshots[key] = _entries.TryGetValue(key, out var existing) ? existing : null;
                    var previous = existing?.Threat ?? 0;
                    var final = checked(previous + delta);
                    var entry = new RuntimeCombatLedgerThreatEntry(request.CombatId, observer, request.SourceActorId, final, request.WorldTick);
                    _entries[key] = entry;
                    mutations.Add(new RuntimeCombatThreatMutation(request.MutationId, request.CombatId, observer,
                        request.SourceActorId, request.EventKind, previous, delta, final, request.WorldTick, policy.PolicyId));
                }
                return mutations;
            }
            catch
            {
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.Value is null) _entries.Remove(snapshot.Key);
                    else _entries[snapshot.Key] = snapshot.Value;
                }
                throw;
            }
        }
        catch
        {
            _appliedMutations.Remove(request.MutationId);
            throw;
        }
    }

    public RuntimeCombatThreatCleanupResult Cleanup(string cleanupId, string combatId, string entityId,
        RuntimeCombatThreatCleanupReason reason)
    {
        RequireCanonical(cleanupId, "threatcleanup.", "COMBAT_THREAT_CLEANUP_ID_REQUIRED");
        RequireCanonical(combatId, "combat.", "COMBAT_THREAT_COMBAT_ID_REQUIRED");
        RequireActor(entityId);
        if (!_appliedCleanup.Add(cleanupId))
            throw new InvalidOperationException("COMBAT_THREAT_CLEANUP_ALREADY_APPLIED");

        var removed = _entries.Values
            .Where(x => x.CombatId.Equals(combatId, StringComparison.OrdinalIgnoreCase))
            .Where(x => reason == RuntimeCombatThreatCleanupReason.CombatEnded ||
                        x.ObserverNpcId.Equals(entityId, StringComparison.OrdinalIgnoreCase) ||
                        x.SourceActorId.Equals(entityId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ObserverNpcId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceActorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var entry in removed) _entries.Remove(Key(entry.CombatId, entry.ObserverNpcId, entry.SourceActorId));
        return new RuntimeCombatThreatCleanupResult(combatId, entityId, reason, removed);
    }

    public IReadOnlyList<RuntimeCombatLedgerThreatEntry> Inspect(string combatId, string observerNpcId)
    {
        RequireCanonical(combatId, "combat.", "COMBAT_THREAT_COMBAT_ID_REQUIRED");
        RequireCanonical(observerNpcId, "npc.", "COMBAT_THREAT_OBSERVER_NPC_ID_REQUIRED");
        return _entries.Values
            .Where(x => x.CombatId.Equals(combatId, StringComparison.OrdinalIgnoreCase) &&
                        x.ObserverNpcId.Equals(observerNpcId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Threat)
            .ThenBy(x => x.SourceActorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Key(string combatId, string observerNpcId, string sourceActorId) =>
        $"{combatId}|{observerNpcId}|{sourceActorId}";

    private static void ValidatePolicy(RuntimeCombatThreatPolicy policy)
    {
        RequireCanonical(policy.PolicyId, "threatpolicy.", "COMBAT_THREAT_POLICY_ID_REQUIRED");
        if (policy.MultiplierBasisPoints < 0) throw new InvalidOperationException("COMBAT_THREAT_MULTIPLIER_INVALID");
        if (policy.MaximumThreatPerMutation < 0) throw new InvalidOperationException("COMBAT_THREAT_CAP_INVALID");
        if (policy.EvidenceReferences is null || policy.EvidenceReferences.Count == 0)
            throw new InvalidOperationException("COMBAT_THREAT_EVIDENCE_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("COMBAT_THREAT_POLICY_NOT_PROMOTED");
        if (policy.Confidence == RuntimeResolutionConfidence.StronglyCorrelated && string.IsNullOrWhiteSpace(policy.SafeFallback))
            throw new InvalidOperationException("COMBAT_THREAT_FALLBACK_REQUIRED");
    }

    private static void RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !(
            value.StartsWith("character.", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("world.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("COMBAT_THREAT_ACTOR_ID_REQUIRED");
    }

    private static void RequireCanonical(string value, string prefix, string error)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(error);
    }
}
