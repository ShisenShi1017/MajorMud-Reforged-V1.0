namespace RealmGate.Server;

public sealed partial class RuntimeItemCatalog
{
    /// <summary>
    /// Returns the decoded equipment slot from equipment.json.
    /// No item-name or item-type inference is performed.
    /// </summary>
    public string? ResolveClassicEquipmentSlot(string itemId) =>
        _equipment.TryGetValue(itemId, out var relationship) ? relationship.Slot : null;
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private static RuntimeItemInstance ResolveClassicEquipmentCandidate(
        IEnumerable<RuntimeItemInstance> source,
        RuntimeItemCatalog catalog,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("An item name is required.");

        var q = query.Trim();
        var inventory = source.ToArray();

        static bool WordStarts(string name, string value) =>
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(word => word.StartsWith(value, StringComparison.OrdinalIgnoreCase));

        var exact = inventory.Where(item =>
        {
            var definition = catalog.Require(item.CanonicalItemId);
            return item.InstanceId.Equals(q, StringComparison.OrdinalIgnoreCase)
                || item.CanonicalItemId.Equals(q, StringComparison.OrdinalIgnoreCase)
                || definition.Name.Equals(q, StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        if (exact.Length > 0) return exact[0];

        var matches = inventory.Where(item =>
        {
            var name = catalog.Require(item.CanonicalItemId).Name;
            return name.StartsWith(q, StringComparison.OrdinalIgnoreCase) || WordStarts(name, q);
        }).ToArray();

        if (matches.Length == 0)
            throw new InvalidOperationException($"No matching item: {q}.");

        return matches.FirstOrDefault(item => item.EquippedSlot is null) ?? matches[0];
    }
}
