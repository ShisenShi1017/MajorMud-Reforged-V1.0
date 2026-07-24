namespace Reforged.Server;

public sealed record RuntimeCombatActionInvalidationSmokeReport(bool Passed, IReadOnlyList<string> Checks);

public static class RuntimeCombatActionInvalidationSmoke
{
    public static RuntimeCombatActionInvalidationSmokeReport Run()
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
        var queue = new RuntimeCombatActionQueue(policies, 200);
        queue.Enqueue(new RuntimeCombatActionRequest("action.attack.1", "combat.1", "character.1", RuntimeQueuedCombatActionKind.PhysicalAttack, 205, "attack.1", 3));
        queue.Enqueue(new RuntimeCombatActionRequest("action.cast.1", "combat.1", "character.1", RuntimeQueuedCombatActionKind.SpellCompletion, 205, "cast.1", 2));
        queue.Enqueue(new RuntimeCombatActionRequest("action.effect.1", "combat.1", "world.1", RuntimeQueuedCombatActionKind.PeriodicEffect, 205, "effect.1", 1));
        var coordinator = new RuntimeCombatActionInvalidationCoordinator(queue);
        coordinator.Register(new RuntimeCombatActionDependency("action.attack.1", "character.1", new[] { "npc.1" }, true, true,
            new[] { "evidence.0163.death_cancellation_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "cancel dependent action"));
        coordinator.Register(new RuntimeCombatActionDependency("action.cast.1", "character.1", new[] { "npc.1" }, true, true,
            new[] { "evidence.0163.death_cancellation_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "cancel dependent action"));
        coordinator.Register(new RuntimeCombatActionDependency("action.effect.1", "world.1", new[] { "npc.1" }, false, true,
            new[] { "evidence.0163.death_cancellation_fixture" }, RuntimeResolutionConfidence.StronglyCorrelated, "cancel dependent action"));
        queue.ClaimDue(205);
        var result = coordinator.Invalidate(new RuntimeCombatEntityInvalidation("combat.1", "npc.1", RuntimeCombatEntityInvalidationReason.Death, 205));
        if (result.Cancellations.Select(x => x.ActionId).SequenceEqual(new[] { "action.effect.1", "action.cast.1", "action.attack.1" })) checks.Add("deterministic_dependency_cancellation");
        if (queue.Actions.All(x => x.State == RuntimeCombatQueuedActionState.Cancelled)) checks.Add("claimed_actions_cancelled");
        if (result.Cancellations.All(x => x.CancellationReason == "target_death")) checks.Add("target_death_reason_trace");
        try { coordinator.Invalidate(new RuntimeCombatEntityInvalidation("combat.1", "npc.1", RuntimeCombatEntityInvalidationReason.Death, 205)); }
        catch (InvalidOperationException e) when (e.Message == "COMBAT_ACTION_INVALIDATION_ALREADY_APPLIED") { checks.Add("duplicate_invalidation_rejected"); }
        return new RuntimeCombatActionInvalidationSmokeReport(checks.Count == 4, checks);
    }
}
