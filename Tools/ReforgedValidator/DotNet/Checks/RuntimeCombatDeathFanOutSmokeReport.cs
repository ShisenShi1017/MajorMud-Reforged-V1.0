namespace Reforged.Server;

public static class RuntimeCombatDeathFanOutSmokeReport
{
    private sealed class Sink : IRuntimeCombatDeathEventSink
    {
        public Sink(string id) => SinkId = id;
        public string SinkId { get; }
        public readonly List<string> Calls = new();
        public void Prepare(RuntimeCombatDeathEvent e) => Calls.Add($"prepare:{e.Sequence}:{e.Kind}");
        public void Commit(RuntimeCombatDeathEvent e) => Calls.Add($"commit:{e.Sequence}:{e.Kind}");
        public void Rollback(RuntimeCombatDeathEvent e) => Calls.Add($"rollback:{e.Sequence}:{e.Kind}");
    }

    public static IReadOnlyDictionary<string, bool> Run()
    {
        var order = new[] { RuntimeCombatDeathEventKind.CombatActionCancellation, RuntimeCombatDeathEventKind.StatusCleanup,
            RuntimeCombatDeathEventKind.ThreatCleanup, RuntimeCombatDeathEventKind.TargetCleanup,
            RuntimeCombatDeathEventKind.CombatParticipantDefeat, RuntimeCombatDeathEventKind.QuestDeathEvent,
            RuntimeCombatDeathEventKind.ExperienceRequest, RuntimeCombatDeathEventKind.LootRequest,
            RuntimeCombatDeathEventKind.GroundDropRequest, RuntimeCombatDeathEventKind.RespawnRequest,
            RuntimeCombatDeathEventKind.ResurrectionEligibilityRequest };
        var sinks = order.Select(x => (x, (IRuntimeCombatDeathEventSink)new Sink($"sink.{x}"))).ToArray();
        var policy = new RuntimeCombatDeathFanOutPolicy("death-policy.promoted.0175", order,
            new[] { "evidence.combat-death-order.0175" }, RuntimeResolutionConfidence.StronglyCorrelated, "rollback all committed sinks");
        var coordinator = new RuntimeCombatDeathFanOutCoordinator(policy, sinks);
        var result = coordinator.Execute(new RuntimeCombatDeathRequest("death.smoke.1", "combat.smoke.1", "npc.1", "character.1",
            "room.1", 80, 12, 0, "attack.smoke.1", new[] { "test-vector.death.0175" }));
        var duplicateRejected = false;
        try { coordinator.Execute(result.Request); } catch (InvalidOperationException) { duplicateRejected = true; }
        return new Dictionary<string, bool>
        {
            ["committed"] = result.Committed,
            ["ordered"] = result.Events.Select(x => x.Kind).SequenceEqual(order),
            ["canonical_event_ids"] = result.Events.All(x => x.EventId.StartsWith("death-event.", StringComparison.OrdinalIgnoreCase)),
            ["duplicate_rejected"] = duplicateRejected,
            ["lethal_hp_clamped"] = result.Request.HitPointsAfter == 0
        };
    }
}
