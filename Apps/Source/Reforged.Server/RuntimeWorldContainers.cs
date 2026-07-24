using System.Text.Json;

namespace Reforged.Server;

public enum RuntimeContainerKind { Chest, Locker, Cabinet, RoomContainer, GuildStorage, BankStorage, QuestContainer, Corpse, EventContainer, HiddenCache }
public enum RuntimeContainerScope { WorldShared, PerCharacter, Organization }
public enum RuntimeContainerPersistence { Ephemeral, WorldState }

public sealed record RuntimeContainerDefinition(
    string ContainerId,
    RuntimeContainerKind Kind,
    string OwnerId,
    RuntimeContainerScope Scope,
    int CapacitySlots,
    long MaximumWeight,
    IReadOnlySet<string> AllowedItemKinds,
    IReadOnlySet<string> DeniedItemIds,
    bool InitiallyLocked,
    string? RequiredKeyId,
    string? TrapId,
    TimeSpan? DecayAfter,
    TimeSpan? ResetAfter,
    RuntimeContainerPersistence Persistence,
    IReadOnlyDictionary<string, int> ResetContents);

public sealed record RuntimeContainerItem(string ItemInstanceId, string ItemId, string ItemKind, int Quantity, long UnitWeight, string OwnerId);
public sealed record RuntimeContainerActor(string ActorId, string? OrganizationId, IReadOnlySet<string> KeyIds, DateTimeOffset NowUtc);
public sealed record RuntimeContainerState(string ContainerId, bool Locked, IReadOnlyList<RuntimeContainerItem> Items, DateTimeOffset? DecayAtUtc, DateTimeOffset? ResetAtUtc, long Revision);
public sealed record RuntimeContainerTransferRequest(string TransferId, string ContainerId, string ActorId, string Direction, RuntimeContainerItem Item, long ExpectedRevision);
public sealed record RuntimeContainerDecision(bool Allowed, string Code, RuntimeContainerState State, RuntimeContainerTransferRequest? TransferRequest, string? TrapTriggerRequest, bool EvidenceOnly);
public sealed record RuntimeContainerSnapshot(int SchemaVersion, IReadOnlyList<RuntimeContainerState> Containers, DateTimeOffset SavedAtUtc);

