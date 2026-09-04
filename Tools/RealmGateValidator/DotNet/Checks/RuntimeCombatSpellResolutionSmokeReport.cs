namespace RealmGate.Server;

public sealed record RuntimeCombatSpellResolutionSmokeReport(bool Passed, IReadOnlyList<string> Failures);

public static class RuntimeCombatSpellResolutionSmoke
{
    public static RuntimeCombatSpellResolutionSmokeReport Run()
    {
        var failures = new List<string>();
        var engine = new RuntimeCombatSpellResolutionEngine();
        var profile = new RuntimeResolutionProfile(
            "combat-spell-resolution-vector-0001", RuntimeResolutionConfidence.StronglyCorrelated,
            "physical", "physical_dr", "evasion", 4, 9, 2, 1, 80, 50, 0, 999,
            true, 95, 2, 1,
            new[] { "Librarium/Objects/formulas/catalog.json", "Librarium/Objects/parity/parity_records.json" },
            "Reject resolution when profile or evidence is unavailable.");
        var request = new RuntimeCombatSpellResolutionRequest(
            "vector-0001", "combat-0001", 17, "character:1", "npc:1", "weapon:1",
            RuntimeDamageDisposition.Damage, profile, 5, 2, 7, 123456UL);
        var first = engine.Resolve(request);
        var second = engine.Resolve(request);
        if (first != second) failures.Add("deterministic-replay-mismatch");
        if (first.FinalMagnitude < profile.MinimumFinalMagnitude || first.FinalMagnitude > profile.MaximumFinalMagnitude)
            failures.Add("final-magnitude-out-of-range");
        if (first.ResistanceReduction != 2) failures.Add("resistance-order-mismatch");
        if (first.EvidenceReferences.Count != 2) failures.Add("evidence-trace-missing");
        return new RuntimeCombatSpellResolutionSmokeReport(failures.Count == 0, failures);
    }
}
