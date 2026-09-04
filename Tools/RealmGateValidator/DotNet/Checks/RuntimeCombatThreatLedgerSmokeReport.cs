namespace RealmGate.Server;

public sealed record RuntimeCombatThreatLedgerSmokeReport(bool Passed, IReadOnlyList<string> Checks);

public static class RuntimeCombatThreatLedgerSmoke
{
    public static RuntimeCombatThreatLedgerSmokeReport Run()
    {
        var checks = new List<string>();
        var policies = new[]
        {
            new RuntimeCombatThreatPolicy("threatpolicy.physical", RuntimeCombatThreatEventKind.PhysicalDamage,
                10000, 0, 1000, false, new[] { "evidence.0164.threat_fixture" },
                RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production multiplier"),
            new RuntimeCombatThreatPolicy("threatpolicy.healing", RuntimeCombatThreatEventKind.Healing,
                5000, 0, 1000, false, new[] { "evidence.0164.threat_fixture" },
                RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production multiplier"),
            new RuntimeCombatThreatPolicy("threatpolicy.taunt", RuntimeCombatThreatEventKind.Taunt,
                0, 75, 75, true, new[] { "evidence.0164.threat_fixture" },
                RuntimeResolutionConfidence.StronglyCorrelated, "reject unresolved production taunt")
        };
        var ledger = new RuntimeCombatThreatLedger(policies);
        var damage = ledger.Apply(new RuntimeCombatThreatMutationRequest("threatmutation.1", "combat.1", "character.1",
            new[] { "npc.2", "npc.1" }, RuntimeCombatThreatEventKind.PhysicalDamage, 40, 10));
        if (damage.Select(x => x.ObserverNpcId).SequenceEqual(new[] { "npc.1", "npc.2" })) checks.Add("canonical_observer_order");
        ledger.Apply(new RuntimeCombatThreatMutationRequest("threatmutation.2", "combat.1", "character.1",
            new[] { "npc.1" }, RuntimeCombatThreatEventKind.Healing, 20, 11));
        var inspect = ledger.Inspect("combat.1", "npc.1");
        if (inspect.Count == 1 && inspect[0].Threat == 50) checks.Add("damage_and_healing_accumulate");
        ledger.Apply(new RuntimeCombatThreatMutationRequest("threatmutation.3", "combat.1", "character.2",
            new[] { "npc.1" }, RuntimeCombatThreatEventKind.Taunt, 0, 12));
        if (ledger.Inspect("combat.1", "npc.1").Select(x => x.SourceActorId).SequenceEqual(new[] { "character.2", "character.1" }))
            checks.Add("deterministic_inspection_order");
        var cleanup = ledger.Cleanup("threatcleanup.1", "combat.1", "character.1", RuntimeCombatThreatCleanupReason.Fled);
        if (cleanup.RemovedEntries.Count == 2 && ledger.Entries.All(x => x.SourceActorId != "character.1")) checks.Add("flee_cleanup");
        try { ledger.Apply(new RuntimeCombatThreatMutationRequest("threatmutation.4", "combat.1", "character.1", new[] { "npc.1" }, RuntimeCombatThreatEventKind.SpellDamage, 10, 13)); }
        catch (InvalidOperationException e) when (e.Message == "COMBAT_THREAT_POLICY_UNRESOLVED") { checks.Add("unresolved_policy_fails_closed"); }
        try { ledger.Cleanup("threatcleanup.1", "combat.1", "character.1", RuntimeCombatThreatCleanupReason.Fled); }
        catch (InvalidOperationException e) when (e.Message == "COMBAT_THREAT_CLEANUP_ALREADY_APPLIED") { checks.Add("duplicate_cleanup_rejected"); }
        return new RuntimeCombatThreatLedgerSmokeReport(checks.Count == 6, checks);
    }
}
