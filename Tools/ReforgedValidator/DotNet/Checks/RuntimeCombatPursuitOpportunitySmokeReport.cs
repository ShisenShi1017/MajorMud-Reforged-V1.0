namespace Reforged.Server;

public sealed record RuntimeCombatPursuitOpportunitySmokeReport(bool Passed, IReadOnlyList<string> Checks);

public static class RuntimeCombatPursuitOpportunitySmoke
{
    private sealed class AttackStub : IRuntimeCombatOpportunityAttackExecutor
    {
        public RuntimeCombatOpportunityAttackResult Execute(RuntimeCombatOpportunityAttackRequest request)
            => new(request.AttackId, true, 1, false);
    }
    private sealed class ResourceStub : IRuntimeCombatEscapeResourceSink
    {
        public int Value = 10;
        public object Capture(string actorId) => Value;
        public void Consume(string actorId, int amount, string resolutionId) => Value = checked(Value - amount);
        public void ConsumeRound(string actorId, string combatId, long worldTick, long sequence, string resolutionId) { }
        public void Restore(string actorId, object snapshot) => Value = (int)snapshot;
    }
    public static RuntimeCombatPursuitOpportunitySmokeReport Run()
    {
        var evidence = new[] { "evidence.0174.pursuit_fixture" };
        var scheduling = new[] {
            new RuntimeCombatSchedulingPolicy("schedule.character.fixture", "character", 10, 5, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved scheduling"),
            new RuntimeCombatSchedulingPolicy("schedule.npc.fixture", "npc", 8, 5, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved scheduling") };
        var combat = new RuntimeCombatInstanceLifecycle("combat.fixture", "room.1", 0, scheduling);
        combat.AddParticipant("character.1", "character", 0); combat.AddParticipant("npc.1", "npc", 0);
        var queue = new RuntimeCombatActionQueue(new[] { new RuntimeCombatActionOrderingPolicy("actionpolicy.attack.fixture", RuntimeQueuedCombatActionKind.PhysicalAttack, 0, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved order") }, 5);
        var invalidation = new RuntimeCombatActionInvalidationCoordinator(queue);
        var threat = new RuntimeCombatThreatLedger(new[] { new RuntimeCombatThreatPolicy("threatpolicy.damage.fixture", RuntimeCombatThreatEventKind.PhysicalDamage, 10000, 0, 1000, false, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved threat") });
        var movement = new RuntimeCombatMovementCoordinator(combat, queue, invalidation, threat, new[] { new RuntimeCombatMovementPolicy("movementpolicy.voluntary.fixture", RuntimeCombatMovementKind.VoluntaryFlee, true, true, true, true, new HashSet<RuntimeCombatMovementBlocker>(), evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved escape") });
        var resource = new ResourceStub();
        var runtime = new RuntimeCombatPursuitOpportunityCoordinator(combat, movement, new AttackStub(), resource);
        var policy = new RuntimeCombatPursuitPolicy("pursuitpolicy.fixture", RuntimeCombatMovementKind.VoluntaryFlee, true, 2, true, true, 1, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved pursuit");
        var req = new RuntimeCombatPursuitRequest("pursuit.1", new RuntimeCombatMovementRequest("movement.1", "combat.fixture", "character.1", "room.1", "room.2", "exit.1", RuntimeCombatMovementKind.VoluntaryFlee, new HashSet<RuntimeCombatMovementBlocker>(), true, 5), policy, RuntimeCombatEscapeDisposition.Escaped, new[] { "npc.1" }, new[] { "npc.1" }, 1);
        var result = runtime.Execute(req);
        var checks = new List<string>();
        if (result.Escaped && result.Handoff is not null) checks.Add("escape_handoff");
        if (result.OpportunityAttacks.Count == 1) checks.Add("opportunity_before_exit");
        if (result.Handoff?.PursuerIds.SequenceEqual(new[] { "npc.1" }) == true) checks.Add("bounded_pursuit");
        return new RuntimeCombatPursuitOpportunitySmokeReport(checks.Count == 3, checks);
    }
}
