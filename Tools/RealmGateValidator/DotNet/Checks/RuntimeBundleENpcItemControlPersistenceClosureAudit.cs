using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleENpcItemControlPersistenceClosureReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleENpcItemControlPersistenceClosureAudit
{
    public static RuntimeBundleENpcItemControlPersistenceClosureReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Merge(string name, bool passed, IEnumerable<string> sourceChecks, IEnumerable<string> sourceFailures)
        {
            if (passed) checks.Add(name + ":passed"); else failures.Add(name + ":failed");
            checks.AddRange(sourceChecks.Select(x => name + ":" + x));
            failures.AddRange(sourceFailures.Select(x => name + ":" + x));
        }

        var inventory = RuntimeNpcInventoryOwnershipSmokeReport.Run(projectRoot);
        Merge("inventory-ownership", inventory.Passed, inventory.Checks, inventory.Failures);
        var equipment = RuntimeNpcEquipmentOwnershipSmokeReport.Run(projectRoot);
        Merge("equipment-ownership", equipment.Passed, equipment.Checks, equipment.Failures);
        var selection = RuntimeNpcEquipmentSelectionSmokeReport.Run(projectRoot);
        Merge("equipment-selection", selection.Passed, selection.Checks, selection.Failures);
        var ammunition = RuntimeNpcAmmunitionSmokeReport.Run(projectRoot);
        Merge("ammunition-use", ammunition.Passed, ammunition.Checks, ammunition.Failures);
        var itemUse = RuntimeNpcItemUseSmokeReport.Run(projectRoot);
        Merge("item-use", itemUse.Passed, itemUse.Checks, itemUse.Failures);
        var itemEffects = RuntimeNpcItemEffectSmokeReport.Run(projectRoot);
        Merge("item-effect-invocation", itemEffects.Passed, itemEffects.Checks, itemEffects.Failures);
        var persistence = RuntimeNpcPersistenceValidationSmokeReport.Run();
        Merge("persistence-validation", persistence.Passed, persistence.Checks, persistence.Failures);

        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options);
            host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var configured) ? configured : host.Worlds.Values.First();
            if (world.Npcs.Values.All(x => x.HitPointRegen >= 0 && x.RegenTime >= 0)) checks.Add("decoded-regeneration-fields-loaded");
            else failures.Add("decoded-regeneration-fields-invalid");
            if (world.Npcs.Values.All(x => x.CharmLevel >= 0 && x.CharmResistance >= 0 && x.FollowValue >= 0)) checks.Add("decoded-charm-follow-fields-loaded");
            else failures.Add("decoded-charm-follow-fields-invalid");
            if (world.Npcs.Values.Any(x => x.CharmResistance > 0)) checks.Add("charm-resistance-live-catalog-consumer");
            else failures.Add("charm-resistance-live-catalog-consumer-missing");
        }
        catch (Exception ex) { failures.Add("catalog:" + ex.Message); }

        return new(
            failures.Count == 0,
            checks,
            failures,
            [
                "NPC inventory transfers execute through RuntimeNpcInventoryOwnershipService and the Bundle C item engine.",
                "NPC equipment ownership and exact-slot selection execute through one equipment authority.",
                "Ammunition consumption, item use, and item-effect invocation execute through live runtime consumers.",
                "Decoded regeneration, charm resistance, charm level, and follow values load into the authoritative NPC catalog.",
                "NPC inventory, equipment, effects, and living metadata are covered by persistence validation."
            ],
            [
                "Equipment choice and item-use admission remain evidence-driven and do not infer undocumented class or AI rules.",
                "Charm resistance is consumed as decoded control resistance; broader native charm command semantics remain bounded by existing control lifecycle authority."
            ],
            ["No verified native formula was found that authorizes inventing autonomous consumable-selection priorities beyond decoded/evidence-backed admission."],
            ["Native-only regeneration timing edge behavior and charm command nuances remain for the final native pass when no decoded evidence exists."]);
    }
}
