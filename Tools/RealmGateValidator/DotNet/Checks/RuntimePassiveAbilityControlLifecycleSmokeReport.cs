namespace RealmGate.Server;

public static class RuntimePassiveAbilityControlLifecycleSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var passive = new RuntimePassiveCombatAbilityPolicy("passiveabilitypolicy.1", "ability.1",
            RuntimePassiveModifierKind.Attack, 2, 1000, 1, "OneHanded", "item.1622",
            RuntimeResolutionConfidence.Proven, new[] { "evidence.passive.1" }, "reject");
        var control = new RuntimeControlLifecyclePolicy("controllifecyclepolicy.1", "spell.1", RuntimeControlEffectKind.Charm,
            true, true, true, true, true, true, true, true, true,
            RuntimeControlPersistenceDisposition.TemporaryOnly, RuntimeResolutionConfidence.StronglyCorrelated,
            new[] { "evidence.control.lifecycle.1" }, "reject");
        var coordinator = new RuntimePassiveAbilityControlLifecycleCoordinator(new[] { passive }, new[] { control });
        var passiveResult = coordinator.ResolvePassive(new RuntimePassiveCombatAbilityRequest("character.1", "OneHanded",
            new[] { "ability.1" }, new[] { "item.1622" }, 10), RuntimePassiveModifierKind.Attack);
        var sink = new SmokeSink();
        var controlResult = coordinator.ResolveControlLifecycle(new RuntimeControlLifecycleRequest("controllifecycle.1",
            "spell.1", "character.1", "npc.1", RuntimeControlBreakReason.RoomSeparation, false, true, true, false), new[] { sink });
        return new Dictionary<string, bool>
        {
            ["passive_applied"] = passiveResult.FinalValue == 13,
            ["passive_evidence"] = passiveResult.EvidenceReferences.Count == 1,
            ["control_broken"] = controlResult.BreakControl && controlResult.ClearControllerOwnership,
            ["control_committed"] = sink.Committed
        };
    }
    private sealed class SmokeSink : IRuntimeControlLifecycleSink
    {
        public bool Committed { get; private set; }
        public void Prepare(RuntimeControlLifecycleDecision decision) { }
        public void Commit(RuntimeControlLifecycleDecision decision) => Committed = true;
        public void Rollback(RuntimeControlLifecycleDecision decision) => Committed = false;
    }
}
