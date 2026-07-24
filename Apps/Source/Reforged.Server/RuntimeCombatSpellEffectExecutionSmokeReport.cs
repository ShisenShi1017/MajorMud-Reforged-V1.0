namespace Reforged.Server;

public static class RuntimeCombatSpellEffectExecutionSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var lifecyclePolicies = new[] {
            new RuntimeSpellEffectPolicy("life.dot", "spell.9010", "damage_over_time", 8, 2,
                RuntimeSpellEffectStackingMode.Refresh, 1, true, false, new[]{"fixture"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeSpellEffectPolicy("life.buff", "spell.9011", "armor_modifier", 8, 0,
                RuntimeSpellEffectStackingMode.Independent, 2, false, false, new[]{"fixture"}, RuntimeResolutionConfidence.Proven, "reject")
        };
        var executionPolicies = new[] {
            new RuntimeSpellEffectExecutionPolicy("exec.dot", "spell.9010", "damage_over_time", RuntimeDamageDisposition.Damage,
                RuntimeSpellEffectPolarity.Harmful, "magic", 20, new[]{"fixture"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeSpellEffectExecutionPolicy("exec.buff", "spell.9011", "armor_modifier", RuntimeDamageDisposition.Healing,
                RuntimeSpellEffectPolarity.Beneficial, "magic", 10, new[]{"fixture"}, RuntimeResolutionConfidence.StronglyCorrelated, "reject")
        };
        var lifecycle = new RuntimeSpellEffectLifecycle(lifecyclePolicies, 10);
        lifecycle.Apply(new RuntimeSpellEffectApplication("effect.10", "cast.dot", "character.1", "npc.2", "spell.9010", 3, 10));
        lifecycle.Apply(new RuntimeSpellEffectApplication("effect.11", "cast.buff", "character.1", "npc.2", "spell.9011", 1, 10));
        var sink = new MemorySink();
        var execution = new RuntimeCombatSpellEffectExecution(lifecycle, executionPolicies, sink);
        var advance = execution.AdvanceAndCommit(14);
        var dispel = execution.Dispel(new RuntimeSpellDispelRequest("dispel.one", "character.1", "npc.2", 14, "magic", null, 1));

        var rollbackLifecycle = new RuntimeSpellEffectLifecycle(lifecyclePolicies, 10);
        rollbackLifecycle.Apply(new RuntimeSpellEffectApplication("effect.20", "cast.fail", "character.1", "npc.3", "spell.9010", 2, 10));
        var failing = new RuntimeCombatSpellEffectExecution(rollbackLifecycle, executionPolicies, new MemorySink(true));
        var rolledBack = Throws(() => failing.AdvanceAndCommit(12)) && rollbackLifecycle.WorldTick == 10;
        return new Dictionary<string, bool> {
            ["periodic_ticks_commit_in_stable_order"] = advance.Commits.Select(x => x.Tick.WorldTick).SequenceEqual(new long[]{12,14}),
            ["periodic_magnitude_includes_stack_count"] = advance.Commits.All(x => x.Mutation.Magnitude == 3),
            ["mutation_failure_restores_effect_clock"] = rolledBack,
            ["dispel_uses_priority_then_age_then_effect_id"] = dispel.Removed.Single().EffectId == "effect.10",
            ["dispel_respects_maximum_effects"] = dispel.Removed.Count == 1,
            ["unresolved_execution_policy_rejects"] = Throws(() => new RuntimeCombatSpellEffectExecution(lifecycle, Array.Empty<RuntimeSpellEffectExecutionPolicy>(), sink).AdvanceAndCommit(16))
        };
    }

    private sealed class MemorySink : IRuntimeCombatSpellMutationSink
    {
        private readonly bool _fail;
        private readonly Dictionary<string,int> _values = new(StringComparer.OrdinalIgnoreCase);
        public MemorySink(bool fail=false) => _fail=fail;
        public object Capture(string targetId) => _values.TryGetValue(targetId,out var v)?v:0;
        public void Apply(RuntimeCombatSpellMutation mutation) { if(_fail) throw new InvalidOperationException("fixture"); _values[mutation.TargetId]=(int)Capture(mutation.TargetId)+mutation.Magnitude; }
        public void Restore(string targetId, object snapshot) => _values[targetId]=(int)snapshot;
    }
    private static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
}
