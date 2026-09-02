namespace Reforged.Server;

public static class RuntimeCombatSpellCastLifecycleSmoke
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var synthetic = new RuntimeSpellCastTimingPolicy(
            "policy.smoke.spell.999999", "spell.999999", 3,
            new HashSet<RuntimeSpellInterruptionReason> { RuntimeSpellInterruptionReason.ExplicitCancel, RuntimeSpellInterruptionReason.ActorMoved },
            new[] { "Evidence/CombatSpellReconstruction/combat_spell_cast_lifecycle_evidence_0153.json#smoke-policy" },
            RuntimeResolutionConfidence.StronglyCorrelated,
            "Reject unpromoted duration and interruption branches.");
        var lifecycle = new RuntimeSpellCastLifecycle(new[] { synthetic });
        var started = lifecycle.Start("cast-1", "character:1", "spell.999999", 100);
        var early = lifecycle.Advance(102).Count == 0;
        var ready = lifecycle.Advance(103).Single();
        var completed = lifecycle.Complete("cast-1", 103);

        var second = lifecycle.Start("cast-2", "character:1", "spell.999999", 104);
        var interrupted = lifecycle.Interrupt(second.CastId, RuntimeSpellInterruptionReason.ActorMoved, 105);

        var unresolvedDurationRejected = false;
        try
        {
            var unresolved = synthetic with { PolicyId = "policy.spell.1304.unresolved", SpellId = "spell.1304", CastDurationTicks = null };
            new RuntimeSpellCastLifecycle(new[] { unresolved }).Start("cast-3", "character:2", "spell.1304", 200);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SPELL_CAST_DURATION_UNRESOLVED") { unresolvedDurationRejected = true; }

        var unresolvedInterruptRejected = false;
        try
        {
            var third = lifecycle.Start("cast-4", "character:2", "spell.999999", 300);
            lifecycle.Interrupt(third.CastId, RuntimeSpellInterruptionReason.ActorDamaged, 301);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SPELL_CAST_INTERRUPTION_UNRESOLVED") { unresolvedInterruptRejected = true; }

        var tickRegressionRejected = false;
        try { lifecycle.Advance(99); }
        catch (InvalidOperationException ex) when (ex.Message == "SPELL_CAST_TICK_REGRESSION") { tickRegressionRejected = true; }

        return new Dictionary<string, bool>
        {
            ["duration_tick_boundary"] = started.CompletionTick == 103 && early && ready.State == RuntimeSpellCastState.ReadyToComplete,
            ["explicit_completion_gate"] = completed.State == RuntimeSpellCastState.Completed,
            ["promoted_interruption"] = interrupted.State == RuntimeSpellCastState.Interrupted,
            ["unresolved_duration_fails_closed"] = unresolvedDurationRejected,
            ["unresolved_interruption_fails_closed"] = unresolvedInterruptRejected,
            ["monotonic_tick_ownership"] = tickRegressionRejected,
            ["deterministic_transition_trace"] = lifecycle.Transitions.Count >= 6
        };
    }
}
