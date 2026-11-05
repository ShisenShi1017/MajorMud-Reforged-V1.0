namespace RealmGate.Server;

public static class RuntimeCombatSpellPersistenceSmokeReport
{
    private sealed class Store : IRuntimeCombatSpellSnapshotStore
    {
        public RuntimeCombatSpellSnapshot? Value { get; private set; }
        public bool FailAfterCommit { get; set; }
        public bool FailOnRollback { get; set; }
        public void Prepare(RuntimeCombatSpellSnapshot snapshot) { }
        public void Commit(RuntimeCombatSpellSnapshot snapshot)
        {
            Value = snapshot;
            if (FailAfterCommit) throw new InvalidOperationException("PERSISTENCE_SMOKE_PARTIAL_COMMIT");
        }
        public void Rollback(RuntimeCombatSpellSnapshot snapshot)
        {
            if (FailOnRollback) throw new InvalidOperationException("PERSISTENCE_SMOKE_ROLLBACK_FAILURE");
            if (ReferenceEquals(Value, snapshot)) Value = null;
        }
        public RuntimeCombatSpellSnapshot? Load(string characterId) => Value?.CharacterId == characterId ? Value : null;
    }

    public static IReadOnlyDictionary<string, bool> Run()
    {
        var store = new Store();
        var policy = new RuntimeCombatSpellPersistencePolicy(
            "persistencepolicy.1", false, true, true, true, false, false, false, false, false,
            RuntimePersistenceDisposition.CleanupOnly, RuntimePersistenceDisposition.Drop,
            RuntimeResolutionConfidence.StronglyCorrelated, new[] { "evidence.persistence.fixture" }, "reject");
        var coordinator = new RuntimeCombatSpellPersistenceCoordinator(policy, store);
        var snapshot = new RuntimeCombatSpellSnapshot(
            "snapshot.1", "character.1", 100, RuntimePersistenceBoundary.Shutdown,
            null,
            new[] { new RuntimePersistedEffect("effect.1", "spell.1", "character.1", "character.1", 20, 1, 5) },
            new[] { new RuntimePersistedTimer("timer.1", "character.1", 30, "cooldown") },
            Array.Empty<RuntimePersistedTimer>(), Array.Empty<RuntimePersistedThreat>(), Array.Empty<RuntimePersistedAction>(), null,
            Array.Empty<string>(), Array.Empty<string>(), string.Empty, new[] { "evidence.snapshot.fixture" });
        var saved = coordinator.Save(snapshot);
        var recovered = coordinator.Recover("character.1", 110);
        var cleanup = coordinator.BuildBoundaryCleanup(RuntimePersistenceBoundary.Disconnect, "character.1");

        var retryStore = new Store { FailAfterCommit = true };
        var retryCoordinator = new RuntimeCombatSpellPersistenceCoordinator(policy, retryStore);
        var retrySnapshot = snapshot with { SnapshotId = "snapshot.2" };
        var partialFailureObserved = false;
        try { retryCoordinator.Save(retrySnapshot); }
        catch (InvalidOperationException ex) when (ex.Message == "PERSISTENCE_SMOKE_PARTIAL_COMMIT")
        {
            partialFailureObserved = true;
        }
        var rolledBackPartialWrite = retryStore.Value is null;
        retryStore.FailAfterCommit = false;
        var safeRetry = retryCoordinator.Save(retrySnapshot).SnapshotId == retrySnapshot.SnapshotId;

        var uncertainStore = new Store { FailAfterCommit = true, FailOnRollback = true };
        var uncertainCoordinator = new RuntimeCombatSpellPersistenceCoordinator(policy, uncertainStore);
        var uncertainSnapshot = snapshot with { SnapshotId = "snapshot.3" };
        var reportedUncertain = false;
        try { uncertainCoordinator.Save(uncertainSnapshot); }
        catch (AggregateException ex) when (ex.Message.Contains("PERSISTENCE_ROLLBACK_UNCERTAIN", StringComparison.Ordinal))
        {
            reportedUncertain = true;
        }
        var blockedUnsafeRetry = false;
        try { uncertainCoordinator.Save(uncertainSnapshot); }
        catch (InvalidOperationException ex) when (ex.Message == "PERSISTENCE_SNAPSHOT_DUPLICATE")
        {
            blockedUnsafeRetry = true;
        }

        return new Dictionary<string, bool>
        {
            ["integrity_hash"] = saved.IntegrityHash.Length == 64,
            ["effect_restored"] = recovered.RestoredSnapshot?.ActiveEffects.Count == 1,
            ["temporary_state_omitted"] = recovered.RestoredSnapshot?.Combat is null && recovered.RestoredSnapshot?.Threat.Count == 0,
            ["disconnect_cleanup"] = cleanup.Contains("interrupt_cast") && cleanup.Contains("detach_combat"),
            ["evidence"] = recovered.EvidenceReferences.Count > 0,
            ["partial_commit_rollback_and_safe_retry"] = partialFailureObserved && rolledBackPartialWrite && safeRetry,
            ["failed_rollback_is_uncertain_and_blocks_duplicate"] = reportedUncertain && blockedUnsafeRetry && uncertainStore.Value is not null
        };
    }
}
