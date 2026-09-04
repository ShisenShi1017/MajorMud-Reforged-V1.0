namespace RealmGate.Server;
public static class RuntimeCombatSpellMovementSmokeReport
{
    private sealed class Sink : IRuntimeSpellMovementEventSink
    {
        public Sink(string id) => SinkId = id;
        public string SinkId { get; }
        public readonly List<string> Calls = new();
        public void Prepare(RuntimeSpellMovementEvent e) => Calls.Add($"prepare:{e.Sequence}:{e.Kind}");
        public void Commit(RuntimeSpellMovementEvent e) => Calls.Add($"commit:{e.Sequence}:{e.Kind}");
        public void Rollback(RuntimeSpellMovementEvent e) => Calls.Add($"rollback:{e.Sequence}:{e.Kind}");
    }
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var order = new[] { RuntimeSpellMovementEventKind.ValidateSource, RuntimeSpellMovementEventKind.ValidateTargets,
            RuntimeSpellMovementEventKind.ValidateDestination, RuntimeSpellMovementEventKind.ValidateRoomRestrictions,
            RuntimeSpellMovementEventKind.ValidateCombatRestrictions, RuntimeSpellMovementEventKind.ValidateConsent,
            RuntimeSpellMovementEventKind.InterruptActions, RuntimeSpellMovementEventKind.CleanupThreat,
            RuntimeSpellMovementEventKind.CleanupCombat, RuntimeSpellMovementEventKind.RemoveMovementStatuses,
            RuntimeSpellMovementEventKind.MovePrimary, RuntimeSpellMovementEventKind.EmitMovementEvent,
            RuntimeSpellMovementEventKind.PersistMovementState };
        var policy = new RuntimeSpellMovementPolicy("movement-policy.promoted.0178", RuntimeSpellMovementKind.TargetTeleport,
            true, true, true, false, true, true, true, true, true, false, order,
            new[] { "evidence.spell-movement-order.0178" }, RuntimeResolutionConfidence.StronglyCorrelated,
            "reject unresolved destination, consent, combat cleanup, or movement ownership");
        var sinks = order.Select(x => (x, (IRuntimeSpellMovementEventSink)new Sink($"sink.{x}"))).ToArray();
        var coordinator = new RuntimeCombatSpellMovementCoordinator(policy, sinks);
        var request = new RuntimeSpellMovementRequest("movement.smoke.1", "spell.1", "character.1", "character.2", null,
            "room.1", "room.2", "exit.1", "combat.1", 100, true, true, true, false, false, true, true, true, true, true, true,
            new[] { "test-vector.movement.0178" });
        var result = coordinator.Execute(request);
        var duplicateRejected = false; try { coordinator.Execute(request); } catch (InvalidOperationException) { duplicateRejected = true; }
        var consentRejected = false; try { new RuntimeCombatSpellMovementCoordinator(policy, sinks).Execute(request with { MovementId = "movement.smoke.2", ConsentGranted = false }); } catch (InvalidOperationException) { consentRejected = true; }
        return new Dictionary<string, bool> { ["committed"] = result.Committed, ["ordered"] = result.Events.Select(x => x.Kind).SequenceEqual(order),
            ["interrupted"] = result.ActionsInterrupted, ["threat_cleaned"] = result.ThreatCleaned, ["combat_cleaned"] = result.CombatCleaned,
            ["root_removed"] = result.RootRemoved, ["hidden_removed"] = result.HiddenRemoved, ["duplicate_rejected"] = duplicateRejected,
            ["consent_rejected"] = consentRejected };
    }
}
