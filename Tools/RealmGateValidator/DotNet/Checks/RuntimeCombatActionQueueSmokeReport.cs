namespace RealmGate.Server;

public sealed record RuntimeCombatActionQueueSmokeReport(
    bool Passed,
    IReadOnlyList<string> Checks);

public static class RuntimeCombatActionQueueSmoke
{
    public static RuntimeCombatActionQueueSmokeReport Run()
    {
        var checks = new List<string>();
        var policies = new[] {
            new RuntimeCombatActionOrderingPolicy("policy.periodic", RuntimeQueuedCombatActionKind.PeriodicEffect, 10,
                new[] { "evidence.0162.same_tick_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production order"),
            new RuntimeCombatActionOrderingPolicy("policy.cast", RuntimeQueuedCombatActionKind.SpellCompletion, 20,
                new[] { "evidence.0162.same_tick_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production order"),
            new RuntimeCombatActionOrderingPolicy("policy.attack", RuntimeQueuedCombatActionKind.PhysicalAttack, 30,
                new[] { "evidence.0162.same_tick_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production order")
        };
        var queue = new RuntimeCombatActionQueue(policies, 100);
        queue.Enqueue(new RuntimeCombatActionRequest("action.3", "combat.1", "character.1", RuntimeQueuedCombatActionKind.PhysicalAttack, 105, "attack.3", 3));
        queue.Enqueue(new RuntimeCombatActionRequest("action.2", "combat.1", "character.1", RuntimeQueuedCombatActionKind.SpellCompletion, 105, "cast.2", 2));
        queue.Enqueue(new RuntimeCombatActionRequest("action.1", "combat.1", "world.1", RuntimeQueuedCombatActionKind.PeriodicEffect, 105, "effect.1", 1));
        var batch = queue.ClaimDue(105);
        if (batch.Actions.Select(x => x.ActionId).SequenceEqual(new[] { "action.1", "action.2", "action.3" })) checks.Add("same_tick_priority_order");
        queue.Complete("action.1", 105);
        queue.Fail("action.2", 105, "fixture_failure");
        queue.Cancel("action.3", 105, "fixture_cancel");
        if (queue.Actions.All(x => x.State is RuntimeCombatQueuedActionState.Completed or RuntimeCombatQueuedActionState.Failed or RuntimeCombatQueuedActionState.Cancelled)) checks.Add("single_terminal_transition");
        try {
            var unresolved = new RuntimeCombatActionQueue(new[] {
                new RuntimeCombatActionOrderingPolicy("policy.unresolved", RuntimeQueuedCombatActionKind.PhysicalAttack, null,
                    new[] { "native.pending" }, RuntimeResolutionConfidence.StronglyCorrelated, "reject")
            });
            unresolved.Enqueue(new RuntimeCombatActionRequest("action.9", "combat.1", "character.1", RuntimeQueuedCombatActionKind.PhysicalAttack, 0, "attack.9", 0));
        } catch (InvalidOperationException e) when (e.Message == "COMBAT_ACTION_QUEUE_ORDER_UNRESOLVED") { checks.Add("unresolved_order_fails_closed"); }
        return new RuntimeCombatActionQueueSmokeReport(checks.Count == 3, checks);
    }
}
