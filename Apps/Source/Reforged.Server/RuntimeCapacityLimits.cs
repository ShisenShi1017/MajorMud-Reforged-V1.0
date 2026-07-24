namespace Reforged.Server;

public enum RuntimeCapacityResource
{
    Users, Characters, NpcInstances, Objects, Corpses, Timers, Parties, Guilds, Messages, ActiveEvents, WorldStateEntries
}

public sealed record RuntimeCapacityDefinition(
    RuntimeCapacityResource Resource,
    long Maximum,
    string OwnerSubsystem,
    bool Configurable = true,
    bool HardLimit = true,
    string? NativeWidth = null);

public sealed record RuntimeCapacitySnapshot(RuntimeCapacityResource Resource, long Used, long Reserved, long Maximum, long Available);
public sealed record RuntimeCapacityResult(bool Success, string Code, RuntimeCapacitySnapshot? Snapshot = null);

public sealed class RuntimeCapacityLimitsService
{
    private readonly Dictionary<RuntimeCapacityResource, RuntimeCapacityDefinition> _definitions = new();
    private readonly Dictionary<RuntimeCapacityResource, long> _used = new();
    private readonly Dictionary<RuntimeCapacityResource, long> _reserved = new();

    public RuntimeCapacityLimitsService(IEnumerable<RuntimeCapacityDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (definition.Maximum < 0) throw new ArgumentException($"capacity-negative-limit:{definition.Resource}");
            if (string.IsNullOrWhiteSpace(definition.OwnerSubsystem)) throw new ArgumentException($"capacity-owner-required:{definition.Resource}");
            if (!_definitions.TryAdd(definition.Resource, definition)) throw new ArgumentException($"capacity-duplicate:{definition.Resource}");
            _used[definition.Resource] = 0;
            _reserved[definition.Resource] = 0;
        }
    }

    public RuntimeCapacityResult Inspect(RuntimeCapacityResource resource)
        => _definitions.ContainsKey(resource) ? new(true, "capacity-inspected", Snapshot(resource)) : new(false, "capacity-resource-unknown");

    public RuntimeCapacityResult SetUsage(string ownerSubsystem, RuntimeCapacityResource resource, long used)
    {
        if (!TryAuthorize(ownerSubsystem, resource, out var denied)) return denied;
        if (used < 0) return new(false, "capacity-usage-negative");
        var definition = _definitions[resource];
        if (definition.HardLimit && used + _reserved[resource] > definition.Maximum) return new(false, "capacity-limit-exceeded", Snapshot(resource));
        _used[resource] = used;
        return new(true, "capacity-usage-updated", Snapshot(resource));
    }

    public RuntimeCapacityResult Reserve(string ownerSubsystem, RuntimeCapacityResource resource, long amount)
    {
        if (!TryAuthorize(ownerSubsystem, resource, out var denied)) return denied;
        if (amount <= 0) return new(false, "capacity-reservation-invalid");
        var definition = _definitions[resource];
        try
        {
            var next = checked(_used[resource] + _reserved[resource] + amount);
            if (definition.HardLimit && next > definition.Maximum) return new(false, "capacity-limit-exceeded", Snapshot(resource));
            _reserved[resource] = checked(_reserved[resource] + amount);
            return new(true, "capacity-reserved", Snapshot(resource));
        }
        catch (OverflowException) { return new(false, "capacity-arithmetic-overflow", Snapshot(resource)); }
    }

    public RuntimeCapacityResult Commit(string ownerSubsystem, RuntimeCapacityResource resource, long amount)
    {
        if (!TryAuthorize(ownerSubsystem, resource, out var denied)) return denied;
        if (amount <= 0 || amount > _reserved[resource]) return new(false, "capacity-commit-invalid", Snapshot(resource));
        try
        {
            _reserved[resource] -= amount;
            _used[resource] = checked(_used[resource] + amount);
            return new(true, "capacity-committed", Snapshot(resource));
        }
        catch (OverflowException) { return new(false, "capacity-arithmetic-overflow", Snapshot(resource)); }
    }

    public RuntimeCapacityResult Release(string ownerSubsystem, RuntimeCapacityResource resource, long amount, bool fromUsage = false)
    {
        if (!TryAuthorize(ownerSubsystem, resource, out var denied)) return denied;
        if (amount <= 0) return new(false, "capacity-release-invalid", Snapshot(resource));
        var source = fromUsage ? _used : _reserved;
        if (amount > source[resource]) return new(false, "capacity-release-exceeds-current", Snapshot(resource));
        source[resource] -= amount;
        return new(true, fromUsage ? "capacity-usage-released" : "capacity-reservation-released", Snapshot(resource));
    }

    public RuntimeCapacityResult ChangeMaximum(string actorSubsystem, RuntimeCapacityResource resource, long maximum, bool hostAuthorized)
    {
        if (!hostAuthorized) return new(false, "capacity-host-authorization-required");
        if (!_definitions.TryGetValue(resource, out var definition)) return new(false, "capacity-resource-unknown");
        if (!definition.Configurable) return new(false, "capacity-limit-not-configurable", Snapshot(resource));
        if (maximum < 0) return new(false, "capacity-negative-limit", Snapshot(resource));
        if (definition.HardLimit && _used[resource] + _reserved[resource] > maximum) return new(false, "capacity-limit-below-current", Snapshot(resource));
        _definitions[resource] = definition with { Maximum = maximum };
        return new(true, "capacity-limit-updated", Snapshot(resource));
    }

    public IReadOnlyList<string> ValidateState()
    {
        var issues = new List<string>();
        foreach (var resource in _definitions.Keys)
        {
            var d = _definitions[resource];
            if (_used[resource] < 0 || _reserved[resource] < 0) issues.Add($"capacity-negative-state:{resource}");
            if (d.HardLimit && _used[resource] + _reserved[resource] > d.Maximum) issues.Add($"capacity-over-limit:{resource}");
        }
        return issues;
    }

    private bool TryAuthorize(string ownerSubsystem, RuntimeCapacityResource resource, out RuntimeCapacityResult denied)
    {
        if (!_definitions.TryGetValue(resource, out var definition)) { denied = new(false, "capacity-resource-unknown"); return false; }
        if (!string.Equals(ownerSubsystem, definition.OwnerSubsystem, StringComparison.OrdinalIgnoreCase)) { denied = new(false, "capacity-owner-mismatch", Snapshot(resource)); return false; }
        denied = new(true, "capacity-authorized"); return true;
    }

    private RuntimeCapacitySnapshot Snapshot(RuntimeCapacityResource resource)
    {
        var d = _definitions[resource]; var used = _used[resource]; var reserved = _reserved[resource];
        var available = d.Maximum >= used + reserved ? d.Maximum - used - reserved : 0;
        return new(resource, used, reserved, d.Maximum, available);
    }
}
