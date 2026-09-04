namespace RealmGate.App.Brains;

public sealed record BorinEquipmentInventoryPlan(
    string Phase,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<string> Evidence,
    string Detail)
{
    public string PromptText
    {
        get
        {
            var priorities = Priorities.Count == 0
                ? "- No equipment/economy priority is currently available."
                : string.Join("\n", Priorities.Select((value, index) => $"{index + 1}. {value}"));
            var evidence = Evidence.Count == 0
                ? "- No equipment/economy-specific evidence is currently available."
                : string.Join("\n", Evidence.Select(value => $"- {value}"));
            return $"Equipment/economy phase: {Phase}\n{Detail}\nPriorities:\n{priorities}\nSupporting evidence:\n{evidence}";
        }
    }

    public static BorinEquipmentInventoryPlan Disabled() => new(
        "disabled",
        Array.Empty<string>(),
        Array.Empty<string>(),
        "Autonomous equipment, inventory, and shop planning is disabled.");
}

/// <summary>
/// Deterministic policy layer for Borin's ordinary-player inventory/equipment/economy decisions.
/// It never sends commands. Its main purpose is to make autonomous ownership changes deliberate,
/// reversible when possible, and grounded in verified live state plus Librarium/personal evidence.
/// </summary>
public static class BorinEquipmentInventoryPlanner
{
    public static BorinEquipmentInventoryPlan Build(
        BorinGameStateSnapshot state,
        BorinGameplayLearningSnapshot personalExperience,
        BorinLibrariumPlayerGuidance librarium)
    {
        var priorities = new List<string>();
        var evidence = new List<string>();
        var phase = ResolvePhase(state);

        var inventoryCaptured = IsCaptured(state.InventoryFreshness);
        var equipmentCaptured = IsCaptured(state.EquipmentFreshness);

        if (!inventoryCaptured)
            priorities.Add("Refresh INVENTORY before buying, selling, dropping, giving, using, or reorganizing possessions; stale or missing inventory is not a safe basis for ownership changes.");
        if (!equipmentCaptured)
            priorities.Add("Refresh EQUIPMENT before replacing or removing gear; do not infer equipped slots from remembered state or item names.");

        if (state.InCombat)
        {
            priorities.Add("Finish the live combat/survival problem before routine shopping or inventory cleanup. Use an item only when its purpose is sufficiently verified for the immediate situation.");
        }
        else
        {
            if (equipmentCaptured && state.EquippedItems.Count == 0)
                priorities.Add("Determine whether usable owned gear should be equipped before spending money on replacements; verify the resulting EQUIPMENT state after each deliberate change.");

            var currentRoomShop = librarium.Evidence
                .Where(value => value.Contains("SHOP IN CURRENT ROOM", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToArray();
            foreach (var value in currentRoomShop)
                evidence.Add($"LIBRARIUM • {value}");

            if (currentRoomShop.Length > 0)
                priorities.Add("A Librarium shop reference matches the current room. Use the live LIST command to verify the actual service/stock before BUY or SELL; Librarium inventory is reference data, not proof of current stock or price.");

            var shopCandidates = librarium.Evidence
                .Where(value => value.Contains("SHOP CANDIDATE", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToArray();
            foreach (var value in shopCandidates)
                evidence.Add($"LIBRARIUM • {value}");

            if (shopCandidates.Length > 0 && currentRoomShop.Length == 0)
                priorities.Add("Treat referenced shops as destinations/opportunities only. Do not invent a route; reach them through verified navigation and confirm the shop with normal live commands.");
        }

        var itemReferences = librarium.Evidence
            .Where(value => value.Contains("ITEM REFERENCE", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToArray();
        foreach (var value in itemReferences)
            evidence.Add($"LIBRARIUM • {value}");

        var personalItemLessons = personalExperience.RelevantLessons
            .Where(value => value.Contains("item", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("equip", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("weapon", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("armor", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("bought", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("purchase", StringComparison.OrdinalIgnoreCase) ||
                            value.Contains("sell", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        foreach (var value in personalItemLessons)
            evidence.Add($"PERSONAL • {value}");

        if (itemReferences.Length > 0)
            priorities.Add("Compare owned/equipped items using evidence-backed stats and requirements, not names alone. A candidate is not an upgrade until Borin can use it and the relevant combat/survival tradeoff is understood.");

        if (state.CarriedCurrency <= 0)
            evidence.Add("LIVE • carried currency is zero or not captured; do not assume a purchase is affordable.");
        else
            evidence.Add($"LIVE • captured carried currency: {state.CarriedCurrency:N0}; the server still decides actual affordability and price.");

        if (inventoryCaptured)
            evidence.Add($"LIVE • inventory captured with {state.InventoryItems.Count} entries.");
        if (equipmentCaptured)
            evidence.Add($"LIVE • equipment captured with {state.EquippedItems.Count} entries.");

        priorities.Add("For a purchase: verify live LIST, compare the candidate, BUY one item, refresh INVENTORY, deliberately EQUIP/WEAR if appropriate, then refresh EQUIPMENT before judging success.");
        priorities.Add("Do not SELL, DROP, GIVE, PUT, or otherwise dispose of an item unless evidence establishes it is safe to lose. If it might be a quest item, key, limited item, upgrade component, or otherwise important, keep it until verified.");
        priorities.Add("Avoid repeated buying/selling loops. Re-evaluate only after live inventory, equipment, currency, shop stock, progression needs, or verified item-performance evidence materially changes.");

        return new BorinEquipmentInventoryPlan(
            phase,
            priorities.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(),
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            "This plan does not grant item facts or shop stock. Live server output controls ownership, stock, prices, and successful equip/use actions; Librarium data is read-only reference evidence and Borin's personal history is character-specific experience.");
    }

    private static bool IsCaptured(string freshness) =>
        !string.IsNullOrWhiteSpace(freshness) &&
        !freshness.Contains("not captured", StringComparison.OrdinalIgnoreCase) &&
        !freshness.Contains("not live", StringComparison.OrdinalIgnoreCase) &&
        !freshness.Contains("unknown", StringComparison.OrdinalIgnoreCase);

    private static string ResolvePhase(BorinGameStateSnapshot state)
    {
        if (!state.HasVerifiedPrompt || !state.HasVerifiedRoom) return "re-establish live perception";
        if (state.InCombat) return "survival before economy";
        if (!IsCaptured(state.InventoryFreshness) || !IsCaptured(state.EquipmentFreshness)) return "verify possessions and equipment";
        if (state.EquippedItems.Count == 0) return "establish basic equipment";
        return "evaluate upgrades and manage resources";
    }
}
