namespace RealmGate.Server;
public static class RuntimeCombatSpellSummoningSmokeReport
{
    private sealed class Sink : IRuntimeCombatSummonEventSink
    {
        public Sink(string id) => SinkId = id;
        public string SinkId { get; }
        public readonly List<string> Calls = new();
        public void Prepare(RuntimeCombatSummonEvent e) => Calls.Add($"prepare:{e.Sequence}:{e.Kind}");
        public void Commit(RuntimeCombatSummonEvent e) => Calls.Add($"commit:{e.Sequence}:{e.Kind}");
        public void Rollback(RuntimeCombatSummonEvent e) => Calls.Add($"rollback:{e.Sequence}:{e.Kind}");
    }
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var order = Enum.GetValues<RuntimeCombatSummonEventKind>();
        var policy = new RuntimeCombatSummonPolicy("summon-policy.promoted.0177", RuntimeCombatSummonOwnerKind.Character,
            true, true, false, false, 2, 4, 20, 1, 30, order,
            Enum.GetValues<RuntimeCombatSummonCleanupReason>(), new[] { "evidence.combat-summon-order.0177" },
            RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved summon ownership or limits");
        var sinks = order.Select(x => (x, (IRuntimeCombatSummonEventSink)new Sink($"sink.{x}"))).ToArray();
        var coordinator = new RuntimeCombatSpellSummoningCoordinator(policy, sinks);
        var request = new RuntimeCombatSummonRequest("summon.smoke.1", "spell.1", "npc.1", "character.1", "room.1", "combat.1", 100, 0, 0, 0, 0, true, true, new[] { "test-vector.summon.0177" });
        var result = coordinator.Execute(request);
        var duplicateRejected = false; try { coordinator.Execute(request); } catch (InvalidOperationException) { duplicateRejected = true; }
        var limitRejected = false; try { new RuntimeCombatSpellSummoningCoordinator(policy, sinks).Execute(request with { SummonId = "summon.smoke.2", ExistingOwned = 2 }); } catch (InvalidOperationException) { limitRejected = true; }
        return new Dictionary<string, bool> { ["committed"] = result.Committed, ["ordered"] = result.Events.Select(x => x.Kind).SequenceEqual(order), ["expires"] = result.ExpiresAtTick == 130, ["combat_inserted"] = result.InsertedIntoCombat, ["threat_inherited"] = result.ThreatInherited, ["duplicate_rejected"] = duplicateRejected, ["limit_rejected"] = limitRejected };
    }
}
