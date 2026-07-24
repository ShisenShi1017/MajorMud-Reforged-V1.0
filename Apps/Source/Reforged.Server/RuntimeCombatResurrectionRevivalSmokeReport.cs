namespace Reforged.Server;

public static class RuntimeCombatResurrectionRevivalSmokeReport
{
    private sealed class Sink : IRuntimeCombatRevivalEventSink
    {
        public Sink(string id) => SinkId = id;
        public string SinkId { get; }
        public readonly List<string> Calls = new();
        public void Prepare(RuntimeCombatRevivalEvent e) => Calls.Add($"prepare:{e.Sequence}:{e.Kind}");
        public void Commit(RuntimeCombatRevivalEvent e) => Calls.Add($"commit:{e.Sequence}:{e.Kind}");
        public void Rollback(RuntimeCombatRevivalEvent e) => Calls.Add($"rollback:{e.Sequence}:{e.Kind}");
    }

    public static IReadOnlyDictionary<string, bool> Run()
    {
        var order = new[] { RuntimeCombatRevivalEventKind.ValidateEligibility, RuntimeCombatRevivalEventKind.ConsumeResource,
            RuntimeCombatRevivalEventKind.ClearDeathState, RuntimeCombatRevivalEventKind.RestoreHitPoints,
            RuntimeCombatRevivalEventKind.RestoreMana, RuntimeCombatRevivalEventKind.RestoreEnergy,
            RuntimeCombatRevivalEventKind.ApplyEffectDisposition, RuntimeCombatRevivalEventKind.MoveToDestination,
            RuntimeCombatRevivalEventKind.ApplyResurrectionSickness, RuntimeCombatRevivalEventKind.PersistCharacterState,
            RuntimeCombatRevivalEventKind.EmitRevivalEvent };
        var sinks = order.Select(x => (x, (IRuntimeCombatRevivalEventSink)new Sink($"sink.{x}"))).ToArray();
        var policy = new RuntimeCombatRevivalPolicy("revival-policy.promoted.0176", RuntimeCombatRevivalMode.Resurrection,
            true, true, false, 2500, 1000, 1000, RuntimeCombatRevivalEffectDisposition.RemoveHarmful,
            order, new[] { "evidence.combat-revival-order.0176" }, RuntimeResolutionConfidence.StronglyCorrelated,
            "reject unresolved eligibility or restoration policy");
        var coordinator = new RuntimeCombatResurrectionRevivalCoordinator(policy, sinks);
        var request = new RuntimeCombatRevivalRequest("revival.smoke.1", "character.1", "character.2", "spell.1",
            "room.1", "room.1", "corpse.1", 100, true, true, 120, 0, 100, 0, 80, 0, 60,
            new[] { "test-vector.revival.0176" });
        var result = coordinator.Execute(request);
        var duplicateRejected = false;
        try { coordinator.Execute(request); } catch (InvalidOperationException) { duplicateRejected = true; }
        var expiredRejected = false;
        try
        {
            var expired = request with { RevivalId = "revival.smoke.2", WorldTick = 121 };
            new RuntimeCombatResurrectionRevivalCoordinator(policy, sinks).Execute(expired);
        }
        catch (InvalidOperationException) { expiredRejected = true; }
        return new Dictionary<string, bool>
        {
            ["committed"] = result.Committed,
            ["ordered"] = result.Events.Select(x => x.Kind).SequenceEqual(order),
            ["hp_restored"] = result.FinalHitPoints == 25,
            ["mana_restored"] = result.FinalMana == 8,
            ["energy_restored"] = result.FinalEnergy == 6,
            ["combat_not_restored"] = !result.CombatStateRestored && result.Events.All(x => !x.RestoreCombatState),
            ["duplicate_rejected"] = duplicateRejected,
            ["expired_rejected"] = expiredRejected
        };
    }
}
