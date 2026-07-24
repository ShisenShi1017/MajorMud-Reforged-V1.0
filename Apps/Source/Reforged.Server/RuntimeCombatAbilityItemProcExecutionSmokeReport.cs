namespace Reforged.Server;

public static class RuntimeCombatAbilityItemProcExecutionSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var ability = new RuntimeCombatAbilityPolicy("abilitypolicy.1", "ability.1", RuntimeCombatAbilityKind.ActivatedAttack,
            "OneHanded", "item.1622", 5, 3, 10, RuntimeResolutionConfidence.Proven,
            new[] { "evidence.ability.1" }, "reject");
        var proc = new RuntimeItemProcPolicy("procpolicy.1", "item.1622", RuntimeItemProcTrigger.OnHit,
            RuntimeProcDisposition.Damage, 10000, 20, 4, null, null,
            RuntimeResolutionConfidence.StronglyCorrelated, new[] { "evidence.proc.1" }, "reject");
        var coordinator = new RuntimeCombatAbilityItemProcCoordinator(new[] { ability });
        var sink = new SmokeSink();
        var result = coordinator.Execute(new RuntimeCombatAbilityRequest("abilityexecution.1", "combat.1", "ability.1",
            "character.1", "npc.1", "OneHanded", "item.1622", 10, 5, 5, new[] { proc }, 7), new[] { sink });
        return new Dictionary<string, bool>
        {
            ["accepted"] = result.Accepted,
            ["resource_consumed"] = result.ResourceConsumed == 5,
            ["cooldown"] = result.NextCooldownTick == 8,
            ["proc_triggered"] = result.ProcDecisions.Count == 1 && result.ProcDecisions[0].Triggered,
            ["committed"] = sink.Committed
        };
    }
    private sealed class SmokeSink : IRuntimeCombatAbilitySink
    {
        public bool Committed { get; private set; }
        public void Prepare(RuntimeCombatAbilityDecision decision) { }
        public void Commit(RuntimeCombatAbilityDecision decision) => Committed = true;
        public void Rollback(RuntimeCombatAbilityDecision decision) => Committed = false;
    }
}
