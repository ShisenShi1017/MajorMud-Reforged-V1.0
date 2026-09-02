namespace Reforged.Server;

public static class RuntimeCombatSpellPersistenceSmokeReport
{
    private sealed class Store : IRuntimeCombatSpellSnapshotStore
    {
        public RuntimeCombatSpellSnapshot? Value { get; private set; }
        public void Prepare(RuntimeCombatSpellSnapshot snapshot) { }
        public void Commit(RuntimeCombatSpellSnapshot snapshot) => Value = snapshot;
        public void Rollback(RuntimeCombatSpellSnapshot snapshot) { if (ReferenceEquals(Value, snapshot)) Value = null; }
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
        return new Dictionary<string, bool>
        {
            ["integrity_hash"] = saved.IntegrityHash.Length == 64,
            ["effect_restored"] = recovered.RestoredSnapshot?.ActiveEffects.Count == 1,
            ["temporary_state_omitted"] = recovered.RestoredSnapshot?.Combat is null && recovered.RestoredSnapshot?.Threat.Count == 0,
            ["disconnect_cleanup"] = cleanup.Contains("interrupt_cast") && cleanup.Contains("detach_combat"),
            ["evidence"] = recovered.EvidenceReferences.Count > 0
        };
    }
}
