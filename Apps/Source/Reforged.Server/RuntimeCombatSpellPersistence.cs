namespace Reforged.Server;

public enum RuntimePersistenceBoundary { Autosave, Logout, Disconnect, Shutdown, Restart, CrashRecovery }
public enum RuntimePersistenceDisposition { Persist, Drop, CleanupOnly }
public enum RuntimeRecoveryResult { Restored, Rejected, CleanupRequired }

public sealed record RuntimeCombatSpellPersistencePolicy(
    string PolicyId,
    bool PersistCombatInstances,
    bool PersistActiveEffects,
    bool PersistCooldowns,
    bool PersistRecoveryTimers,
    bool PersistThreat,
    bool PersistPendingActions,
    bool PersistCastingState,
    bool PersistSummons,
    bool PersistControlEffects,
    RuntimePersistenceDisposition DisconnectDisposition,
    RuntimePersistenceDisposition LogoutDisposition,
    RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimePersistedTimer(string TimerId, string OwnerId, long RemainingTicks, string TimerKind);
public sealed record RuntimePersistedEffect(string EffectId, string SpellId, string SourceId, string TargetId, long RemainingTicks, int StackCount, long NextTickOffset);
public sealed record RuntimePersistedCombatParticipant(string ActorId, string RoomId, string State, long NextActionOffset, int Initiative);
public sealed record RuntimePersistedCombatInstance(string CombatId, string RoomId, long Round, IReadOnlyList<RuntimePersistedCombatParticipant> Participants);
public sealed record RuntimePersistedThreat(string CombatId, string ObserverId, string SourceId, long Value);
public sealed record RuntimePersistedAction(string ActionId, string CombatId, string ActorId, string Kind, long DueTickOffset, string State);
public sealed record RuntimePersistedCast(string CastId, string SpellId, string ActorId, IReadOnlyList<string> TargetIds, long CompletionOffset, string State);

public sealed record RuntimeCombatSpellSnapshot(
    string SnapshotId,
    string CharacterId,
    long CapturedWorldTick,
    RuntimePersistenceBoundary Boundary,
    RuntimePersistedCombatInstance? Combat,
    IReadOnlyList<RuntimePersistedEffect> ActiveEffects,
    IReadOnlyList<RuntimePersistedTimer> Cooldowns,
    IReadOnlyList<RuntimePersistedTimer> RecoveryTimers,
    IReadOnlyList<RuntimePersistedThreat> Threat,
    IReadOnlyList<RuntimePersistedAction> PendingActions,
    RuntimePersistedCast? CastingState,
    IReadOnlyList<string> SummonIds,
    IReadOnlyList<string> ControlEffectIds,
    string IntegrityHash,
    IReadOnlyList<string> EvidenceReferences);

public sealed record RuntimeCombatSpellRecoveryDecision(
    string SnapshotId,
    RuntimeRecoveryResult Result,
    RuntimeCombatSpellSnapshot? RestoredSnapshot,
    IReadOnlyList<string> CleanupRequests,
    IReadOnlyList<string> EvidenceReferences);

public interface IRuntimeCombatSpellSnapshotStore
{
    void Prepare(RuntimeCombatSpellSnapshot snapshot);
    void Commit(RuntimeCombatSpellSnapshot snapshot);
    void Rollback(RuntimeCombatSpellSnapshot snapshot);
    RuntimeCombatSpellSnapshot? Load(string characterId);
}

public sealed class RuntimeCombatSpellPersistenceCoordinator
{
    private readonly RuntimeCombatSpellPersistencePolicy _policy;
    private readonly IRuntimeCombatSpellSnapshotStore _store;
    private readonly HashSet<string> _committedSnapshots = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCombatSpellPersistenceCoordinator(RuntimeCombatSpellPersistencePolicy policy, IRuntimeCombatSpellSnapshotStore store)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ValidatePolicy(policy);
    }

    public RuntimeCombatSpellSnapshot Save(RuntimeCombatSpellSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);
        if (_policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis) throw new InvalidOperationException("PERSISTENCE_POLICY_NOT_PROMOTED");
        if (!_committedSnapshots.Add(snapshot.SnapshotId)) throw new InvalidOperationException("PERSISTENCE_SNAPSHOT_DUPLICATE");
        var normalized = ApplyPolicy(snapshot);
        var expectedHash = ComputeIntegrityHash(normalized with { IntegrityHash = string.Empty });
        normalized = normalized with { IntegrityHash = expectedHash };
        try
        {
            _store.Prepare(normalized);
            _store.Commit(normalized);
            return normalized;
        }
        catch
        {
            _store.Rollback(normalized);
            _committedSnapshots.Remove(snapshot.SnapshotId);
            throw;
        }
    }

    public RuntimeCombatSpellRecoveryDecision Recover(string characterId, long currentWorldTick)
    {
        RequireCanonical(characterId, @"^character\.\d+$", "PERSISTENCE_CHARACTER_ID_INVALID");
        if (currentWorldTick < 0) throw new InvalidOperationException("PERSISTENCE_WORLD_TICK_INVALID");
        var snapshot = _store.Load(characterId);
        if (snapshot is null)
            return new RuntimeCombatSpellRecoveryDecision("snapshot.0", RuntimeRecoveryResult.Rejected, null, Array.Empty<string>(), _policy.EvidenceReferences);
        ValidateSnapshot(snapshot);
        if (!snapshot.IntegrityHash.Equals(ComputeIntegrityHash(snapshot with { IntegrityHash = string.Empty }), StringComparison.OrdinalIgnoreCase))
            return new RuntimeCombatSpellRecoveryDecision(snapshot.SnapshotId, RuntimeRecoveryResult.Rejected, null, new[] { "discard_corrupt_snapshot" }, _policy.EvidenceReferences);
        if (snapshot.CapturedWorldTick > currentWorldTick)
            return new RuntimeCombatSpellRecoveryDecision(snapshot.SnapshotId, RuntimeRecoveryResult.Rejected, null, new[] { "discard_future_snapshot" }, _policy.EvidenceReferences);
        var cleanup = new List<string>();
        if (!_policy.PersistCombatInstances && snapshot.Combat is not null) cleanup.Add("cleanup_combat_instance");
        if (!_policy.PersistThreat && snapshot.Threat.Count > 0) cleanup.Add("cleanup_threat");
        if (!_policy.PersistPendingActions && snapshot.PendingActions.Count > 0) cleanup.Add("cleanup_pending_actions");
        if (!_policy.PersistCastingState && snapshot.CastingState is not null) cleanup.Add("cleanup_casting_state");
        var restored = ApplyPolicy(snapshot);
        return new RuntimeCombatSpellRecoveryDecision(snapshot.SnapshotId, cleanup.Count == 0 ? RuntimeRecoveryResult.Restored : RuntimeRecoveryResult.CleanupRequired, restored, cleanup, _policy.EvidenceReferences);
    }

    public IReadOnlyList<string> BuildBoundaryCleanup(RuntimePersistenceBoundary boundary, string characterId)
    {
        RequireCanonical(characterId, @"^character\.\d+$", "PERSISTENCE_CHARACTER_ID_INVALID");
        var disposition = boundary switch
        {
            RuntimePersistenceBoundary.Disconnect => _policy.DisconnectDisposition,
            RuntimePersistenceBoundary.Logout => _policy.LogoutDisposition,
            _ => RuntimePersistenceDisposition.Persist
        };
        return disposition switch
        {
            RuntimePersistenceDisposition.Persist => Array.Empty<string>(),
            RuntimePersistenceDisposition.CleanupOnly => new[] { "cancel_pending_actions", "interrupt_cast", "cleanup_threat", "detach_combat" },
            RuntimePersistenceDisposition.Drop => new[] { "cancel_pending_actions", "interrupt_cast", "remove_nonpersistent_effects", "cleanup_threat", "detach_combat", "discard_snapshot" },
            _ => throw new InvalidOperationException("PERSISTENCE_DISPOSITION_INVALID")
        };
    }

    private RuntimeCombatSpellSnapshot ApplyPolicy(RuntimeCombatSpellSnapshot s) => s with
    {
        Combat = _policy.PersistCombatInstances ? s.Combat : null,
        ActiveEffects = _policy.PersistActiveEffects ? s.ActiveEffects : Array.Empty<RuntimePersistedEffect>(),
        Cooldowns = _policy.PersistCooldowns ? s.Cooldowns : Array.Empty<RuntimePersistedTimer>(),
        RecoveryTimers = _policy.PersistRecoveryTimers ? s.RecoveryTimers : Array.Empty<RuntimePersistedTimer>(),
        Threat = _policy.PersistThreat ? s.Threat : Array.Empty<RuntimePersistedThreat>(),
        PendingActions = _policy.PersistPendingActions ? s.PendingActions : Array.Empty<RuntimePersistedAction>(),
        CastingState = _policy.PersistCastingState ? s.CastingState : null,
        SummonIds = _policy.PersistSummons ? s.SummonIds : Array.Empty<string>(),
        ControlEffectIds = _policy.PersistControlEffects ? s.ControlEffectIds : Array.Empty<string>(),
        EvidenceReferences = s.EvidenceReferences.Concat(_policy.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
    };

    private static string ComputeIntegrityHash(RuntimeCombatSpellSnapshot s)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static void ValidatePolicy(RuntimeCombatSpellPersistencePolicy p)
    {
        RequireCanonical(p.PolicyId, @"^persistencepolicy\.\d+$", "PERSISTENCE_POLICY_ID_INVALID");
        if (p.EvidenceReferences is null || p.EvidenceReferences.Count == 0) throw new InvalidOperationException("PERSISTENCE_EVIDENCE_REQUIRED");
        if (!p.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PERSISTENCE_SAFE_FALLBACK_INVALID");
    }

    private static void ValidateSnapshot(RuntimeCombatSpellSnapshot s)
    {
        RequireCanonical(s.SnapshotId, @"^snapshot\.\d+$", "PERSISTENCE_SNAPSHOT_ID_INVALID");
        RequireCanonical(s.CharacterId, @"^character\.\d+$", "PERSISTENCE_CHARACTER_ID_INVALID");
        if (s.CapturedWorldTick < 0) throw new InvalidOperationException("PERSISTENCE_WORLD_TICK_INVALID");
        if (s.EvidenceReferences is null || s.EvidenceReferences.Count == 0) throw new InvalidOperationException("PERSISTENCE_EVIDENCE_REQUIRED");
        if (s.ActiveEffects.Any(x => x.RemainingTicks < 0 || x.NextTickOffset < 0 || x.StackCount < 1)) throw new InvalidOperationException("PERSISTENCE_EFFECT_INVALID");
        if (s.Cooldowns.Concat(s.RecoveryTimers).Any(x => x.RemainingTicks < 0)) throw new InvalidOperationException("PERSISTENCE_TIMER_INVALID");
        if (s.PendingActions.Any(x => x.DueTickOffset < 0)) throw new InvalidOperationException("PERSISTENCE_ACTION_INVALID");
        if (s.CastingState is not null && s.CastingState.CompletionOffset < 0) throw new InvalidOperationException("PERSISTENCE_CAST_INVALID");
        foreach (var id in s.SummonIds) RequireCanonical(id, @"^npc\.\d+$", "PERSISTENCE_SUMMON_ID_INVALID");
        foreach (var id in s.ControlEffectIds) RequireCanonical(id, @"^effect\.\d+$", "PERSISTENCE_CONTROL_EFFECT_ID_INVALID");
    }

    private static void RequireCanonical(string value, string pattern, string error)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, pattern)) throw new InvalidOperationException(error);
    }
}
