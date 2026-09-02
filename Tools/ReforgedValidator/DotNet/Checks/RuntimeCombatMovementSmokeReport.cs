namespace Reforged.Server;

public sealed record RuntimeCombatMovementSmokeReport(bool Passed, IReadOnlyList<string> Checks);

public static class RuntimeCombatMovementSmoke
{
    public static RuntimeCombatMovementSmokeReport Run()
    {
        var evidence = new[] { "evidence.0165.movement_fixture" };
        var scheduling = new[] {
            new RuntimeCombatSchedulingPolicy("schedule.character.fixture", "character", 10, 5, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved scheduling"),
            new RuntimeCombatSchedulingPolicy("schedule.npc.fixture", "npc", 8, 5, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved scheduling") };
        var combat = new RuntimeCombatInstanceLifecycle("combat.fixture", "room.1", 0, scheduling);
        combat.AddParticipant("character.1", "character", 0);
        combat.AddParticipant("npc.1", "npc", 0);
        var queue = new RuntimeCombatActionQueue(new[] {
            new RuntimeCombatActionOrderingPolicy("actionpolicy.attack.fixture", RuntimeQueuedCombatActionKind.PhysicalAttack, 0, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved order") }, 5);
        queue.Enqueue(new RuntimeCombatActionRequest("action.1", "combat.fixture", "character.1", RuntimeQueuedCombatActionKind.PhysicalAttack, 5, "attack.1", 0));
        var invalidation = new RuntimeCombatActionInvalidationCoordinator(queue);
        invalidation.Register(new RuntimeCombatActionDependency("action.1", "character.1", new[] { "npc.1" }, true, true, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "cancel dependent action"));
        var threat = new RuntimeCombatThreatLedger(new[] {
            new RuntimeCombatThreatPolicy("threatpolicy.damage.fixture", RuntimeCombatThreatEventKind.PhysicalDamage, 10_000, 0, 1000, false, evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved threat") });
        threat.Apply(new RuntimeCombatThreatMutationRequest("threatmutation.1", "combat.fixture", "character.1", new[] { "npc.1" }, RuntimeCombatThreatEventKind.PhysicalDamage, 10, 5));
        var policies = new[] { new RuntimeCombatMovementPolicy("movementpolicy.voluntary.fixture", RuntimeCombatMovementKind.VoluntaryFlee, true, true, true, true,
            new HashSet<RuntimeCombatMovementBlocker> { RuntimeCombatMovementBlocker.Rooted, RuntimeCombatMovementBlocker.Stunned, RuntimeCombatMovementBlocker.Sleeping, RuntimeCombatMovementBlocker.Paralyzed, RuntimeCombatMovementBlocker.ExitLocked, RuntimeCombatMovementBlocker.ExitRestricted },
            evidence, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved escape outcome") };
        var runtime = new RuntimeCombatMovementCoordinator(combat, queue, invalidation, threat, policies);
        var result = runtime.Execute(new RuntimeCombatMovementRequest("movement.1", "combat.fixture", "character.1", "room.1", "room.2", "exit.1",
            RuntimeCombatMovementKind.VoluntaryFlee, new HashSet<RuntimeCombatMovementBlocker>(), true, 5));
        var checks = new List<string>();
        if (result.Moved && result.Handoff is not null) checks.Add("room_exit_handoff");
        if (queue.Get("action.1").State == RuntimeCombatQueuedActionState.Cancelled) checks.Add("pending_action_cancelled");
        if (threat.Entries.Count == 0) checks.Add("threat_cleaned");
        if (combat.Participants.Single(x => x.ActorId == "character.1").State == RuntimeCombatParticipantState.Fled) checks.Add("participant_fled");
        return new RuntimeCombatMovementSmokeReport(checks.Count == 4, checks);
    }
}
