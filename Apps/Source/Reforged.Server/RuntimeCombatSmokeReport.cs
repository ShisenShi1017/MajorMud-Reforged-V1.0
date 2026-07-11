using System.Text.Json;
namespace Reforged.Server;

public sealed class RuntimeCombatSmokeReport
{
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public bool Passed => Failures.Count == 0;
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeCombatSmokeReport Run()
    {
        var report = new RuntimeCombatSmokeReport();
        var sink = new MemorySink();
        var session = new RuntimeCombatSession("combat.1", "room.1", new StableInitiative(), new TestActions(), sink, new TestDeaths());
        session.AddParticipant(new("character.1", RuntimeCombatParticipantKind.Character, "room.1", 20, 20));
        session.AddParticipant(new("npc.1", RuntimeCombatParticipantKind.Npc, "room.1", 5, 5));
        Check(report, "participants", session.Capture().Participants.Count == 2);
        session.BeginRound();
        Check(report, "round", session.Capture().Round == 1);
        Check(report, "initiative", session.Capture().InitiativeOrder.SequenceEqual(new[] { "character.1", "npc.1" }));
        var attack = session.Execute(new("action.1", "character.1", RuntimeCombatActionKind.Attack, "npc.1", "generated.attack"), DateTimeOffset.UtcNow);
        Check(report, "attack", attack.Succeeded && attack.Events.Any(x => x.EventType == "damage"));
        Check(report, "death", session.Capture().Participants.Single(x => x.ParticipantId == "npc.1").IsAlive == false);
        Check(report, "corpse-hook", sink.Events.Any(x => x.EventType == "death"));
        Check(report, "complete", session.Capture().State == RuntimeCombatState.Completed);
        Check(report, "validator", session.Validate().Count == 0);

        var rollbackSink = new MemorySink { ThrowOnApply = true };
        var rollback = new RuntimeCombatSession("combat.2", "room.1", new StableInitiative(), new TestActions(), rollbackSink, new TestDeaths());
        rollback.AddParticipant(new("character.1", RuntimeCombatParticipantKind.Character, "room.1", 20, 20));
        rollback.AddParticipant(new("npc.1", RuntimeCombatParticipantKind.Npc, "room.1", 5, 5));
        rollback.BeginRound();
        try { rollback.Execute(new("action.2", "character.1", RuntimeCombatActionKind.Attack, "npc.1"), DateTimeOffset.UtcNow); }
        catch { }
        Check(report, "rollback", rollback.Capture().Participants.Single(x => x.ParticipantId == "npc.1").CurrentHitPoints == 5);
        return report;
    }

    private static void Check(RuntimeCombatSmokeReport report, string name, bool passed) { report.Checks.Add(name); if (!passed) report.Failures.Add(name); }
    private sealed class StableInitiative : IRuntimeCombatInitiativePolicy { public IReadOnlyList<string> Order(IReadOnlyCollection<RuntimeCombatParticipant> p) => p.Select(x => x.ParticipantId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
    private sealed class TestActions : IRuntimeCombatActionPolicy
    {
        public RuntimeCombatActionResult Resolve(RuntimeCombatSnapshot snapshot, RuntimeCombatAction action, DateTimeOffset nowUtc) =>
            new(true, null, null, new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "damage", action.ActorId, action.TargetId, 5, nowUtc, new Dictionary<string,string>{{"definition_id", action.DefinitionId ?? string.Empty}}) });
    }
    private sealed class TestDeaths : IRuntimeCombatDeathHandler
    {
        public IReadOnlyList<RuntimeCombatEvent> HandleDeath(RuntimeCombatSnapshot snapshot, RuntimeCombatParticipant participant, DateTimeOffset nowUtc) =>
            new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "death", participant.ParticipantId, participant.ParticipantId, 0, nowUtc) };
    }
    private sealed class MemorySink : IRuntimeCombatEventSink
    {
        public bool ThrowOnApply { get; init; }
        public List<RuntimeCombatEvent> Events { get; } = new();
        public void Apply(RuntimeCombatEvent combatEvent) { if (ThrowOnApply) throw new InvalidOperationException("sink-failure"); Events.Add(combatEvent); }
        public void Revert(RuntimeCombatEvent combatEvent) => Events.Remove(combatEvent);
    }
}
