namespace Reforged.Server;

public static class RuntimeCombatSpellBindingSmoke
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var weaponBinding = new RuntimeCombatSpellDefinitionBinding(
            "binding.item.1622", RuntimeCombatSpellDefinitionKind.Weapon, "item.1622", "profile.item.1622",
            14, 33, "physical", null, null,
            new[] { "Data/Classic/items.json#item.1622", "Librarium/Objects/items/item_records.json#item.1622" },
            RuntimeResolutionConfidence.Proven, "Reject when canonical binding or profile is unavailable.");
        var profile = new RuntimeResolutionProfile(
            "profile.item.1622", RuntimeResolutionConfidence.Proven, "physical", null, null,
            14, 33, 0, 0, 0, 0, 0, 999, false, 100, 1, 1,
            weaponBinding.EvidenceReferences, weaponBinding.SafeFallback);
        var catalog = new RuntimeCombatSpellBindingCatalog(new[] { weaponBinding });
        var resolver = new RuntimeCombatSpellBoundResolver(catalog, new[] { profile }, new RuntimeCombatSpellResolutionEngine());
        var sink = new SmokeSink(100);
        var executor = new RuntimeCombatSpellAtomicExecutor(resolver, sink);
        var request = new RuntimeCombatSpellBoundRequest("resolution-0151", "combat-0151", 151, "character.1", "npc.1", "item.1622", RuntimeDamageDisposition.Damage, 0, 0, 0, 151);
        var committed = executor.Execute(request);
        var hpAfterCommit = sink.Value;
        sink.FailNext = true;
        var rolledBack = false;
        try { executor.Execute(request with { ResolutionId = "resolution-0151-rollback" }); }
        catch (InvalidOperationException) { rolledBack = sink.Value == hpAfterCommit; }
        var unboundRejected = false;
        try { resolver.Resolve(request with { DefinitionId = "item.999999" }); }
        catch (InvalidOperationException ex) { unboundRejected = ex.Message == "COMBAT_SPELL_DEFINITION_UNBOUND"; }
        return new Dictionary<string, bool>
        {
            ["canonical_weapon_binding"] = committed.Trace.DefinitionId == "item.1622",
            ["magnitude_uses_promoted_weapon_range"] = committed.Trace.FinalMagnitude is >= 14 and <= 33,
            ["mutation_committed_once"] = hpAfterCommit == 100 - committed.Trace.FinalMagnitude,
            ["sink_failure_rolls_back"] = rolledBack,
            ["unbound_definition_fails_closed"] = unboundRejected
        };
    }

    private sealed class SmokeSink : IRuntimeCombatSpellMutationSink
    {
        public int Value { get; private set; }
        public bool FailNext { get; set; }
        public SmokeSink(int value) => Value = value;
        public object Capture(string targetId) => Value;
        public void Apply(RuntimeCombatSpellMutation mutation)
        {
            Value = mutation.Disposition == RuntimeDamageDisposition.Damage ? Value - mutation.Magnitude : Value + mutation.Magnitude;
            if (FailNext) { FailNext = false; throw new InvalidOperationException("SMOKE_SINK_FAILURE"); }
        }
        public void Restore(string targetId, object snapshot) => Value = (int)snapshot;
    }
}
