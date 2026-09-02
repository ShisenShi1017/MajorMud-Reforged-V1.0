using System.Text.Json;
namespace Reforged.Server;

public sealed class RuntimeCombatPart2SmokeReport
{
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public bool Passed => Failures.Count == 0;
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeCombatPart2SmokeReport Run()
    {
        var report = new RuntimeCombatPart2SmokeReport();
        var sink = new Sink();
        var session = new RuntimeCombatSession("combat.advanced", "room.1", new Initiative(), new Actions(), sink, new Deaths());
        session.AddParticipant(new("character.1", RuntimeCombatParticipantKind.Character, "room.1", 20, 20));
        session.AddParticipant(new("npc.1", RuntimeCombatParticipantKind.Npc, "room.1", 6, 6));
        var coordinator = new RuntimeCombatCoordinator(session, new Threat(), new Ai(), new RoundEffects(), new Rewards(), sink);

        var roundEvents = coordinator.BeginRound(DateTimeOffset.UtcNow);
        Check(report, "round-effects", roundEvents.Count == 1 && sink.Events.Any(x => x.EventType == "round-effect"));
        var first = coordinator.Execute(new("player.attack", "character.1", RuntimeCombatActionKind.Attack, "npc.1"), DateTimeOffset.UtcNow);
        Check(report, "threat", first.Succeeded && coordinator.Capture().Threat.Any(x => x.SourceId == "npc.1" && x.TargetId == "character.1"));
        var npcResults = coordinator.ExecuteNpcTurns(DateTimeOffset.UtcNow);
        Check(report, "npc-ai", npcResults.Count == 1 && npcResults[0].Succeeded);
        var saved = coordinator.Capture();
        coordinator.Restore(saved);
        Check(report, "persistence", coordinator.Capture().Threat.Count == saved.Threat.Count);
        coordinator.Execute(new("player.kill", "character.1", RuntimeCombatActionKind.Attack, "npc.1"), DateTimeOffset.UtcNow);
        Check(report, "rewards", sink.Events.Any(x => x.EventType == "reward"));
        Check(report, "validator", coordinator.Validate().Count == 0);

        var failingSink = new Sink { FailEventType = "reward" };
        var rollbackSession = new RuntimeCombatSession("combat.reward.rollback", "room.1", new Initiative(), new Actions(), failingSink, new Deaths());
        rollbackSession.AddParticipant(new("character.1", RuntimeCombatParticipantKind.Character, "room.1", 20, 20));
        rollbackSession.AddParticipant(new("npc.1", RuntimeCombatParticipantKind.Npc, "room.1", 1, 1));
        var rollback = new RuntimeCombatCoordinator(rollbackSession, new Threat(), new Ai(), new RoundEffects(), new Rewards(), failingSink);
        rollback.BeginRound(DateTimeOffset.UtcNow);
        try { rollback.Execute(new("kill", "character.1", RuntimeCombatActionKind.Attack, "npc.1"), DateTimeOffset.UtcNow); } catch { }
        Check(report, "reward-rollback", rollback.Capture().Combat.Participants.Single(x => x.ParticipantId == "npc.1").IsAlive);
        return report;
    }

    private static void Check(RuntimeCombatPart2SmokeReport report, string name, bool passed) { report.Checks.Add(name); if (!passed) report.Failures.Add(name); }
    private sealed class Initiative : IRuntimeCombatInitiativePolicy { public IReadOnlyList<string> Order(IReadOnlyCollection<RuntimeCombatParticipant> p) => p.Select(x => x.ParticipantId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
    private sealed class Actions : IRuntimeCombatActionPolicy
    {
        public RuntimeCombatActionResult Resolve(RuntimeCombatSnapshot snapshot, RuntimeCombatAction action, DateTimeOffset nowUtc)
        {
            var magnitude = action.ActionId.Contains("kill", StringComparison.OrdinalIgnoreCase) ? 100 : 1;
            return new(true, null, null, new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "damage", action.ActorId, action.TargetId, magnitude, nowUtc) });
        }
    }
    private sealed class Deaths : IRuntimeCombatDeathHandler
    {
        public IReadOnlyList<RuntimeCombatEvent> HandleDeath(RuntimeCombatSnapshot snapshot, RuntimeCombatParticipant participant, DateTimeOffset nowUtc) =>
            new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "death", participant.ParticipantId, participant.ParticipantId, 0, nowUtc) };
    }
    private sealed class Threat : IRuntimeCombatThreatPolicy
    {
        public IReadOnlyList<RuntimeCombatThreatEntry> Project(RuntimeCombatSnapshot before, IReadOnlyList<RuntimeCombatEvent> events, IReadOnlyCollection<RuntimeCombatThreatEntry> current)
        {
            var map = current.ToDictionary(x => (x.SourceId, x.TargetId), x => x.Threat);
            foreach (var e in events.Where(x => x.EventType == "damage" && x.TargetId is not null))
            {
                var key = (SourceId: e.TargetId!, TargetId: e.ActorId);
                map[key] = map.GetValueOrDefault(key) + Math.Max(0, e.Magnitude);
            }
            return map.Select(x => new RuntimeCombatThreatEntry(x.Key.SourceId, x.Key.TargetId, x.Value)).ToArray();
        }
    }
    private sealed class Ai : IRuntimeCombatAiPolicy
    {
        public RuntimeCombatAction? SelectAction(RuntimeCombatSnapshot snapshot, RuntimeCombatParticipant participant, IReadOnlyCollection<RuntimeCombatThreatEntry> threat, DateTimeOffset nowUtc)
        {
            var target = threat.Where(x => x.SourceId == participant.ParticipantId).OrderByDescending(x => x.Threat).Select(x => x.TargetId).FirstOrDefault();
            return target is null ? null : new RuntimeCombatAction(Guid.NewGuid().ToString("N"), participant.ParticipantId, RuntimeCombatActionKind.Attack, target);
        }
    }
    private sealed class RoundEffects : IRuntimeCombatRoundEffectSource
    {
        public IReadOnlyList<RuntimeCombatEvent> ResolveRoundStart(RuntimeCombatSnapshot snapshot, DateTimeOffset nowUtc) =>
            new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "round-effect", "system", null, 0, nowUtc) };
    }
    private sealed class Rewards : IRuntimeCombatRewardHandler
    {
        public IReadOnlyList<RuntimeCombatEvent> ResolveRewards(RuntimeCombatSnapshot snapshot, DateTimeOffset nowUtc) =>
            new[] { new RuntimeCombatEvent(Guid.NewGuid().ToString("N"), snapshot.SessionId, snapshot.Round, "reward", "system", null, 0, nowUtc) };
    }
    private sealed class Sink : IRuntimeCombatEventSink
    {
        public string? FailEventType { get; init; }
        public List<RuntimeCombatEvent> Events { get; } = new();
        public void Apply(RuntimeCombatEvent combatEvent) { if (combatEvent.EventType == FailEventType) throw new InvalidOperationException("sink-failure"); Events.Add(combatEvent); }
        public void Revert(RuntimeCombatEvent combatEvent) => Events.Remove(combatEvent);
    }
}
