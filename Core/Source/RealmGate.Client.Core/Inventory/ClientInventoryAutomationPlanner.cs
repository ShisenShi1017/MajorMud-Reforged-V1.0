namespace RealmGate.Client.Inventory;

public static class ClientInventoryAutomationPlanner
{
    private static readonly HashSet<string> AmbiguousSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "FINGER", "WRIST"
    };

    public static ClientEquipmentSet CaptureEquipmentSet(
        ClientInventoryStateSnapshot snapshot,
        string name)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var set = new ClientEquipmentSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Captured Set" : name.Trim()
        };
        var order = 0;
        foreach (var item in snapshot.Equipment)
        {
            set.Items.Add(new ClientEquipmentSetEntry
            {
                ItemId = item.ItemId,
                ItemName = item.Name,
                Slot = item.Slot,
                Order = order++
            });
        }
        return set;
    }

    public static ClientInventoryAutomationPlan PlanEquipmentSet(
        ClientInventoryStateSnapshot snapshot,
        ClientInventoryAutomationProfile profile,
        ClientEquipmentSet set)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(set);

        var desired = set.Items.Where(item => item.ItemName.Length > 0)
            .OrderBy(item => item.Order).ToArray();
        var desiredNames = desired.Select(item => Canonical(item.ItemName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var controlledSlots = desired.Select(item => item.Slot.Trim())
            .Where(slot => slot.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var commands = new List<string>();
        var missing = new List<string>();

        foreach (var current in snapshot.Equipment)
        {
            if (current.Slot.Length == 0 || AmbiguousSlots.Contains(current.Slot) ||
                !controlledSlots.Contains(current.Slot) || desiredNames.Contains(Canonical(current.Name)))
                continue;
            Add(commands, ApplyTemplate(profile.Preferences.RemoveCommandTemplate, current.Name, 1, 0));
        }

        foreach (var requested in desired)
        {
            if (snapshot.Equipment.Any(item => Matches(item, requested)))
                continue;
            if (!snapshot.Inventory.Any(item => Matches(item, requested)))
            {
                missing.Add(requested.ItemName);
                continue;
            }
            Add(commands, ApplyTemplate(profile.Preferences.EquipCommandTemplate, requested.ItemName, 1, 0));
        }

        return new ClientInventoryAutomationPlan
        {
            Action = $"Apply equipment set '{set.Name}'",
            Commands = Limit(commands, profile.Preferences.MaximumCommandsPerPass),
            MissingItems = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public static ClientInventoryAutomationPlan PlanRules(
        ClientInventoryStateSnapshot snapshot,
        ClientInventoryAutomationProfile profile,
        ClientInventoryAutomationContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var commands = new List<string>();
        var warnings = new List<string>();
        foreach (var item in snapshot.Inventory)
        {
            var rule = profile.Rules.Where(rule => rule.Enabled && rule.Disposition != ClientInventoryDisposition.Buy)
                .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(rule => Matches(rule, item));
            if (rule is null || rule.Disposition == ClientInventoryDisposition.Keep)
                continue;

            switch (rule.Disposition)
            {
                case ClientInventoryDisposition.Sell when !profile.Preferences.AllowAutomaticSell:
                case ClientInventoryDisposition.Stash when !profile.Preferences.AllowAutomaticStash:
                case ClientInventoryDisposition.Discard when !profile.Preferences.AllowAutomaticDiscard:
                    warnings.Add($"{rule.Disposition} for {item.Name} is disabled.");
                    break;
                case ClientInventoryDisposition.Sell when !snapshot.ShopAvailable:
                    warnings.Add($"Sell for {item.Name} requires a proven shop in the current room.");
                    break;
                case ClientInventoryDisposition.Stash when !context.AtSavedStash:
                    warnings.Add($"Stash for {item.Name} requires the current room to be a saved stash.");
                    break;
                case ClientInventoryDisposition.Sell:
                    Add(commands, ApplyTemplate(profile.Preferences.SellCommandTemplate, item.Name, item.Quantity, 0));
                    break;
                case ClientInventoryDisposition.Stash:
                    Add(commands, ApplyTemplate(profile.Preferences.StashCommandTemplate, item.Name, item.Quantity, 0));
                    break;
                case ClientInventoryDisposition.Discard:
                    Add(commands, ApplyTemplate(profile.Preferences.DiscardCommandTemplate, item.Name, item.Quantity, 0));
                    break;
            }
        }

        foreach (var rule in profile.Rules.Where(rule => rule.Enabled && rule.Disposition == ClientInventoryDisposition.Buy)
                     .OrderBy(rule => rule.Priority))
        {
            if (!profile.Preferences.AllowAutomaticBuy)
            {
                warnings.Add($"Buy for {rule.MatchValue} is disabled.");
                continue;
            }
            if (!snapshot.ShopAvailable)
            {
                warnings.Add($"Buy for {rule.MatchValue} requires a proven shop in the current room.");
                continue;
            }
            var owned = snapshot.Inventory.Concat(snapshot.Equipment).Where(item => Matches(rule, item)).Sum(item => item.Quantity);
            var requested = Math.Max(0, rule.TargetQuantity - owned);
            if (requested > 0)
                Add(commands, ApplyTemplate(profile.Preferences.BuyCommandTemplate, rule.MatchValue, requested, 0));
        }

        if (profile.Preferences.AutoDepositExcessCurrency)
        {
            var amount = snapshot.CurrencyCopper - Math.Max(0, profile.Preferences.KeepCurrency);
            if (amount > 0 && !snapshot.BankAvailable)
                warnings.Add("Currency deposit requires a proven bank in the current room.");
            else if (amount > 0)
                Add(commands, ApplyTemplate(profile.Preferences.DepositExcessCommandTemplate, string.Empty, 1, amount));
        }

        return new ClientInventoryAutomationPlan
        {
            Action = "Process inventory rules",
            Commands = Limit(commands, profile.Preferences.MaximumCommandsPerPass),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public static string ApplyTemplate(string? template, string itemName, int quantity, long amount)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        return template.Trim()
            .Replace("{item}", itemName.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{quantity}", Math.Max(1, quantity).ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", Math.Max(0, amount).ToString(), StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool Matches(ClientInventoryRule rule, ClientInventoryItemState item)
    {
        var value = rule.MatchValue.Trim();
        if (value.Length == 0) return false;
        return rule.MatchMode switch
        {
            ClientInventoryRuleMatchMode.ExactName => item.Name.Equals(value, StringComparison.OrdinalIgnoreCase),
            ClientInventoryRuleMatchMode.NameContains => item.Name.Contains(value, StringComparison.OrdinalIgnoreCase),
            ClientInventoryRuleMatchMode.ItemId => item.ItemId.Equals(value, StringComparison.OrdinalIgnoreCase),
            ClientInventoryRuleMatchMode.Slot => item.Slot.Equals(value, StringComparison.OrdinalIgnoreCase),
            ClientInventoryRuleMatchMode.ItemType => item.ItemType.Equals(value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool Matches(ClientInventoryItemState item, ClientEquipmentSetEntry requested) =>
        requested.ItemId.Length > 0 && item.ItemId.Equals(requested.ItemId, StringComparison.OrdinalIgnoreCase) ||
        Canonical(item.Name).Equals(Canonical(requested.ItemName), StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static void Add(List<string> commands, string command)
    {
        if (command.Length > 0 && !commands.Contains(command, StringComparer.OrdinalIgnoreCase))
            commands.Add(command);
    }

    private static IReadOnlyList<string> Limit(IEnumerable<string> commands, int maximum) =>
        commands.Take(Math.Clamp(maximum, 1, 100)).ToArray();
}
