namespace RealmGate.Server;

public static class RuntimeCombatSpellEffectLifecycleSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var policies = new[]
        {
            new RuntimeSpellEffectPolicy("effect.policy.dot", "spell.9004", "damage_over_time", 6, 2,
                RuntimeSpellEffectStackingMode.Refresh, 1, true, false,
                new[] {"fixture"}, RuntimeResolutionConfidence.Proven, "reject"),
            new RuntimeSpellEffectPolicy("effect.policy.independent", "spell.9005", "attribute_modifier", 4, 0,
                RuntimeSpellEffectStackingMode.Independent, 2, false, false,
                new[] {"fixture"}, RuntimeResolutionConfidence.StronglyCorrelated, "reject")
        };
        var lifecycle = new RuntimeSpellEffectLifecycle(policies, 10);
        var first = lifecycle.Apply(new RuntimeSpellEffectApplication("effect.1", "cast.one",
            "character.1", "npc.2", "spell.9004", 3, 10));
        var advance = lifecycle.Advance(14);
        var refreshed = lifecycle.Apply(new RuntimeSpellEffectApplication("effect.2", "cast.two",
            "character.1", "npc.2", "spell.9004", 5, 14));
        lifecycle.Apply(new RuntimeSpellEffectApplication("effect.3", "cast.three",
            "character.1", "character.2", "spell.9005", 1, 14));
        lifecycle.Apply(new RuntimeSpellEffectApplication("effect.4", "cast.four",
            "character.1", "character.2", "spell.9005", 1, 14));
        var stackRejected = Throws(() => lifecycle.Apply(new RuntimeSpellEffectApplication("effect.5", "cast.five",
            "character.1", "character.2", "spell.9005", 1, 14)));
        var expired = lifecycle.Advance(20);
        return new Dictionary<string, bool>
        {
            ["deterministic_tick_schedule"] = advance.Ticks.Select(x => x.WorldTick).SequenceEqual(new long[] {12,14}),
            ["refresh_reuses_existing_effect_identity"] = refreshed.EffectId == first.EffectId && refreshed.Magnitude == 5,
            ["maximum_independent_stacks_rejected"] = stackRejected,
            ["expiration_is_terminal"] = expired.Expired.Any(x => x.EffectId == "effect.1" && x.State == RuntimeSpellEffectState.Expired),
            ["tick_order_is_stable"] = advance.Ticks.Select(x => x.EffectId).SequenceEqual(new[] {"effect.1","effect.1"}),
            ["terminal_effect_cannot_be_removed_twice"] = Throws(() => lifecycle.Remove("effect.1", 20, "duplicate"))
        };
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch { return true; }
    }
}