public sealed class RuntimeWorldContainerService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeContainerDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeContainerState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistencePath;

    public RuntimeWorldContainerService(IEnumerable<RuntimeContainerDefinition> definitions, string? persistencePath = null)
    {
        _persistencePath = persistencePath;
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!_definitions.TryAdd(definition.ContainerId, definition)) throw new InvalidOperationException("duplicate-container-id:" + definition.ContainerId);
            _states[definition.ContainerId] = InitialState(definition, DateTimeOffset.UtcNow);
        }
        Restore();
    }

    public RuntimeContainerDecision Inspect(string containerId, RuntimeContainerActor actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var access = ValidateScope(definition, actor);
            if (access is not null) return Deny(access, state);
            return Allow("container-inspected", state);
        }
    }

    public RuntimeContainerDecision Unlock(string containerId, RuntimeContainerActor actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var access = ValidateScope(definition, actor);
            if (access is not null) return Deny(access, state);
            if (!state.Locked) return Allow("container-already-unlocked", state);
            if (string.IsNullOrWhiteSpace(definition.RequiredKeyId)) return Deny("container-unlock-unsupported", state, evidenceOnly: true);
            if (!actor.KeyIds.Contains(definition.RequiredKeyId)) return Deny("container-key-required", state);
            var next = state with { Locked = false, Revision = state.Revision + 1 };
            _states[containerId] = next;
            SaveLocked();
            return new(true, "container-unlocked", next, null, definition.TrapId, false);
        }
    }

    public RuntimeContainerDecision Lock(string containerId, RuntimeContainerActor actor)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var access = ValidateScope(definition, actor);
            if (access is not null) return Deny(access, state);
            if (state.Locked) return Allow("container-already-locked", state);
            if (!string.IsNullOrWhiteSpace(definition.RequiredKeyId) && !actor.KeyIds.Contains(definition.RequiredKeyId)) return Deny("container-key-required", state);
            var next = state with { Locked = true, Revision = state.Revision + 1 };
            _states[containerId] = next;
            SaveLocked();
            return Allow("container-locked", next);
        }
    }

    public RuntimeContainerDecision RequestDeposit(string containerId, RuntimeContainerActor actor, RuntimeContainerItem item)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var failure = ValidateTransfer(definition, state, actor, item, depositing: true);
            if (failure is not null) return Deny(failure, state);
            var request = new RuntimeContainerTransferRequest(Guid.NewGuid().ToString("N"), containerId, actor.ActorId, "deposit", item, state.Revision);
            return new(true, "container-deposit-requested", state, request, definition.TrapId, false);
        }
    }

    public RuntimeContainerDecision RequestWithdrawal(string containerId, RuntimeContainerActor actor, string itemInstanceId, int quantity)
    {
        lock (_gate)
        {
            TickLocked(actor.NowUtc);
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var access = ValidateScope(definition, actor);
            if (access is not null) return Deny(access, state);
            if (state.Locked) return Deny("container-locked", state);
            if (quantity <= 0) return Deny("container-quantity-invalid", state);
            var stored = state.Items.FirstOrDefault(i => i.ItemInstanceId.Equals(itemInstanceId, StringComparison.OrdinalIgnoreCase));
            if (stored is null) return Deny("container-item-not-found", state);
            if (stored.Quantity < quantity) return Deny("container-quantity-unavailable", state);
            var requested = stored with { Quantity = quantity };
            var request = new RuntimeContainerTransferRequest(Guid.NewGuid().ToString("N"), containerId, actor.ActorId, "withdraw", requested, state.Revision);
            return new(true, "container-withdrawal-requested", state, request, definition.TrapId, false);
        }
    }

    public RuntimeContainerDecision Commit(RuntimeContainerTransferRequest request)
    {
        lock (_gate)
        {
            if (!TryResolve(request.ContainerId, out _, out var state)) return Missing();
            if (state.Revision != request.ExpectedRevision) return Deny("container-revision-conflict", state);
            var items = state.Items.ToList();
            if (request.Direction.Equals("deposit", StringComparison.OrdinalIgnoreCase))
            {
                var index = items.FindIndex(i => i.ItemInstanceId.Equals(request.Item.ItemInstanceId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) items[index] = items[index] with { Quantity = checked(items[index].Quantity + request.Item.Quantity) };
                else items.Add(request.Item);
            }
            else if (request.Direction.Equals("withdraw", StringComparison.OrdinalIgnoreCase))
            {
                var index = items.FindIndex(i => i.ItemInstanceId.Equals(request.Item.ItemInstanceId, StringComparison.OrdinalIgnoreCase));
                if (index < 0 || items[index].Quantity < request.Item.Quantity) return Deny("container-item-changed", state);
                var remaining = items[index].Quantity - request.Item.Quantity;
                if (remaining == 0) items.RemoveAt(index); else items[index] = items[index] with { Quantity = remaining };
            }
            else return Deny("container-transfer-direction-invalid", state);
            var next = state with { Items = items.ToArray(), Revision = state.Revision + 1 };
            _states[request.ContainerId] = next;
            SaveLocked();
            return Allow("container-transfer-committed", next);
        }
    }

    public RuntimeContainerDecision Reset(string containerId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!TryResolve(containerId, out var definition, out var state)) return Missing();
            var next = InitialState(definition, nowUtc) with { Revision = state.Revision + 1 };
            _states[containerId] = next;
            SaveLocked();
            return Allow("container-reset", next);
        }
    }

    public IReadOnlyList<string> Tick(DateTimeOffset nowUtc) { lock (_gate) return TickLocked(nowUtc); }

    public IReadOnlyList<string> ValidateState()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var definition in _definitions.Values)
            {
                if (!_states.TryGetValue(definition.ContainerId, out var state)) { failures.Add("missing-state:" + definition.ContainerId); continue; }
                if (state.Items.Count > definition.CapacitySlots) failures.Add("capacity-exceeded:" + definition.ContainerId);
                if (TotalWeight(state.Items) > definition.MaximumWeight) failures.Add("weight-exceeded:" + definition.ContainerId);
                if (state.Items.Any(i => i.Quantity <= 0 || i.UnitWeight < 0)) failures.Add("invalid-item:" + definition.ContainerId);
            }
            return failures;
        }
    }

    private string? ValidateTransfer(RuntimeContainerDefinition definition, RuntimeContainerState state, RuntimeContainerActor actor, RuntimeContainerItem item, bool depositing)
    {
        var access = ValidateScope(definition, actor);
        if (access is not null) return access;
        if (state.Locked) return "container-locked";
        if (item.Quantity <= 0 || item.UnitWeight < 0) return "container-item-invalid";
        if (depositing && !item.OwnerId.Equals(actor.ActorId, StringComparison.OrdinalIgnoreCase)) return "container-item-ownership-invalid";
        if (definition.DeniedItemIds.Contains(item.ItemId)) return "container-item-denied";
        if (definition.AllowedItemKinds.Count > 0 && !definition.AllowedItemKinds.Contains(item.ItemKind)) return "container-item-kind-denied";
        var newSlot = !state.Items.Any(i => i.ItemInstanceId.Equals(item.ItemInstanceId, StringComparison.OrdinalIgnoreCase));
        if (newSlot && state.Items.Count >= definition.CapacitySlots) return "container-capacity-exceeded";
        if (checked(TotalWeight(state.Items) + item.UnitWeight * item.Quantity) > definition.MaximumWeight) return "container-weight-exceeded";
        return null;
    }

    private static string? ValidateScope(RuntimeContainerDefinition definition, RuntimeContainerActor actor)
    {
        if (definition.Scope == RuntimeContainerScope.PerCharacter && !definition.OwnerId.Equals(actor.ActorId, StringComparison.OrdinalIgnoreCase)) return "container-character-access-denied";
        if (definition.Scope == RuntimeContainerScope.Organization && !definition.OwnerId.Equals(actor.OrganizationId, StringComparison.OrdinalIgnoreCase)) return "container-organization-access-denied";
        return null;
    }

    private IReadOnlyList<string> TickLocked(DateTimeOffset nowUtc)
    {
        var changed = new List<string>();
        foreach (var definition in _definitions.Values)
        {
            var state = _states[definition.ContainerId];
            if (state.ResetAtUtc is not null && state.ResetAtUtc <= nowUtc)
            {
                _states[definition.ContainerId] = InitialState(definition, nowUtc) with { Revision = state.Revision + 1 };
                changed.Add(definition.ContainerId + ":reset");
            }
            else if (state.DecayAtUtc is not null && state.DecayAtUtc <= nowUtc)
            {
                _states[definition.ContainerId] = state with { Items = Array.Empty<RuntimeContainerItem>(), DecayAtUtc = null, Revision = state.Revision + 1 };
                changed.Add(definition.ContainerId + ":decayed");
            }
        }
        if (changed.Count > 0) SaveLocked();
        return changed;
    }

    private static RuntimeContainerState InitialState(RuntimeContainerDefinition definition, DateTimeOffset nowUtc)
    {
        var items = definition.ResetContents.Select(pair => new RuntimeContainerItem("reset:" + definition.ContainerId + ":" + pair.Key, pair.Key, "catalog", pair.Value, 0, definition.OwnerId)).ToArray();
        return new(definition.ContainerId, definition.InitiallyLocked, items,
            definition.DecayAfter is null ? null : nowUtc + definition.DecayAfter,
            definition.ResetAfter is null ? null : nowUtc + definition.ResetAfter, 0);
    }

    private static long TotalWeight(IEnumerable<RuntimeContainerItem> items) => items.Sum(i => checked(i.UnitWeight * i.Quantity));
    private bool TryResolve(string id, out RuntimeContainerDefinition definition, out RuntimeContainerState state)
    {
        var definitionFound = _definitions.TryGetValue(id, out definition!);
        var stateFound = _states.TryGetValue(id, out state!);
        return definitionFound && stateFound;
    }
    private static void ValidateDefinition(RuntimeContainerDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.ContainerId)) throw new InvalidOperationException("container-id-required");
        if (string.IsNullOrWhiteSpace(d.OwnerId)) throw new InvalidOperationException("container-owner-required:" + d.ContainerId);
        if (d.CapacitySlots <= 0) throw new InvalidOperationException("container-capacity-invalid:" + d.ContainerId);
        if (d.MaximumWeight < 0) throw new InvalidOperationException("container-weight-invalid:" + d.ContainerId);
        if (d.ResetContents.Any(p => p.Value <= 0)) throw new InvalidOperationException("container-reset-content-invalid:" + d.ContainerId);
    }
    private static RuntimeContainerDecision Missing() => new(false, "container-not-found", new("missing", false, Array.Empty<RuntimeContainerItem>(), null, null, 0), null, null, false);
    private static RuntimeContainerDecision Allow(string code, RuntimeContainerState state) => new(true, code, state, null, null, false);
    private static RuntimeContainerDecision Deny(string code, RuntimeContainerState state, bool evidenceOnly = false) => new(false, code, state, null, null, evidenceOnly);

    private void Restore()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath)) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<RuntimeContainerSnapshot>(File.ReadAllText(_persistencePath));
            if (snapshot?.SchemaVersion != 1) return;
            foreach (var state in snapshot.Containers)
                if (_definitions.TryGetValue(state.ContainerId, out var definition) && definition.Persistence == RuntimeContainerPersistence.WorldState)
                    _states[state.ContainerId] = state;
        }
        catch { }
    }
    private void SaveLocked()
    {
        if (string.IsNullOrWhiteSpace(_persistencePath)) return;
        var states = _states.Values.Where(s => _definitions[s.ContainerId].Persistence == RuntimeContainerPersistence.WorldState).ToArray();
        var snapshot = new RuntimeContainerSnapshot(1, states, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_persistencePath) ?? ".");
        var temp = _persistencePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _persistencePath, true);
    }
}
