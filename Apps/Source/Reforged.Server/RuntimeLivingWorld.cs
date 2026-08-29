using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLivingNpcState(
    string InstanceId,
    string SpawnId,
    string NpcId,
    string Name,
    string RoomId,
    string HomeRoomId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool Alive,
    DateTimeOffset? RespawnAtUtc,
    string InteractionModel,
    int RespawnMinutes,
    IReadOnlyDictionary<string,string> Metadata);

public sealed record RuntimeLivingWorldObjectDefinition(
    string Id,
    string RoomId,
    string Kind,
    string Name,
    string? Direction,
    string? KeyItemId,
    bool InitiallyOpen,
    bool InitiallyLocked,
    string? TargetRoomId,
    IReadOnlyDictionary<string,string> Metadata);

public sealed record RuntimeLivingWorldObjectState(
    string Id,
    bool Open,
    bool Locked,
    bool Activated,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeWizardKnockDoor(
    RuntimeLivingWorldObjectDefinition Definition,
    RuntimeLivingWorldObjectState State,
    RuntimeLivingWorldObjectDefinition? ReciprocalDefinition,
    RuntimeLivingWorldObjectState? ReciprocalState);

public sealed record RuntimeLivingWorldSnapshot(
    string WorldId,
    IReadOnlyList<RuntimeLivingNpcState> Npcs,
    IReadOnlyList<RuntimeLivingWorldObjectState> Objects,
    DateTimeOffset SavedAtUtc,
    IReadOnlyList<RuntimeItemInstance>? Items = null);

public sealed record RuntimeSpawnPlacementIssue(
    string SpawnId,
    string SourceRoomReference,
    string ResolvedRuntimeRoomId,
    string Reason);

public sealed record RuntimeStaticRoomItemResetResult(
    int DefinitionCount,
    int ExistingCount,
    int GameLimitBlockedCount,
    int RestoredCount,
    IReadOnlyList<string> RestoredPlacementIds);

public sealed record RuntimeClassicNativeDeathFloorPlacement(
    bool Placed,
    string? RoomId,
    bool Hidden,
    bool Stacked,
    int? NativeSlot,
    string Stage,
    string? FloorInstanceId);

public static class RuntimeClassicSkillTest
{
    public static int SuccessChance(int skillValue,int modifier) => Math.Clamp(checked(skillValue + modifier),0,100);

    public static bool Passes(int skillValue,int modifier,int roll)
    {
        if(roll<1||roll>100)throw new ArgumentOutOfRangeException(nameof(roll));
        return roll<=SuccessChance(skillValue,modifier);
    }
}

/// <summary>
/// Authoritative shared runtime state for generated NPC spawns and generated world interaction objects.
/// The service never invents NPCs, routes, doors, keys, hazards, or triggers: all definitions are loaded
/// from generated Data/&lt;world&gt; files. Runtime state is persisted separately under the realm save root.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public bool ReplaceNpcMetadata(string instanceId, IReadOnlyDictionary<string, string> metadata)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            _npcs[current.InstanceId] = current with { Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase) };
            Save();
            return true;
        }
    }

    private readonly object _gate = new();
    private readonly string _projectRoot;
    private readonly RuntimeWorld _world;
    private readonly RuntimeItemCatalog _itemCatalog;
    private readonly string _persistenceRoot;
    private readonly Dictionary<string,RuntimeLivingNpcState> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectDefinition> _objectDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectState> _objects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeItemInstance> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeItemInstance> _staticRoomItemDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeSpawnPlacementIssue> _spawnPlacementIssues = [];

    public RuntimeLivingWorldService(string projectRoot, RuntimeWorld world, string? persistenceRoot = null)
    {
        _projectRoot = projectRoot;
        _world = world;
        _itemCatalog=RuntimeItemCatalog.Load(projectRoot,world.Definition.Id);
        _persistenceRoot = string.IsNullOrWhiteSpace(persistenceRoot)
            ? Path.Combine(RuntimeSavePaths.RealmRoot(projectRoot, world.Definition.Id), "WorldState")
            : persistenceRoot;
        LoadGeneratedNpcInstances();
        LoadGeneratedWorldObjects();
        LoadGeneratedRoomItems();
        InitializeLivingWorldCompletion();
        Restore();
        RestoreLivingWorldCompletion();
    }

    /// <summary>
    /// Applies the authored NPC HPRegeneration amount once for the recovered native
    /// 30-fast-tick slow generation. WCCMMUD staggers individual monster slow updates
    /// across host cycles after the generation boundary; that micro-order remains part
    /// of the broader scheduler assertion rather than being invented here.
    /// </summary>
    public int ApplyNativeSlowNpcRegeneration(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var changed = 0;
            foreach (var pair in _npcs.ToArray())
            {
                var npc = pair.Value;
                if (!npc.Alive || npc.CurrentHitPoints >= npc.MaximumHitPoints) continue;
                if (!npc.Metadata.TryGetValue("hp_regen", out var raw) || !int.TryParse(raw, out var amount) || amount <= 0) continue;
                var next = Math.Min(npc.MaximumHitPoints, checked(npc.CurrentHitPoints + amount));
                if (next == npc.CurrentHitPoints) continue;
                _npcs[pair.Key] = npc with { CurrentHitPoints = next };
                changed++;
            }
            if (changed > 0) Save();
            return changed;
        }
    }

    public string WorldId => _world.Definition.Id;
    public int NpcInstanceCount { get { lock (_gate) return _npcs.Count; } }
    public int WorldObjectCount { get { lock (_gate) return _objectDefinitions.Count; } }
    public int GroundItemCount { get { lock (_gate) return _items.Count; } }
    public IReadOnlyList<RuntimeRoom> ContestRooms(int mapId, int minimumRoom, int maximumRoom, int excludedAttributeMask)
    {
        lock (_gate) return _world.AllRooms().Where(room => room.MapId == mapId
            && room.RoomNumber >= minimumRoom && room.RoomNumber <= maximumRoom
            && (room.EnvironmentAttributes & excludedAttributeMask) == 0).ToArray();
    }
    public bool TryPlaceContestItem(string canonicalItemId, string roomId, bool hidden, int roomCapacity, string contestId)
    {
        lock (_gate)
        {
            var occupied = _items.Values.Count(item => item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(roomId, StringComparison.OrdinalIgnoreCase)
                && RuntimeClassicHiddenSearchService.IsHidden(item) == hidden);
            if (occupied >= roomCapacity || _world.FindRoomById(roomId) is null) return false;
            var definition = _itemCatalog.Require(canonicalItemId);
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["majormud_plus_contest"] = contestId,
                ["native_contest_item"] = "true"
            };
            if (hidden) metadata["hidden_at_cleanup"] = "true";
            var item = new RuntimeItemInstance(Guid.NewGuid().ToString("N"), definition.Id, null,
                RuntimeItemLocations.Room, roomId, null, definition.DefaultCharges, 1, null, metadata);
            _items.Add(item.InstanceId, item);
            Save();
            return true;
        }
    }
    public int RemoveGroundItems(string canonicalItemId)
    {
        lock (_gate)
        {
            var ids = _items.Values.Where(item => item.CanonicalItemId.Equals(canonicalItemId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.InstanceId).ToArray();
            foreach (var id in ids) _items.Remove(id);
            if (ids.Length > 0) Save();
            return ids.Length;
        }
    }
    public IReadOnlyList<RuntimeItemInstance> GroundItems(string roomId)
    {
        lock (_gate) return _items.Values.Where(item=>
            item.LocationKind.Equals(RuntimeItemLocations.Room,StringComparison.OrdinalIgnoreCase)
            &&item.LocationId.Equals(roomId,StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public RuntimeItemInstance TakeGroundItem(string instanceId,string characterId)
    {
        lock(_gate)
        {
            if(!_items.TryGetValue(instanceId,out var item))throw new InvalidOperationException("The room item is no longer present.");

            // Native room QTY stores extra copies beyond the first. Removing one copy decrements QTY
            // and leaves the slot in place until the final copy is removed. Keep generic Reforged
            // stacks unchanged; this split applies only to slots produced by the recovered native
            // room insertion contract.
            var nativeRoomStack = item.Metadata?.TryGetValue("native_room_quantity_encoding", out var encoding) == true
                && encoding.Equals("extra_copies_beyond_first", StringComparison.OrdinalIgnoreCase);
            RuntimeItemInstance moved;
            if(nativeRoomStack && item.StackQuantity > 1)
            {
                var remainingMetadata=item.Metadata!.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
                remainingMetadata["native_room_qty"]=(item.StackQuantity-2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _items[instanceId]=item with{StackQuantity=item.StackQuantity-1,Metadata=remainingMetadata};
                var movedMetadata=item.Metadata!.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
                movedMetadata.Remove("native_room_slot");
                movedMetadata.Remove("native_room_qty");
                movedMetadata.Remove("native_room_quantity_encoding");
                movedMetadata.Remove("native_item_uses");
                movedMetadata.Remove("hidden_at_cleanup");
                movedMetadata.Remove("hidden_at_cleanup_utc");
                moved=new RuntimeItemInstance(Guid.NewGuid().ToString("N"),item.CanonicalItemId,characterId,
                    RuntimeItemLocations.Character,characterId,null,item.Charges,1,item.Durability,movedMetadata);
            }
            else
            {
                _items.Remove(instanceId);
                var movedMetadata=item.Metadata is null
                    ? null
                    : item.Metadata.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
                if(movedMetadata is not null)
                {
                    movedMetadata.Remove("native_room_slot");
                    movedMetadata.Remove("native_room_qty");
                    movedMetadata.Remove("native_room_quantity_encoding");
                    movedMetadata.Remove("native_item_uses");
                    movedMetadata.Remove("hidden_at_cleanup");
                    movedMetadata.Remove("hidden_at_cleanup_utc");
                }
                moved=item with{OwnerId=characterId,LocationKind=RuntimeItemLocations.Character,LocationId=characterId,
                    StackQuantity=1,Metadata=movedMetadata};
            }
            Save();
            return moved;
        }
    }
    public void DropGroundItem(RuntimeItemInstance item,string roomId)
    {
        lock(_gate)
        {
            var metadata=item.Metadata is null
                ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                : item.Metadata.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
            metadata.Remove("hidden_at_cleanup");
            metadata.Remove("hidden_at_cleanup_utc");
            metadata.Remove("revealed_by_pickup");
            _items[item.InstanceId]=item with{OwnerId=null,LocationKind=RuntimeItemLocations.Room,LocationId=roomId,EquippedSlot=null,Metadata=metadata};
            Save();
        }
    }
    public RuntimeClassicNativeDeathFloorPlacement DropNativeDeathGroundItemTree(
        IReadOnlyList<RuntimeItemInstance> items, string rootInstanceId, RuntimeSession session)
    {
        lock (_gate)
        {
            var root = items.FirstOrDefault(item => item.InstanceId.Equals(rootInstanceId, StringComparison.OrdinalIgnoreCase));
            if (root is null) throw new InvalidOperationException($"Native death-drop root '{rootInstanceId}' is unavailable.");
            var inventoryEmergencyFallback = root.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase);
            var placement = DisposeNativeDeathItemInRoom(root, session.CurrentRoom, 0, "death-room-dfs");

            if (!placement.Placed)
                placement = DisposeNativeDeathItemInTrail(root, session.ClassicMovementTrail);

            if (!placement.Placed && inventoryEmergencyFallback &&
                _world.FindRoom(RuntimeClassicNativeCharacterDeathAuthority.EmergencyInventorySpillMap,
                    RuntimeClassicNativeCharacterDeathAuthority.EmergencyInventorySpillRoom) is { } emergencyRoom)
            {
                placement = DisposeNativeDeathItemInRoom(root, emergencyRoom, 0, "inventory-emergency-room-164-dfs");
            }

            if (!placement.Placed)
                return placement with { Stage = inventoryEmergencyFallback ? "native-floor-exhausted-inventory-lost" : "native-floor-exhausted-secondary-lost" };

            // Reforged portable-container descendants are a compatibility extension around the
            // native root-slot behavior. Preserve their tree with the successfully placed root; if
            // the native root merged into an existing room slot, reparent immediate children to
            // that surviving room instance instead of orphaning them.
            var byContainer = items
                .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Push(rootInstanceId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!included.Add(id)) continue;
                if (byContainer.TryGetValue(id, out var children))
                    foreach (var child in children) pending.Push(child.InstanceId);
            }

            foreach (var item in items.Where(item => included.Contains(item.InstanceId) &&
                         !item.InstanceId.Equals(rootInstanceId, StringComparison.OrdinalIgnoreCase)))
            {
                var metadata = item.Metadata is null
                    ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                    : item.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                metadata["native_character_death_drop"] = "wccmmud_check_kill_user";
                var locationId = placement.Stacked && item.LocationId.Equals(rootInstanceId, StringComparison.OrdinalIgnoreCase)
                    ? placement.FloorInstanceId ?? rootInstanceId
                    : item.LocationId;
                _items[item.InstanceId] = item with { OwnerId = null, LocationId = locationId, EquippedSlot = null, Metadata = metadata };
            }
            Save();
            return placement;
        }
    }

    private RuntimeClassicNativeDeathFloorPlacement DisposeNativeDeathItemInRoom(
        RuntimeItemInstance root, RuntimeRoom room, int depth, string stage)
    {
        if (depth > RuntimeClassicNativeCharacterDeathAuthority.SpillMaximumDepth)
            return NativeDeathPlacementFailure(stage);

        // _dispose_of_item_in_room invokes the visible add-item owner twice before traversing.
        var placement = TryInsertNativeRoomItem(root, room, hidden: false, stage);
        if (placement.Placed) return placement;
        placement = TryInsertNativeRoomItem(root, room, hidden: false, stage);
        if (placement.Placed) return placement;

        foreach (var direction in RuntimeClassicNativeCharacterDeathAuthority.NativeSpillDirectionOrder)
        {
            var exit = _world.FindExit(room, direction);
            if (exit is null || exit.HasBlockedDestination || exit.TargetRoomNumber <= 0) continue;
            var exitType = exit.ExitType ?? 0;
            if (exitType == RuntimeClassicNativeCharacterDeathAuthority.ChangeMapExitRoomType ||
                exitType == RuntimeClassicNativeCharacterDeathAuthority.RemoteActionExitRoomType) continue;
            var target = _world.FindRoom(exit.TargetMapId, exit.TargetRoomNumber);
            if (target is null) continue;
            placement = DisposeNativeDeathItemInRoom(root, target, depth + 1, stage);
            if (placement.Placed) return placement;
        }
        return NativeDeathPlacementFailure(stage);
    }

    private RuntimeClassicNativeDeathFloorPlacement DisposeNativeDeathItemInTrail(
        RuntimeItemInstance root, IReadOnlyList<RuntimeRoomKey> trail)
    {
        foreach (var key in trail.Take(RuntimeClassicNativeCharacterDeathAuthority.MovementTrailLength))
        {
            var room = _world.FindRoom(key.MapId, key.RoomNumber);
            if (room is null) continue;
            var placement = TryInsertNativeRoomItem(root, room, hidden: false, "movement-trail-visible");
            if (placement.Placed) return placement;
            placement = TryInsertNativeRoomItem(root, room, hidden: true, "movement-trail-hidden");
            if (placement.Placed) return placement;
        }
        return NativeDeathPlacementFailure("movement-trail-exhausted");
    }

    private RuntimeClassicNativeDeathFloorPlacement TryInsertNativeRoomItem(
        RuntimeItemInstance incoming, RuntimeRoom room, bool hidden, string stage)
    {
        var definition = _itemCatalog.Require(incoming.CanonicalItemId);
        var nativeUses = RuntimeClassicNativeCharacterDeathAuthority.NativeItemUses(definition, incoming);
        var capacity = hidden
            ? RuntimeClassicNativeCharacterDeathAuthority.HiddenRoomItemSlots
            : RuntimeClassicNativeCharacterDeathAuthority.VisibleRoomItemSlots;
        var roomItems = _items.Values
            .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)
                && RuntimeClassicHiddenSearchService.IsHidden(item) == hidden)
            .OrderBy(NativeRoomSlotSortKey)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Native _add_item_to_room stacks solely on incoming ItemUses == -1 plus same item ID.
        // It does this before looking for an empty slot and does not consult a generic stackable flag.
        if (nativeUses == -1)
        {
            var stackTarget = roomItems.FirstOrDefault(item =>
                item.CanonicalItemId.Equals(incoming.CanonicalItemId, StringComparison.OrdinalIgnoreCase));
            if (stackTarget is not null)
            {
                var metadata = stackTarget.Metadata is null
                    ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                    : stackTarget.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var quantity = checked(Math.Max(1, stackTarget.StackQuantity) + Math.Max(1, incoming.StackQuantity));
                metadata["native_character_death_drop"] = "wccmmud_check_kill_user";
                metadata["native_room_quantity_encoding"] = "extra_copies_beyond_first";
                metadata["native_room_qty"] = (quantity - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["native_item_uses"] = "-1";
                _items[stackTarget.InstanceId] = stackTarget with { StackQuantity = quantity, Metadata = metadata };
                return new(true, room.Id, hidden, true, ReadNativeRoomSlot(stackTarget), stage, stackTarget.InstanceId);
            }
        }

        var slot = FirstFreeNativeRoomSlot(roomItems, capacity);
        if (slot < 0) return NativeDeathPlacementFailure(stage);
        var incomingMetadata = incoming.Metadata is null
            ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
            : incoming.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        incomingMetadata["native_character_death_drop"] = "wccmmud_check_kill_user";
        incomingMetadata["native_room_slot"] = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
        incomingMetadata["native_item_uses"] = nativeUses.ToString(System.Globalization.CultureInfo.InvariantCulture);
        incomingMetadata["native_room_quantity_encoding"] = "extra_copies_beyond_first";
        incomingMetadata["native_room_qty"] = Math.Max(0, incoming.StackQuantity - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        incomingMetadata.Remove("revealed_by_pickup");
        if (hidden) incomingMetadata["hidden_at_cleanup"] = "true";
        else
        {
            incomingMetadata.Remove("hidden_at_cleanup");
            incomingMetadata.Remove("hidden_at_cleanup_utc");
        }
        _items[incoming.InstanceId] = incoming with
        {
            OwnerId = null,
            LocationKind = RuntimeItemLocations.Room,
            LocationId = room.Id,
            EquippedSlot = null,
            Metadata = incomingMetadata
        };
        return new(true, room.Id, hidden, false, slot, stage, incoming.InstanceId);
    }

    private static int FirstFreeNativeRoomSlot(IReadOnlyList<RuntimeItemInstance> roomItems, int capacity)
    {
        var used = new HashSet<int>();
        var unassigned = 0;
        foreach (var item in roomItems)
        {
            var slot = ReadNativeRoomSlot(item);
            if (slot is >= 0 && slot < capacity) used.Add(slot.Value);
            else unassigned++;
        }
        for (var count = 0; count < unassigned; count++)
        {
            var conceptual = Enumerable.Range(0, capacity).FirstOrDefault(index => !used.Contains(index), -1);
            if (conceptual < 0) return -1;
            used.Add(conceptual);
        }
        return Enumerable.Range(0, capacity).FirstOrDefault(index => !used.Contains(index), -1);
    }

    private static int NativeRoomSlotSortKey(RuntimeItemInstance item) => ReadNativeRoomSlot(item) ?? int.MaxValue;

    private static int? ReadNativeRoomSlot(RuntimeItemInstance item) =>
        item.Metadata?.TryGetValue("native_room_slot", out var raw) == true && int.TryParse(raw, out var slot) ? slot : null;

    private static RuntimeClassicNativeDeathFloorPlacement NativeDeathPlacementFailure(string stage) =>
        new(false, null, false, false, null, stage, null);

    public void DropGroundItemTree(IReadOnlyList<RuntimeItemInstance> items, string rootInstanceId, string roomId)
    {
        lock (_gate)
        {
            var byContainer = items
                .Where(item => item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Push(rootInstanceId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!included.Add(id)) continue;
                if (byContainer.TryGetValue(id, out var children))
                    foreach (var child in children) pending.Push(child.InstanceId);
            }
            foreach (var item in items.Where(item => included.Contains(item.InstanceId)))
            {
                var metadata = item.Metadata is null
                    ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                    : item.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                metadata["character_death_scatter"] = "decoded_room_spell_ability_157";
                _items[item.InstanceId] = item.InstanceId.Equals(rootInstanceId, StringComparison.OrdinalIgnoreCase)
                    ? item with { OwnerId = null, LocationKind = RuntimeItemLocations.Room, LocationId = roomId, EquippedSlot = null, Metadata = metadata }
                    : item with { OwnerId = null, EquippedSlot = null, Metadata = metadata };
            }
            Save();
        }
    }
    public void MoveGroundItems(IReadOnlyList<string> instanceIds, string destinationRoomId)
    {
        lock (_gate)
        {
            foreach (var instanceId in instanceIds)
            {
                if (!_items.TryGetValue(instanceId, out var item))
                    throw new InvalidOperationException($"Scatter item '{instanceId}' is unavailable.");
                _items[instanceId] = item with { OwnerId = null, LocationKind = RuntimeItemLocations.Room,
                    LocationId = destinationRoomId, EquippedSlot = null };
            }
            Save();
        }
    }
    public bool TryConsumeGroundItem(string roomId,string canonicalItemId)
    {
        lock(_gate)
        {
            var item=_items.Values.FirstOrDefault(value=>value.LocationKind.Equals(RuntimeItemLocations.Room,StringComparison.OrdinalIgnoreCase)&&value.LocationId.Equals(roomId,StringComparison.OrdinalIgnoreCase)&&value.CanonicalItemId.Equals(canonicalItemId,StringComparison.OrdinalIgnoreCase));
            if(item is null)return false;
            _items.Remove(item.InstanceId);Save();return true;
        }
    }
    public RuntimeStaticRoomItemResetResult ResetGeneratedRoomItems(
        IReadOnlyDictionary<string,int>? externallyOwnedItemQuantities=null,
        bool? unlimitedItemsEnabled=null)
    {
        lock(_gate)
        {
            externallyOwnedItemQuantities??=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var unlimitedItems=unlimitedItemsEnabled
                ?? RuntimeClassicAdministrationPolicyStore.Load(_projectRoot,_world.Definition.Id).UnlimitedItemsEnabled;
            var invasionNpcIds=_npcs.Values
                .Where(npc=>npc.Metadata.TryGetValue("summoned_by",out var source)&&source.StartsWith("wcc-invasion:",StringComparison.OrdinalIgnoreCase))
                .Select(npc=>npc.InstanceId).ToArray();
            foreach(var instanceId in invasionNpcIds)
            {
                _npcs.Remove(instanceId);
                _npcAi.Remove(instanceId);
            }
            var existing=0;
            var limited=0;
            var restored=new List<string>();
            foreach(var definition in _staticRoomItemDefinitions.Values)
            {
                if(_items.ContainsKey(definition.InstanceId)){existing++;continue;}
                var itemDefinition=_itemCatalog.Require(definition.CanonicalItemId);
                var externalQuantity=externallyOwnedItemQuantities.TryGetValue(definition.CanonicalItemId,out var quantity)?Math.Max(0,quantity):0;
                var worldQuantity=_items.Values.Where(item=>item.CanonicalItemId.Equals(definition.CanonicalItemId,StringComparison.OrdinalIgnoreCase))
                    .Sum(item=>item.StackQuantity);
                if(!unlimitedItems&&itemDefinition.GameLimit>0&&checked(worldQuantity+externalQuantity)>=itemDefinition.GameLimit){limited++;continue;}
                _items[definition.InstanceId]=definition;
                restored.Add(definition.InstanceId);
            }
            if(restored.Count>0||invasionNpcIds.Length>0)Save();
            return new(_staticRoomItemDefinitions.Count,existing,limited,restored.Count,restored);
        }
    }
    public IReadOnlyList<RuntimeLivingWorldObjectDefinition> CaptureWorldObjectDefinitions()
    {
        lock (_gate) return _objectDefinitions.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<RuntimeSpawnPlacementIssue> CaptureSpawnPlacementIssues()
    {
        lock (_gate) return _spawnPlacementIssues.ToArray();
    }

    public IReadOnlyList<RuntimeLivingNpcState> NpcsInRoom(string roomId)
    {
        lock (_gate)
            return _npcs.Values.Where(x => x.Alive && x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RuntimeLivingNpcState SummonNpc(string npcId, string roomId, string source)
    {
        lock (_gate)
        {
            if (_world.FindRoomById(roomId) is null)
                throw new InvalidOperationException($"summon room {roomId} is not generated");
            var legacyNpcId = ParseLegacyNumber(npcId);
            if (legacyNpcId <= 0 || !_world.Npcs.TryGetValue(legacyNpcId, out var definition))
                throw new InvalidOperationException($"summon target {npcId} is not generated");

            var ordinal = _npcs.Values.Count(x => x.SpawnId.Equals(source, StringComparison.OrdinalIgnoreCase)) + 1;
            var instanceId = $"{source}:{npcId}:{ordinal}";
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source_room_reference"] = roomId,
                ["resolved_runtime_room_id"] = roomId,
                ["source_map_id"] = "0",
                ["source_room_number"] = "0",
                ["alignment"] = definition.Alignment,
                ["disposition"] = definition.Alignment.Contains("evil", StringComparison.OrdinalIgnoreCase) ? "hostile" : "neutral",
                ["combat_hostility_state"] = "idle",
                ["summoned_by"] = source,
                ["see_hidden"] = definition.SeeHidden ? "1" : "0",
                ["charm_level"] = definition.CharmLevel.ToString(),
                ["charm_resistance"] = definition.CharmResistance.ToString(),
                ["follow_value"] = definition.FollowValue.ToString(),
                ["regen_time"] = definition.RegenTime.ToString(),
                ["hp_regen"] = definition.HitPointRegen.ToString(),
                ["respawn_hours"] = definition.RespawnHours.ToString(),
                ["undead"] = definition.Undead ? "1" : "0",
                ["non_living"] = definition.NonLiving ? "1" : "0",
                ["negated_ability_ids"] = string.Join(",", definition.NegatedAbilityIds),
                ["npc_legacy_id"] = definition.LegacyId.ToString(),
                ["guarded_npc_legacy_ids"] = string.Join(",", definition.GuardedNpcLegacyIds),
                ["resist_water"] = definition.WaterResistance.ToString(),
                ["spell_immunity_level"] = definition.SpellImmunityLevel.ToString(),
                ["non_attack_item_legacy_id"] = definition.NonAttackItemLegacyId.ToString(),
                ["game_limit"] = definition.GameLimit.ToString(),
                ["gender"] = definition.Gender.ToString(),
                ["monster_type"] = definition.MonsterType.ToString(),
                ["experience_multiplier"] = definition.ExperienceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var state = new RuntimeLivingNpcState(
                instanceId,
                source,
                definition.Id,
                definition.Name,
                roomId,
                roomId,
                Math.Max(1, definition.HitPoints),
                Math.Max(1, definition.HitPoints),
                true,
                null,
                "summoned_quest_intent",
                0,
                metadata);
            _npcs[instanceId] = state;
            // Summoned NPCs are created after the living-world AI tables have already
            // been initialized. Register a matching AI state immediately so scheduler,
            // aggro, death, and persistence paths never index a missing instance key.
            _npcAi[instanceId] = new RuntimeNpcAiState(
                instanceId,
                "idle",
                null,
                roomId,
                roomId,
                0,
                DateTimeOffset.UtcNow);
            PublishLivingWorldEventLocked("npc-summoned", instanceId, roomId, DateTimeOffset.UtcNow);
            Save();
            SaveLivingWorldCompletionLocked();
            return state;
        }
    }

    public IReadOnlyList<RuntimeLivingWorldObjectDefinition> ObjectsInRoom(string roomId)
    {
        lock (_gate)
            return _objectDefinitions.Values.Where(x=>x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RuntimeWizardKnockDoor ResolveWizardKnockDoor(string roomId, string query)
    {
        lock (_gate)
        {
            var normalized = NormalizeDirection(query);
            var matches = _objectDefinitions.Values.Where(definition =>
                    definition.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase)
                    && definition.Kind.Equals("door", StringComparison.OrdinalIgnoreCase)
                    && _objects.TryGetValue(definition.Id, out var state)
                    && (!state.Open || state.Locked)
                    && (string.IsNullOrWhiteSpace(query)
                        || definition.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
                        || definition.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || NormalizeDirection(definition.Direction ?? string.Empty).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (matches.Length == 0) throw new InvalidOperationException("There is no closed door here matching that target.");
            if (matches.Length > 1) throw new InvalidOperationException("More than one closed door matches; specify its direction or generated ID.");
            var definition = matches[0];
            var reciprocal = _objectDefinitions.Values.SingleOrDefault(candidate =>
                candidate.Kind.Equals("door", StringComparison.OrdinalIgnoreCase)
                && candidate.RoomId.Equals(definition.TargetRoomId, StringComparison.OrdinalIgnoreCase)
                && candidate.TargetRoomId?.Equals(definition.RoomId, StringComparison.OrdinalIgnoreCase) == true
                && NormalizeDirection(candidate.Direction ?? string.Empty).Equals(OppositeDirection(definition.Direction), StringComparison.OrdinalIgnoreCase));
            return new RuntimeWizardKnockDoor(definition, _objects[definition.Id], reciprocal,
                reciprocal is not null && _objects.TryGetValue(reciprocal.Id, out var reciprocalState) ? reciprocalState : null);
        }
    }

    public void SetWizardKnockDoorState(RuntimeWizardKnockDoor door, bool opened)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _objects[door.Definition.Id] = door.State with { Open = opened ? true : door.State.Open, Locked = opened ? false : door.State.Locked, UpdatedAtUtc = now };
            if (door.ReciprocalDefinition is not null && door.ReciprocalState is not null)
                _objects[door.ReciprocalDefinition.Id] = door.ReciprocalState with { Open = opened ? true : door.ReciprocalState.Open, Locked = opened ? false : door.ReciprocalState.Locked, UpdatedAtUtc = now };
            Save();
            SaveLivingWorldCompletionLocked();
        }
    }

    private static string OppositeDirection(string? direction) => NormalizeDirection(direction ?? string.Empty) switch
    {
        "N" => "S", "S" => "N", "E" => "W", "W" => "E", "U" => "D", "D" => "U", _ => string.Empty
    };

    public bool CanTraverse(RuntimeRoom room, string direction, out string failure)
    {
        var normalized = NormalizeDirection(direction);
        lock (_gate)
        {
            if (!_world.TryResolveExit(room, normalized, out var exit, out _, out failure) || exit is null)
                return false;

            // Compatibility support for older evidence catalogs. Promoted MMExplorer
            // movement authority lives directly on the map-qualified exit record.
            foreach (var trigger in _world.FindMovementTriggers(room.MapId, room.RoomNumber, normalized))
            {
                if (!trigger.TargetRoomExists || trigger.HasInvalidDestination)
                {
                    failure = $"Movement trigger {trigger.Id} blocks this route because its generated destination is unavailable.";
                    return false;
                }
            }

            var door = _objectDefinitions.Values.FirstOrDefault(x =>
                x.RoomId.Equals(room.Id,StringComparison.OrdinalIgnoreCase)
                && x.Kind.Equals("door",StringComparison.OrdinalIgnoreCase)
                && NormalizeDirection(x.Direction ?? string.Empty).Equals(normalized,StringComparison.OrdinalIgnoreCase));
            if (door is null) { failure=string.Empty; return true; }
            if (!_objects.TryGetValue(door.Id, out var state))
            {
                failure = $"{door.Name} has no runtime state.";
                return false;
            }
            if (state.Locked) { failure=$"{door.Name} is locked."; return false; }
            if (!state.Open) { failure=$"{door.Name} is closed."; return false; }
            failure=string.Empty; return true;
        }
    }


    public string DescribeExit(RuntimeRoom room, string direction)
    {
        var normalized = NormalizeDirection(direction);
        lock (_gate)
        {
            var door = _objectDefinitions.Values.FirstOrDefault(x =>
                x.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)
                && x.Kind.Equals("door", StringComparison.OrdinalIgnoreCase)
                && NormalizeDirection(x.Direction ?? string.Empty).Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (door is null || !_objects.TryGetValue(door.Id, out var state)) return normalized;
            var flags = new List<string>();
            if (!state.Open) flags.Add("closed");
            if (state.Locked) flags.Add("locked");
            return flags.Count == 0 ? normalized : $"{normalized} [{string.Join(", ", flags)}]";
        }
    }

    public string Interact(RuntimeSession session, RuntimeCommand command, RuntimeItemState? inventory)
    {
        var query=command.ArgumentText.Trim();
        lock (_gate)
        {
            var matches=_objectDefinitions.Values.Where(x=>x.RoomId.Equals(session.CurrentRoom.Id,StringComparison.OrdinalIgnoreCase)
                && ((query.Length > 0 && (x.Id.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(query,StringComparison.OrdinalIgnoreCase)))
                    || MatchesAlias(x, command.Name, query))).ToArray();
            if(matches.Length==0)return $"You do not see an interactive object matching '{query}' here.";
            if(matches.Length>1)return $"More than one object matches '{query}'; use its generated ID.";
            var definition=matches[0]; var before=_objects[definition.Id];
            try
            {
                string? successOutput = null;
                if (definition.Metadata.TryGetValue("success_message_ids", out var encodedMessageIds))
                {
                    var messageIds = encodedMessageIds
                        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(value => int.TryParse(value, out var id) && id > 0
                            ? id
                            : throw new InvalidOperationException($"Decoded interaction {definition.Id} has an invalid success message ID '{value}'."))
                        .ToArray();
                    if (messageIds.Length == 0)
                        throw new InvalidOperationException($"Decoded interaction {definition.Id} has no valid success message IDs.");
                    var messages = RuntimeClassicNativeTextblockPayloadAuthority.Load(_projectRoot);
                    successOutput = string.Join(Environment.NewLine, messageIds.Select(messages.RequireMessage));
                }
                var after=command.Name switch
                {
                    "open" => Open(definition,before),
                    "close" => Close(definition,before),
                    "unlock" => Unlock(definition,before,inventory),
                    "lock" => Lock(definition,before,inventory),
                    "pull" or "push" or "activate" => Activate(definition,before),
                    _ when MatchesAlias(definition, command.Name, query) => Activate(definition,before),
                    _ => throw new InvalidOperationException("unsupported-world-interaction")
                };
                _objects[definition.Id]=after; Save();
                if (successOutput is not null) return successOutput;
                return $"You {command.Name} {definition.Name}.";
            }
            catch { _objects[definition.Id]=before; throw; }
        }
    }

    public IReadOnlyList<string> Tick(DateTimeOffset nowUtc)
    {
        var events=new List<string>();
        lock (_gate)
        {
            // MajorMUD lair regeneration is admitted by room entry after the delay has
            // elapsed; the global heartbeat must not materialize due NPCs into empty rooms.
            events.AddRange(TickLivingWorldCompletionLocked(nowUtc));
            events.AddRange(TickNpcTimedStatusesLocked(nowUtc));
            if(events.Count>0){ Save(); SaveLivingWorldCompletionLocked(); }
        }
        return events;
    }

    public bool ApplyNpcDamage(string instanceId,int damage,DateTimeOffset nowUtc)
    {
        if(damage<0)throw new ArgumentOutOfRangeException(nameof(damage));
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)||!current.Alive)return false;
            var hp=Math.Max(0,current.CurrentHitPoints-damage);
            if (hp == 0)
            {
                EnterNpcRespawnWaitLocked(instanceId, nowUtc, "combat-damage", null);
                PublishLivingWorldEventLocked("npc-died", instanceId, current.RoomId, nowUtc);
            }
            else
            {
                _npcs[instanceId]=current with { CurrentHitPoints=hp };
                PublishLivingWorldEventLocked("npc-damaged", instanceId, current.RoomId, nowUtc);
            }
            Save(); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public bool ApplyNpcHealing(string instanceId,int healing,DateTimeOffset nowUtc)
    {
        if(healing<0)throw new ArgumentOutOfRangeException(nameof(healing));
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)||!current.Alive)return false;
            var hp=Math.Min(current.MaximumHitPoints,current.CurrentHitPoints+healing);
            _npcs[instanceId]=current with { CurrentHitPoints=hp };
            PublishLivingWorldEventLocked("npc-healed", instanceId, current.RoomId, nowUtc);
            Save(); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public RuntimeLivingWorldSnapshot Capture()
    {
        lock(_gate)return new(WorldId,_npcs.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray(),_objects.Values.OrderBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).ToArray(),DateTimeOffset.UtcNow,_items.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public IReadOnlyList<string> Validate()
    {
        lock(_gate)
        {
            var failures=new List<string>();
            if(_npcs.Values.Any(x=>x.CurrentHitPoints<0||x.CurrentHitPoints>x.MaximumHitPoints))failures.Add("invalid-npc-hit-points");
            if(_npcs.Values.Any(x=>!x.Alive&&x.RespawnAtUtc is null&&!x.Metadata.ContainsKey("npc_respawn_block_reason")))failures.Add("dead-npc-missing-respawn-or-block");
            if(_npcs.Values.Any(x=>_world.FindRoomById(x.RoomId) is null))failures.Add("npc-room-unresolved");
            if(_spawnPlacementIssues.Count>0)failures.Add("spawn-placement-unresolved");
            if (!_world.NpcAiCatalog.Passed) failures.Add("npc-ai-catalog-invalid");
            if (!_world.StatusEffectCatalog.Passed) failures.Add("status-effect-catalog-invalid");
            if (!_world.LootRuleCatalog.Passed) failures.Add("loot-rule-catalog-invalid");
            if (_npcs.Values.Any(x => !x.Metadata.ContainsKey("npc_ai_authority") && !x.InteractionModel.Equals("summoned_quest_intent", StringComparison.OrdinalIgnoreCase))) failures.Add("npc-ai-authority-missing");
            if(_objects.Keys.Any(x=>!_objectDefinitions.ContainsKey(x)))failures.Add("unknown-world-object-state");
            return failures;
        }
    }

    private void LoadGeneratedNpcInstances()
    {
        var spawnTimingCatalog = RuntimeMajorMudSpawnTimingCatalog.Load(_projectRoot);
        foreach(var spawn in _world.Spawns)
        {
            if (!TryResolveSpawnRoom(spawn, out var room, out var reason))
            {
                _spawnPlacementIssues.Add(new(spawn.Id, FormatSourceRoomReference(spawn), spawn.ResolvedRuntimeRoomId, reason));
                continue;
            }

            var definitions = ResolveMaterializedDefinitions(spawn, room);
            if (definitions.Count == 0)
            {
                _spawnPlacementIssues.Add(new(spawn.Id, FormatSourceRoomReference(spawn), spawn.ResolvedRuntimeRoomId, "spawn-has-no-resolved-npc-definition"));
                continue;
            }

            var ordinal=0;
            foreach(var npc in definitions)
            {
                ordinal++;
                var id=$"{spawn.Id}:{npc.Id}:{ordinal}";
                var aiRelationship = _world.NpcAiCatalog.FindBySpawnId(spawn.Id)
                    ?? throw new InvalidOperationException($"NPC AI relationship missing for generated spawn '{spawn.Id}'.");
                var spawnTiming = spawnTimingCatalog.Resolve(spawn, npc);
                var metadata = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source_room_reference"] = FormatSourceRoomReference(spawn),
                    ["resolved_runtime_room_id"] = room.Id,
                    ["source_map_id"] = spawn.SourceMapId.ToString(),
                    ["source_room_number"] = spawn.SourceRoomNumber.ToString(),
                    ["alignment"] = npc.Alignment,
                    ["disposition"] = ResolveDisposition(npc, spawn),
                    ["combat_hostility_state"] = "idle",
                    ["npc_ai_authority"] = "generated:spawns.json",
                    ["npc_ai_schema_version"] = _world.NpcAiCatalog.SchemaVersion,
                    ["npc_ai_room_reference"] = aiRelationship.RoomReference,
                    ["npc_ai_monster_group_id"] = aiRelationship.MonsterGroupId.ToString(),
                    ["npc_ai_respawn_minutes"] = aiRelationship.RespawnMinutes.ToString(),
                    ["npc_ai_interaction_model"] = aiRelationship.InteractionModel,
                    ["npc_respawn_timer_mode"] = spawnTiming.Mode,
                    ["npc_respawn_timer_source_value"] = spawnTiming.SourceValue.ToString(),
                    ["npc_respawn_timer_source_unit"] = spawnTiming.SourceUnit,
                    ["npc_respawn_timer_authority"] = spawnTiming.Authority,
                    ["npc_respawn_timer_confidence"] = spawnTiming.Confidence,
                    ["npc_respawn_timer_effective_minutes"] = spawnTiming.EffectiveMinutes.ToString(),
                    ["npc_respawn_native_minute_gate_pending"] = spawnTiming.NativeMinuteGatePending ? "true" : "false",
                    ["npc_respawn_native_room_entry_pending"] = spawnTiming.NativeRoomEntryAdmissionPending ? "true" : "false",
                    ["npc_respawn_timer_note"] = spawnTiming.Note,
                    ["see_hidden"] = npc.SeeHidden ? "1" : "0",
                    ["animal"] = npc.Animal ? "1" : "0",
                    ["charm_level"] = npc.CharmLevel.ToString(),
                    ["charm_resistance"] = npc.CharmResistance.ToString(),
                    ["follow_value"] = npc.FollowValue.ToString(),
                    ["regen_time"] = npc.RegenTime.ToString(),
                    ["hp_regen"] = npc.HitPointRegen.ToString(),
                    ["respawn_hours"] = npc.RespawnHours.ToString(),
                    ["undead"] = npc.Undead ? "1" : "0",
                    ["non_living"] = npc.NonLiving ? "1" : "0",
                    ["negated_ability_ids"] = string.Join(",", npc.NegatedAbilityIds),
                    ["npc_legacy_id"] = npc.LegacyId.ToString(),
                    ["guarded_npc_legacy_ids"] = string.Join(",", npc.GuardedNpcLegacyIds),
                    ["resist_water"] = npc.WaterResistance.ToString(),
                    ["spell_immunity_level"] = npc.SpellImmunityLevel.ToString(),
                    ["non_attack_item_legacy_id"] = npc.NonAttackItemLegacyId.ToString(),
                    ["game_limit"] = npc.GameLimit.ToString(),
                    ["gender"] = npc.Gender.ToString(),
                    ["monster_type"] = npc.MonsterType.ToString(),
                    ["experience_multiplier"] = npc.ExperienceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum_area_population"] = spawn.MaximumAreaPopulation.ToString()
                };
                _npcs[id]=new(id,spawn.Id,npc.Id,npc.Name,room.Id,room.Id,
                    Math.Max(1,npc.HitPoints),Math.Max(1,npc.HitPoints),true,null,aiRelationship.InteractionModel,spawnTiming.EffectiveMinutes,metadata);
            }
        }
    }

    private bool TryResolveSpawnRoom(RuntimeNpcSpawn spawn, out RuntimeRoom room, out string reason)
    {
        room = null!;
        reason = string.Empty;

        if (spawn.SourceMapId <= 0 || spawn.SourceRoomNumber <= 0)
        {
            reason = "missing-map-qualified-source-room";
            return false;
        }

        var expectedRoomId = new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber).ToString();
        room = _world.FindRoom(spawn.SourceMapId, spawn.SourceRoomNumber)!;
        if (room is null)
        {
            reason = $"map-qualified-room-not-found:{expectedRoomId}";
            return false;
        }

        if (!room.Id.Equals(expectedRoomId, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"resolved-room-id-mismatch:{expectedRoomId}->{room.Id}";
            return false;
        }

        if (!spawn.ResolvedRuntimeRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"spawn-runtime-room-mismatch:{spawn.ResolvedRuntimeRoomId}->{room.Id}";
            return false;
        }

        return true;
    }

    private IReadOnlyList<RuntimeNpcDefinition> ResolveMaterializedDefinitions(RuntimeNpcSpawn spawn, RuntimeRoom room)
    {
        if (spawn.BossDefinition is not null)
        {
            return new[] { spawn.BossDefinition };
        }

        var explicitIds = ReadExplicitNpcLegacyIds(room.NpcDefinition);
        if (explicitIds.Count > 0)
        {
            return explicitIds
                .Select(id => _world.Npcs.TryGetValue(id, out var npc) ? npc : null)
                .Where(npc => npc is not null)
                .Cast<RuntimeNpcDefinition>()
                .DistinctBy(npc => npc.Id, StringComparer.OrdinalIgnoreCase)
                .Take(ResolveSpawnMaterializationLimit(spawn, ReadMaximumNpcCount(room.NpcDefinition)))
                .ToArray();
        }

        return spawn.GroupDefinitions
            .OrderBy(npc => npc.LegacyId)
            .Take(ResolveSpawnMaterializationLimit(spawn, Math.Max(1, ReadMaximumNpcCount(room.NpcDefinition))))
            .ToArray();
    }

    private static int ResolveSpawnMaterializationLimit(RuntimeNpcSpawn spawn, int roomLimit)
    {
        var normalizedRoomLimit = Math.Max(1, roomLimit);
        return spawn.MaximumAreaPopulation > 0
            ? Math.Min(normalizedRoomLimit, spawn.MaximumAreaPopulation)
            : normalizedRoomLimit;
    }

    private static IReadOnlyList<int> ReadExplicitNpcLegacyIds(string npcDefinition)
    {
        if (string.IsNullOrWhiteSpace(npcDefinition)) return Array.Empty<int>();
        var payload = npcDefinition;
        var colon = payload.IndexOf(':');
        if (colon >= 0) payload = payload[(colon + 1)..];
        var bracket = payload.IndexOf('[');
        if (bracket >= 0) payload = payload[..bracket];
        return payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }

    private static int ReadMaximumNpcCount(string npcDefinition)
    {
        if (string.IsNullOrWhiteSpace(npcDefinition)) return 1;
        var marker = "Max";
        var index = npcDefinition.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 1;
        var start = index + marker.Length;
        while (start < npcDefinition.Length && char.IsWhiteSpace(npcDefinition[start])) start++;
        var end = start;
        while (end < npcDefinition.Length && char.IsDigit(npcDefinition[end])) end++;
        return end > start && int.TryParse(npcDefinition[start..end], out var count) ? Math.Max(1, count) : 1;
    }

    private static string ResolveDisposition(RuntimeNpcDefinition npc, RuntimeNpcSpawn spawn)
    {
        if (spawn.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            spawn.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
            spawn.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
        {
            return "friendly";
        }

        if (npc.Alignment.Contains("evil", StringComparison.OrdinalIgnoreCase))
        {
            return "hostile";
        }

        return "neutral";
    }

    private static string FormatSourceRoomReference(RuntimeNpcSpawn spawn) =>
        spawn.SourceMapId > 0 && spawn.SourceRoomNumber > 0
            ? new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber).ToString()
            : spawn.RoomId;

    private void LoadGeneratedWorldObjects()
    {
        LoadDecodedExitObjects();
        LoadDecodedRoomInteractions();
    }

    private void LoadGeneratedRoomItems()
    {
        var path=Path.Combine(_projectRoot,_world.Definition.DataRoot,"room_item_placements.json");
        if(!File.Exists(path))return;
        foreach(var value in RuntimeChunkedClassicCatalog.LoadRecords(path))
        {
            var placementId=JsonValueReader.ReadString(value,"id",string.Empty);
            var roomId=JsonValueReader.ReadString(value,"room_id",string.Empty);
            var itemId=JsonValueReader.ReadString(value,"item_id",string.Empty);
            var quantity=Math.Max(1,JsonValueReader.ReadInt(value,"quantity",1));
            if(string.IsNullOrWhiteSpace(placementId)||_world.FindRoomById(roomId) is null||string.IsNullOrWhiteSpace(itemId))continue;
            _=_itemCatalog.Require(itemId);
            var metadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
            {
                ["static_placement_id"]=placementId,
                ["generated_static_room_item"]="true"
            };
            if(JsonValueReader.ReadString(value,"visibility","visible").Equals("hidden_searchable",StringComparison.OrdinalIgnoreCase))
                metadata["hidden_at_cleanup"]="true";
            var definition=new RuntimeItemInstance(placementId,itemId,null,RuntimeItemLocations.Room,roomId,null,0,quantity,null,metadata);
            _staticRoomItemDefinitions[placementId]=definition;
            _items[placementId]=definition;
        }
    }

    private static string NormalizeCanonicalItemId(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return string.Empty;
        if (trimmed.StartsWith("item.", StringComparison.OrdinalIgnoreCase)) return trimmed;
        return int.TryParse(trimmed, out var itemId) && itemId > 0
            ? $"item.{itemId}"
            : trimmed;
    }

    private void LoadDecodedExitObjects()
    {
        var exitsPath = Path.Combine(_projectRoot, _world.Definition.DataRoot, _world.Definition.ExitsFile);
        if (!File.Exists(exitsPath)) return;
        foreach (var value in RuntimeChunkedClassicCatalog.LoadRecords(exitsPath))
        {
            var gateKind = JsonValueReader.ReadString(value, "gate_kind", string.Empty);
            if (!gateKind.Equals("door", StringComparison.OrdinalIgnoreCase)
                && !gateKind.Equals("key", StringComparison.OrdinalIgnoreCase)) continue;
            var id = JsonValueReader.ReadString(value, "id", string.Empty);
            var roomId = ReadRelationship(value, "from_room_id", "from_room");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomId)) continue;
            var direction = JsonValueReader.ReadString(value, "direction", string.Empty);
            var targetRoomId = ReadRelationship(value, "to_room_id", "to_room");
            var qualifier = JsonValueReader.ReadString(value, "qualifier", "Door");
            var keyItemId = string.Empty;
            if (value.TryGetProperty("prerequisites", out var prerequisites) && prerequisites.ValueKind == JsonValueKind.Object)
                keyItemId = NormalizeCanonicalItemId(JsonValueReader.ReadString(prerequisites, "required_item_id", string.Empty));
            var initiallyOpen = false;
            var initiallyLocked = gateKind.Equals("key", StringComparison.OrdinalIgnoreCase);
            if (value.TryGetProperty("edge_state_semantics", out var edgeState) && edgeState.ValueKind == JsonValueKind.Object)
            {
                var initialState = JsonValueReader.ReadString(edgeState, "initial_state", string.Empty);
                if (!string.IsNullOrWhiteSpace(initialState))
                {
                    initiallyOpen = initialState.Contains("open", StringComparison.OrdinalIgnoreCase)
                                    && !initialState.Contains("closed", StringComparison.OrdinalIgnoreCase);
                    initiallyLocked = initialState.Contains("locked", StringComparison.OrdinalIgnoreCase)
                                      && !initialState.Contains("unlocked", StringComparison.OrdinalIgnoreCase);
                }
            }
            var definition = new RuntimeLivingWorldObjectDefinition(
                id, roomId, "door", string.IsNullOrWhiteSpace(qualifier) ? $"{direction} door" : qualifier,
                direction, keyItemId, initiallyOpen, initiallyLocked, targetRoomId, ReadMetadata(value));
            _objectDefinitions[id] = definition;
            _objects[id] = new(id, definition.InitiallyOpen, definition.InitiallyLocked, false, DateTimeOffset.UtcNow);
        }
    }

    private void LoadDecodedRoomInteractions()
    {
        var path = Path.Combine(_projectRoot, _world.Definition.DataRoot, "room_interactions.json");
        if (!File.Exists(path)) return;
        foreach (var value in RuntimeChunkedClassicCatalog.LoadRecords(path))
        {
            var id = JsonValueReader.ReadString(value, "id", string.Empty);
            var roomId = ReadRelationship(value, "from_room_id", "from_room");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomId)) continue;
            var direction = JsonValueReader.ReadString(value, "direction", string.Empty);
            var kind = JsonValueReader.ReadString(value, "interaction_kind", "room_interaction");
            var commands = ReadStringArray(value, "commands");
            var name = commands.FirstOrDefault()
                ?? (kind.Equals("exit_action", StringComparison.OrdinalIgnoreCase)
                    ? $"{direction} exit action"
                    : kind.Replace('_', ' '));
            var definition = new RuntimeLivingWorldObjectDefinition(
                id, roomId, kind, name, direction, string.Empty, true, false, string.Empty,
                ReadMetadata(value, commands));
            _objectDefinitions[id] = definition;
            _objects[id] = new(id, true, false, false, DateTimeOffset.UtcNow);
        }
    }

    private void Restore()
    {
        var path=PersistencePath; if(!File.Exists(path))return;
        try
        {
            var snapshot=JsonSerializer.Deserialize<RuntimeLivingWorldSnapshot>(File.ReadAllText(path));
            if(snapshot is null||!snapshot.WorldId.Equals(WorldId,StringComparison.OrdinalIgnoreCase))return;
            foreach(var npc in snapshot.Npcs)
            {
                if(!_npcs.TryGetValue(npc.InstanceId,out var generated))continue;
                _npcs[npc.InstanceId]=npc with
                {
                    RoomId = generated.RoomId,
                    HomeRoomId = generated.HomeRoomId,
                    Metadata = generated.Metadata
                };
            }
            foreach(var state in snapshot.Objects)if(_objects.ContainsKey(state.Id))_objects[state.Id]=state;
            if(snapshot.Items is not null)
            {
                _items.Clear();
                foreach(var item in snapshot.Items.Where(item=>item.LocationKind.Equals(RuntimeItemLocations.Room,StringComparison.OrdinalIgnoreCase)))
                    _items[item.InstanceId]=item;
            }
        }
        catch(JsonException){ }
    }

    private void Save()
    {
        var path=PersistencePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(Capture(),new JsonSerializerOptions{WriteIndented=true})); File.Move(temp,path,true);
    }

    private string PersistencePath=>Path.Combine(_persistenceRoot,"living-world.json");
    private static RuntimeLivingWorldObjectState Open(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s){if(s.Locked)throw new InvalidOperationException($"{d.Name} is locked.");return s with{Open=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Close(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)=>s with{Open=false,UpdatedAtUtc=DateTimeOffset.UtcNow};
    private static RuntimeLivingWorldObjectState Unlock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);return s with{Locked=false,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Lock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);if(s.Open)throw new InvalidOperationException($"Close {d.Name} first.");return s with{Locked=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Activate(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)
    {
        if(d.Metadata.TryGetValue("repeatability",out var repeatability))
        {
            if(repeatability.Equals("does_not_increment_progress",StringComparison.OrdinalIgnoreCase))return s;
            if(repeatability.Contains("idempotent_per_latch_cycle",StringComparison.OrdinalIgnoreCase)&&s.Activated)return s;
        }
        var latchStep=d.Metadata.TryGetValue("runtime_disposition",out var disposition)
            && disposition.Equals("active_bound_action_latch_step",StringComparison.OrdinalIgnoreCase);
        return s with{Activated=latchStep||!s.Activated,UpdatedAtUtc=DateTimeOffset.UtcNow};
    }
    private static void RequireKey(RuntimeLivingWorldObjectDefinition d,RuntimeItemState? inventory){if(string.IsNullOrWhiteSpace(d.KeyItemId))return;if(inventory is null||!inventory.Items.Any(x=>x.CanonicalItemId.Equals(d.KeyItemId,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException($"You need {d.KeyItemId}.");}
    private static string NormalizeDirection(string value)=>value.Trim().ToUpperInvariant() switch{"NORTH"=>"N","SOUTH"=>"S","EAST"=>"E","WEST"=>"W","NORTHEAST"=>"NE","NORTHWEST"=>"NW","SOUTHEAST"=>"SE","SOUTHWEST"=>"SW","UP"=>"U","DOWN"=>"D",var x=>x};
    private static bool ReadBool(JsonElement e,string name,bool fallback)=>e.TryGetProperty(name,out var v)?v.ValueKind==JsonValueKind.True||v.ValueKind==JsonValueKind.String&&bool.TryParse(v.GetString(),out var b)&&b:fallback;
    private static string ReadRelationship(JsonElement element,string directName,string relationshipName)
    {
        var direct=JsonValueReader.ReadString(element,directName,string.Empty);
        if(!string.IsNullOrWhiteSpace(direct))return direct;
        return element.TryGetProperty("relationships",out var relationships)&&relationships.ValueKind==JsonValueKind.Object
            ? JsonValueReader.ReadString(relationships,relationshipName,string.Empty)
            : string.Empty;
    }
    private static IReadOnlyList<string> ReadStringArray(JsonElement element,string name)=>
        element.TryGetProperty(name,out var values)&&values.ValueKind==JsonValueKind.Array
            ? values.EnumerateArray().Where(value=>value.ValueKind==JsonValueKind.String).Select(value=>value.GetString()??string.Empty).Where(value=>!string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();
    private static IReadOnlyDictionary<string,string> ReadMetadata(JsonElement element,IReadOnlyList<string>? commands=null)
    {
        var metadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var disposition=JsonValueReader.ReadString(element,"runtime_disposition",string.Empty);
        if(!string.IsNullOrWhiteSpace(disposition))metadata["runtime_disposition"]=disposition;
        foreach(var property in new[]{"bound_exit_id","bound_edge_state_id","action_number"})
        {
            var value=JsonValueReader.ReadString(element,property,string.Empty);
            if(!string.IsNullOrWhiteSpace(value))metadata[property]=value;
        }
        if(commands is not null&&commands.Count>0)metadata["commands"]=string.Join("|",commands);
        if(element.TryGetProperty("command_semantics",out var semantics)&&semantics.ValueKind==JsonValueKind.Object)
        {
            foreach(var property in new[]{"required_skill","skill_modifier","delay_seconds","failure_textblock_id","skill_test_model",
                         "required_room_item_id","room_item_failure_textblock_id","required_inventory_item_id",
                         "inventory_item_failure_textblock_id","consume_inventory_item","native_textblock_id",
                         "consume_room_item","return_room_id","repeatability","reset_timing","native_action_type","native_spell_id"})
            {
                if(!semantics.TryGetProperty(property,out var value)||value.ValueKind==JsonValueKind.Null)continue;
                metadata[property]=value.ToString();
            }
            if(semantics.TryGetProperty("success_remote_action",out var remote)&&remote.ValueKind==JsonValueKind.Array)
                metadata["success_remote_action"]=string.Join("|",remote.EnumerateArray().Select(value=>value.ToString()));
            if(semantics.TryGetProperty("success_message_ids",out var messages)&&messages.ValueKind==JsonValueKind.Array)
                metadata["success_message_ids"]=string.Join("|",messages.EnumerateArray().Select(value=>value.ToString()));
            if(semantics.TryGetProperty("random_textblock_ids",out var randoms)&&randoms.ValueKind==JsonValueKind.Array)
                metadata["random_textblock_ids"]=string.Join("|",randoms.EnumerateArray().Select(value=>value.ToString()));
        }
        return metadata;
    }
    private static bool MatchesAlias(RuntimeLivingWorldObjectDefinition definition,string verb,string query)
    {
        if(!definition.Metadata.TryGetValue("commands",out var encoded)||string.IsNullOrWhiteSpace(encoded))return false;
        var entered=$"{verb} {query}".Trim();
        return encoded.Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Any(alias=>alias.Equals(entered,StringComparison.OrdinalIgnoreCase)
                || (query.Length > 0 && alias.Equals(query,StringComparison.OrdinalIgnoreCase))
                || (query.Length == 0 && alias.Equals(verb,StringComparison.OrdinalIgnoreCase)));
    }

    private static int ParseLegacyNumber(string id)
    {
        var dot = id.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? id[(dot + 1)..] : id, out var value) ? value : 0;
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string,RuntimeLivingWorldService> _livingWorlds=new(StringComparer.OrdinalIgnoreCase);
    private RuntimeLivingWorldService Living(RuntimeSession session)=>Living(session.World);
    private RuntimeLivingWorldService Living(RuntimeWorld world)=>_livingWorlds.GetOrAdd(world.Definition.Id,_=>new RuntimeLivingWorldService(_characters.ProjectRoot,world));
    public IReadOnlyList<RuntimeLivingNpcState> LivingNpcsInRoom(RuntimeSession session)=>Living(session).NpcsInRoom(session.CurrentRoom.Id);
    public RuntimeLivingNpcState SummonLivingNpc(RuntimeSession session,string npcId,string source)=>Living(session).SummonNpc(npcId,session.CurrentRoom.Id,source);
    public bool CanTraverseLivingWorld(RuntimeSession session,string direction,out string failure)
    {
        var exit = session.World.FindExit(session.CurrentRoom, direction);
        if (exit is null)
        {
            failure = "You cannot go that way.";
            return false;
        }

        var context = BuildExitGateContext(session, null);

        var decision = Living(session).EvaluateExitGate(session.CurrentRoom, direction, context);
        if (!decision.Allowed)
        {
            failure = DescribeExitGateFailure(decision.Code);
            return false;
        }

        return Living(session).CanTraverse(session.CurrentRoom,direction,out failure);
    }

    private RuntimeExitGateContext BuildExitGateContext(RuntimeSession session, string? commandText)
    {
        RuntimeBundleDSearchRuntime.EnsureLoaded(_characters.ProjectRoot, session.World.Definition.Id);
        var discoveredTargets = new HashSet<string>(
            RuntimeBundleDSearchRuntime.Registry.GetDiscoveredTargets(session.Actor.Id),
            StringComparer.OrdinalIgnoreCase);

        var itemState = LoadItemState(session);
        var itemIds = itemState?.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.CanonicalItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        RuntimeCharacterRecord? character = session.Actor is RuntimeCharacterActor actor
            ? _characters.LoadCharacter(actor.Id)
            : null;
        var abilityValues = new Dictionary<int,long>();
        if (character is not null)
        {
            foreach (var pair in character.Skills)
            {
                var abilityId = ParseDecodedLegacyId(pair.Key);
                if (abilityId > 0) abilityValues[abilityId] = pair.Value;
            }
        }

        var completedActions = character?.Conditions
            .Where(pair => pair.Key.StartsWith("action:", StringComparison.OrdinalIgnoreCase)
                || pair.Key.StartsWith("remote_action:", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key[(pair.Key.IndexOf(':') + 1)..])
            .ToArray() ?? Array.Empty<string>();

        return new RuntimeExitGateContext(
            itemIds,
            itemState?.Gold ?? 0,
            discoveredTargets,
            commandText)
        {
            Level = character?.Level ?? (session.Actor is RuntimeCharacterActor characterActor ? characterActor.Level : 1),
            ClassId = ParseDecodedLegacyId(character?.ClassId),
            RaceId = ParseDecodedLegacyId(character?.RaceId),
            AlignmentName = ReadCharacterCondition(character, "alignment", "alignment_name"),
            AbilityValues = abilityValues,
            PicklocksValue = ReadCharacterValue(character, "picklocks", "picklock", "locks"),
            StrengthValue = ReadCharacterValue(character, "strength", "str"),
            CompletedActions = completedActions
        };
    }

    private static int ParseDecodedLegacyId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var normalized = value.Trim();
        var separator = normalized.LastIndexOf('.');
        if (separator >= 0) normalized = normalized[(separator + 1)..];
        return int.TryParse(normalized, out var parsed) ? parsed : 0;
    }

    private static int ReadCharacterValue(RuntimeCharacterRecord? character, params string[] keys)
    {
        if (character is null) return 0;
        var value = 0;
        foreach (var key in keys)
        {
            if (character.Skills.TryGetValue(key, out var skill)) { value = skill; break; }
            if (character.BaseStats.TryGetValue(key, out var stat)) { value = stat; break; }
        }
        if (keys.Any(key => key.Equals("picklocks", StringComparison.OrdinalIgnoreCase) || key.Equals("picklock", StringComparison.OrdinalIgnoreCase)))
            return RuntimeClassicCharacterProgressionFormula.PicklocksValue(character);
        return value;
    }

    private static string ReadCharacterCondition(RuntimeCharacterRecord? character, params string[] keys)
    {
        if (character is null) return string.Empty;
        foreach (var key in keys)
            if (character.Conditions.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        return string.Empty;
    }

    private static string DescribeExitGateFailure(string code) => code switch
    {
        "door-locked" => "The door is locked.",
        "door-closed" => "The door is closed.",
        "hidden-undiscovered" => "You cannot find an exit in that direction.",
        "required-item-missing" => "You do not have the item required to pass.",
        "toll-insufficient" => "You do not have enough money to pay the toll.",
        "class-not-allowed" or "class-denied" => "Your class cannot pass this way.",
        "race-not-allowed" or "race-denied" => "Your race cannot pass this way.",
        "level-below-minimum" => "You are not experienced enough to pass this way.",
        "level-above-maximum" => "You can no longer pass this way.",
        "alignment-below-minimum" or "alignment-above-maximum" => "Your alignment prevents you from passing.",
        "ability-below-minimum" or "ability-above-maximum" => "You lack the required ability to pass.",
        "required-actions-incomplete" or "required-action-order" => "You have not completed the required actions.",
        "picklocks-or-strength-failed" => "You cannot overcome this barrier.",
        "destination-blocked" => "That route does not currently lead anywhere.",
        "unsupported-evidence-only" or "qualified-unknown-evidence-only" or "gate-evidence-only" or "required-actions-unbound-evidence-only" => "The decoded exit condition cannot be executed.",
        "native-passive-event-pending" => "This hidden route is controlled by a native room event that has not occurred.",
        _ => $"You cannot pass ({code})."
    };
    public bool TryExecuteTeleporterCommand(RuntimeSession session, RuntimeCommand command, out string result)
    {
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var living = Living(session);
        var matches = living.CaptureGates().Where(g =>
            g.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
            && g.GateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase)
            && g.Commands.Any(alias => alias.Equals(entered, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 0) { result = string.Empty; return false; }
        if (matches.Length > 1) { result = "The decoded command matches more than one teleporter exit."; return true; }
        var gate = matches[0];
        var decision = living.EvaluateExitGate(session.CurrentRoom, gate.Direction, BuildExitGateContext(session, entered));
        if (!decision.Allowed) { result = $"The teleporter does not activate ({decision.Code})."; return true; }
        var previous = session.CurrentRoom;
        if (!CanMove(session, out var movementFailure)) { result = movementFailure; return true; }
        // The exact entered phrase was already evaluated above. Re-evaluating this
        // text-command gate through direction-only traversal drops that phrase and
        // incorrectly rejects a valid decoded command.
        if (!session.TryMove(gate.Direction, out var movementResult)) { result = movementResult; return true; }
        BroadcastDeparture(session, previous.Id, gate.Direction);
        BroadcastArrival(session, gate.Direction);
        Save(session);
        result = RenderRoom(session);
        return true;
    }

    public bool TryExecuteDecodedWorldCommand(RuntimeSession session, RuntimeCommand command, out string result)
    {
        if (TryExecuteTeleporterCommand(session, command, out result)) return true;
        if (TryExecuteClassicCasinoCommand(session, command, out result)) return true;
        if (TryExecuteClassicRoomActionScript(session, command, out result)) return true;
        var living = Living(session);
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var matches = living.CaptureWorldObjectDefinitions().Where(definition =>
            definition.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
            && definition.Metadata.TryGetValue("commands", out var encoded)
            && encoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(alias => alias.Equals(entered, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 0)
        {
            if (RuntimeMegaMudRouteCompatibility.TryResolve(
                    _characters.ProjectRoot, session.World, session.CurrentRoom, entered,
                    out var legacyTarget, out var legacyTransition))
            {
                if (!CanMove(session, out var movementFailure))
                {
                    result = movementFailure;
                    return true;
                }

                session.MoveToWorld(
                    session.World,
                    legacyTarget,
                    $"megamud-route-compat:{legacyTransition.FromSignature}:{legacyTransition.NormalizedCommand}");
                Save(session);
                AdmitClassicRoomEntryNpcRespawns(session);
                result = RenderRoom(session);
                return true;
            }

            result = string.Empty;
            return false;
        }
        if (matches.Length > 1) { result = "The decoded command matches more than one room interaction."; return true; }
        if (matches[0].Kind.Equals("native_support_invasion", StringComparison.OrdinalIgnoreCase))
        {
            result=_wccInvasions.Execute(session,entered,living);return true;
        }
        if (matches[0].Metadata.TryGetValue("required_skill",out var requiredSkill))
        {
            result=ExecuteDecodedSkillInteraction(session,command,matches[0],requiredSkill);
            return true;
        }
        if (matches[0].Metadata.ContainsKey("required_inventory_item_id"))
        {
            result=ExecuteDecodedItemInteraction(session,command,matches[0]);
            return true;
        }
        result = living.Interact(session, command, LoadItemState(session));
        return true;
    }

    private string ExecuteDecodedItemInteraction(
        RuntimeSession session,
        RuntimeCommand command,
        RuntimeLivingWorldObjectDefinition definition)
    {
        if(session.Actor is not RuntimeCharacterActor actor)throw new InvalidOperationException("A persistent character is required for the decoded item action.");
        var before=_characters.LoadCharacter(actor.Id)??throw new InvalidOperationException("Character save was not found for the decoded item action.");
        var state=before.ItemState??new RuntimeItemState(Array.Empty<RuntimeItemInstance>(),0,new Dictionary<string,string>(),Array.Empty<RuntimeCorpseState>());
        var requiredId=definition.Metadata["required_inventory_item_id"];
        var item=state.Items.FirstOrDefault(candidate=>
            candidate.CanonicalItemId.Equals(requiredId,StringComparison.OrdinalIgnoreCase)
            &&candidate.OwnerId?.Equals(actor.Id,StringComparison.OrdinalIgnoreCase)==true
            &&(candidate.LocationKind.Equals(RuntimeItemLocations.Character,StringComparison.OrdinalIgnoreCase)
               ||candidate.LocationKind.Equals(RuntimeItemLocations.Equipment,StringComparison.OrdinalIgnoreCase)));
        if(item is null)throw new InvalidOperationException($"You need {requiredId}.");
        var consume=!definition.Metadata.TryGetValue("consume_inventory_item",out var consumeText)
            ||!bool.TryParse(consumeText,out var parsedConsume)||parsedConsume;
        var items=consume
            ? state.Items.SelectMany(candidate=>candidate.InstanceId.Equals(item.InstanceId,StringComparison.OrdinalIgnoreCase)
                ? candidate.StackQuantity>1 ? new[]{candidate with{StackQuantity=candidate.StackQuantity-1}} : Array.Empty<RuntimeItemInstance>()
                : new[]{candidate}).ToArray()
            : state.Items;
        var updated=before with{ItemState=state with{Items=items},UpdatedAtUtc=DateTimeOffset.UtcNow};
        _characters.SaveCharacter(updated,$"decoded-room-item-action:{definition.Id}");
        try{return Living(session).Interact(session,command,updated.ItemState);}
        catch{_characters.SaveCharacter(before,$"decoded-room-item-action-rollback:{definition.Id}");throw;}
    }

    private string ExecuteDecodedSkillInteraction(
        RuntimeSession session,
        RuntimeCommand command,
        RuntimeLivingWorldObjectDefinition definition,
        string requiredSkill)
    {
        if(session.Actor is not RuntimeCharacterActor actor)throw new InvalidOperationException("A persistent character is required for the decoded skill test.");
        var character=_characters.LoadCharacter(actor.Id)??throw new InvalidOperationException("Character save was not found for the decoded skill test.");
        var skillValue=ReadCharacterSkillValue(character,requiredSkill);
        var modifier=definition.Metadata.TryGetValue("skill_modifier",out var modifierText)&&int.TryParse(modifierText,out var parsedModifier)?parsedModifier:0;
        var roll=Random.Shared.Next(1,101);
        if(!RuntimeClassicSkillTest.Passes(skillValue,modifier,roll))
        {
            if(!definition.Metadata.TryGetValue("failure_textblock_id",out var failureText)||!int.TryParse(failureText,out var failureTextblockId))
                throw new InvalidOperationException("Decoded skill-test failure textblock is unavailable.");
            var nativePayloadPath=Path.Combine(_characters.ProjectRoot,"Data","Classic","native_textblock_payloads.json");
            if(File.Exists(nativePayloadPath))
            {
                var payloads=RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot);
                if(payloads.TryResolveTextblock(failureTextblockId,out var directPayload))return directPayload;
                var graph=RuntimeClassicTextblockGraphAuthority.Load(_characters.ProjectRoot);
                var failureNode=graph.Require(failureTextblockId);
                var failure=RuntimeClassicDecodedFailureBranch.Execute(
                    failureNode,character,payloads,
                    RuntimeSpellCatalog.Load(_characters.ProjectRoot,character.WorldId),
                    Random.Shared);
                if(failure.Character != character)
                    _characters.SaveCharacter(failure.Character,$"decoded-skill-test-failure:{definition.Id}:textblock.{failureTextblockId}");
                if(failure.Output.Count>0)return string.Join(Environment.NewLine,failure.Output);
                throw new InvalidOperationException($"Decoded skill-test failure textblock {failureTextblockId} has no renderable presentation operation.");
            }
            return "You strain against the rubble, but it does not move.";
        }
        return Living(session).Interact(session,command,LoadItemState(session));
    }

    private static bool HasClassicDisarmTrapsAbility(RuntimeCharacterRecord character)
    {
        foreach(var key in character.Skills.Keys)
        {
            if(key.Equals("traps",StringComparison.OrdinalIgnoreCase)
                ||key.Equals("disarmtraps",StringComparison.OrdinalIgnoreCase)
                ||key.Equals("disarm-traps",StringComparison.OrdinalIgnoreCase))
                return true;

            var normalized=key.Trim();
            var separator=normalized.LastIndexOf('.');
            if(separator>=0)normalized=normalized[(separator+1)..];
            if(int.TryParse(normalized,out var abilityId)&&abilityId==41)return true;
        }
        return false;
    }

    private static int ReadCharacterSkillValue(RuntimeCharacterRecord character,string skill)
    {
        var keys=skill.Trim().ToLowerInvariant() switch
        {
            "strength"=>new[]{"strength","str"},
            "agility"=>new[]{"agility","agi","agl"},
            "intellect"=>new[]{"intellect","int"},
            "wisdom"=>new[]{"wisdom","wil","willpower"},
            "health"=>new[]{"health","hea"},
            "charm"=>new[]{"charm","cha"},
            var other=>new[]{other}
        };
        foreach(var key in keys)
        {
            if(character.Skills.TryGetValue(key,out var skillValue))return skillValue;
            if(character.BaseStats.TryGetValue(key,out var statValue))return statValue;
        }
        return 0;
    }

    public string DescribeLivingExit(RuntimeSession session,string direction)=>Living(session).DescribeExit(session.CurrentRoom,direction);

    public bool TryExecuteClassicExitAction(RuntimeSession session,RuntimeCommand command,out string result)
    {
        result=string.Empty;
        var action=command.Name.ToLowerInvariant();
        var direction=command.ArgumentText.Trim();
        if(action.Equals("disarm",StringComparison.OrdinalIgnoreCase))
        {
            if(command.Arguments.Count<2||!command.Arguments[0].Equals("trap",StringComparison.OrdinalIgnoreCase))
            {
                result="DISARM TRAP <direction>";
                return true;
            }
            direction=command.Arguments[1];
        }
        if(string.IsNullOrWhiteSpace(direction))
        {
            result=action.Equals("disarm",StringComparison.OrdinalIgnoreCase)?"DISARM TRAP <direction>":$"{action.ToUpperInvariant()} <direction>";
            return true;
        }

        var living=Living(session);
        if(!living.TryGetClassicExitActionEvidence(session.CurrentRoom,direction,out var evidence))
            return false;

        if(action.Equals("disarm",StringComparison.OrdinalIgnoreCase))
        {
            if(!evidence.GateKind.Equals("damage_trap",StringComparison.OrdinalIgnoreCase)
                &&!evidence.GateKind.Equals("spell_trap",StringComparison.OrdinalIgnoreCase))
            {
                result="No decoded exit trap is present in that direction.";
                return true;
            }

            RuntimeCharacterRecord? trapCharacter=session.Actor is RuntimeCharacterActor trapActor?_characters.LoadCharacter(trapActor.Id):null;
            if(trapCharacter is null)
            {
                result="Character state is unavailable.";
                return true;
            }
            if(!HasClassicDisarmTrapsAbility(trapCharacter))
            {
                result="DISARM TRAP requires the Traps skill.";
                return true;
            }

            // Native help proves that a traps expert may attempt to disarm a trap only after
            // the trap has been found with SEARCH. The exact native find/disarm probability and
            // per-exit Para2 transition are still unresolved, so do not reuse the generic hidden-
            // object search roll or invent a successful state mutation here.
            result="DISARM TRAP is recognized and the Traps skill requirement is met, but native SEARCH discovery, the disarm roll, and the trap state transition remain evidence-gated.";
            return true;
        }

        if(!evidence.GateKind.Equals("door",StringComparison.OrdinalIgnoreCase)
            &&!evidence.GateKind.Equals("key",StringComparison.OrdinalIgnoreCase))
            return false;

        RuntimeCharacterRecord? character=session.Actor is RuntimeCharacterActor actor?_characters.LoadCharacter(actor.Id):null;
        if(character is null)
        {
            result="Character state is unavailable.";
            return true;
        }

        var allowsAction=action.Equals("picklock",StringComparison.OrdinalIgnoreCase)?evidence.AllowsPicklock:evidence.AllowsBash;
        if(!allowsAction)
        {
            result=$"No recovered {action.ToUpperInvariant()} path exists for that door.";
            return true;
        }

        var actionValue=0;
        var actionStat=string.Empty;
        if(action.Equals("picklock",StringComparison.OrdinalIgnoreCase))
        {
            actionValue=RuntimeClassicCharacterProgressionFormula.PicklocksValue(character);
            actionStat="Picklocks";
            if(actionValue<=0)
            {
                result="PICKLOCK requires the Picklocks skill.";
                return true;
            }
        }
        else
        {
            actionValue=ReadCharacterSkillValue(character,"strength");
            actionStat="Strength";
        }

        // The decoded room qualifier is authoritative about whether a door has a numeric
        // Picklocks/Strength requirement or permits any value to attempt the action.
        // Do not reinterpret this as a traversal check and do not invent the remaining
        // native random/success dispatcher around a character that meets the requirement.
        if(evidence.RequirementFor(action) is int requirement&&actionValue<requirement)
        {
            result=$"{action.ToUpperInvariant()} does not meet this door's recovered {actionStat} requirement ({requirement}).";
            return true;
        }

        var requirementText=evidence.RequirementFor(action) is int recovered
            ?$"recovered {actionStat} requirement {recovered} is met"
            :evidence.IsAnyRequirementFor(action)
                ?$"this door permits any {actionStat} value to attempt the action"
                :$"the {actionStat} requirement is not separately projected";
        result=$"{action.ToUpperInvariant()} is recognized and {requirementText}, but the native success roll, exact failure text, and state transition remain evidence-gated.";
        return true;
    }

    public string ExecuteLivingWorldCommand(RuntimeSession session,RuntimeCommand command)
    {
        if((command.Name is "picklock" or "disarm")&&TryExecuteClassicExitAction(session,command,out var nativeExitAction))return nativeExitAction;
        return TryExecuteDecodedWorldCommand(session,command,out var decoded)
            ? decoded
            : Living(session).Interact(session,command,LoadItemState(session));
    }
    public string RenderLivingWorldState(RuntimeSession session)
    {
        var world=Living(session);var snapshot=world.Capture();
        return $"Living world: NPC instances={snapshot.Npcs.Count}; active={snapshot.Npcs.Count(x=>x.Alive)}; exits={session.World.ExitCount}; legacy movement triggers={session.World.TriggerCount}; interaction objects={snapshot.Objects.Count}; validation={string.Join(',',world.Validate())}";
    }

    public Task TickLivingWorldsAsync(CancellationToken cancellationToken)
    {
        var now=DateTimeOffset.UtcNow;
        var schedulerStep=_classicNativeSchedulerClock.Advance(now);
        TickActiveCharacterRegeneration(now,schedulerStep);
        foreach(var world in _livingWorlds.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(schedulerStep.SlowDue) world.ApplyNativeSlowNpcRegeneration(now);
            world.Tick(now);
        }
        return Task.CompletedTask;
    }
}

public sealed class RuntimeLivingWorldSmokeReport
{
    public bool Passed=>Failures.Count==0;
    public int GeneratedSpawnCount{get;set;}
    public int GeneratedExitCount{get;set;}
    public int GeneratedInteractionCount{get;set;}
    public int LegacyMovementTriggerCount{get;set;}
    public int RuntimeNpcInstanceCount{get;set;}
    public int GeneratedWorldObjectCount{get;set;}
    public IReadOnlyList<string> GateKinds{get;set;}=Array.Empty<string>();
    public IReadOnlyList<string> ValidationSignals{get;set;}=Array.Empty<string>();
    public List<string> Checks{get;}=[];
    public List<string> Failures{get;}=[];

    public static RuntimeLivingWorldSmokeReport Run(string projectRoot,string worldId)
    {
        var report=new RuntimeLivingWorldSmokeReport();
        string? smokeRoot=null;
        try
        {
            var options=ServerRuntimeOptions.Load(projectRoot);
            var definition=options.Worlds.First(x=>x.Id.Equals(worldId,StringComparison.OrdinalIgnoreCase));
            var world=new RuntimeWorldLoader().Load(projectRoot,definition);
            smokeRoot=Path.Combine(Path.GetTempPath(),"reforged-living-world-smoke-"+Guid.NewGuid().ToString("N"));
            var service=new RuntimeLivingWorldService(projectRoot,world,smokeRoot);
            report.GeneratedSpawnCount=world.SpawnCount;
            report.GeneratedExitCount=world.ExitCount;
            report.GeneratedInteractionCount=CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,"room_interactions.json"));
            report.LegacyMovementTriggerCount=world.TriggerCount;
            report.RuntimeNpcInstanceCount=service.NpcInstanceCount;
            report.GeneratedWorldObjectCount=service.WorldObjectCount;
            report.GateKinds=world.AllRooms().SelectMany(room=>room.Exits).Select(x=>x.GateKind).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();

            Check(report,world.SpawnCount>0,"generated-spawn-loading");
            Check(report,world.RoomCount==CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,definition.RoomsFile)),"map-qualified-room-loading");
            Check(report,world.ExitCount==61976,"map-qualified-exit-loading-61976");

            var duplicate=world.AllRooms()
                .GroupBy(room=>room.RoomNumber)
                .FirstOrDefault(group=>group.Select(room=>room.MapId).Distinct().Count()>1);
            Check(report,duplicate is not null,"duplicate-room-number-found");
            if(duplicate is not null)
            {
                var copies=duplicate.OrderBy(room=>room.MapId).ToArray();
                Check(report,copies.All(room=>world.FindRoom(room.MapId,room.RoomNumber)?.Id==room.Id),"duplicate-room-number-map-separation");
                Check(report,copies.Select(room=>room.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==copies.Length,"room-identity-not-collapsed");
            }

            Check(report,report.GeneratedInteractionCount>0,"generated-room-interaction-loading");
            var phoenixRoom=world.FindRoom(9,1423);
            var phoenixPairedRoom=world.FindRoom(9,1425);
            Check(report,phoenixRoom is not null&&phoenixPairedRoom is not null,"decoded-phoenix-remoteaction-rooms");
            if(phoenixRoom is not null&&phoenixPairedRoom is not null)
            {
                Check(report,!service.CanTraverse(phoenixRoom,"w",out _),"decoded-phoenix-paired-door-initially-closed");
                service.RecordQuestRemoteAction("1423.66.0.3",phoenixRoom.Id,"living-world-smoke");
                Check(report,service.CanTraverse(phoenixRoom,"w",out _)
                    &&service.CanTraverse(phoenixPairedRoom,"e",out _),"decoded-phoenix-remoteaction-opens-paired-door");
                var remoteEvent=service.EventsAfter(0).LastOrDefault(value=>value.Kind=="quest-remote-action"&&value.EntityId=="1423.66.0.3");
                Check(report,remoteEvent is not null
                    &&remoteEvent.Metadata.GetValueOrDefault("native_behavior")=="toggle_paired_door_state"
                    &&remoteEvent.Metadata.GetValueOrDefault("resulting_native_state")=="0","decoded-phoenix-remoteaction-event-proof");
                service.RecordQuestRemoteAction("1423.66.0.3",phoenixRoom.Id,"living-world-smoke");
                Check(report,!service.CanTraverse(phoenixRoom,"w",out _)
                    &&!service.CanTraverse(phoenixPairedRoom,"e",out _),"decoded-phoenix-remoteaction-native-repeat-toggle");
                var wizardKnockDoor=service.ResolveWizardKnockDoor(phoenixRoom.Id,"w");
                service.SetWizardKnockDoorState(wizardKnockDoor,opened:true);
                Check(report,service.CanTraverse(phoenixRoom,"w",out _)
                    &&service.CanTraverse(phoenixPairedRoom,"e",out _),"decoded-wizard-knock-opens-and-unlocks-paired-door");
                service.SetWizardKnockDoorState(wizardKnockDoor,opened:false);
                Check(report,!service.CanTraverse(phoenixRoom,"w",out _)
                    &&!service.CanTraverse(phoenixPairedRoom,"e",out _),"decoded-wizard-knock-transactional-state-restore");
                var stateBitResults=new List<string>();
                foreach(var signature in new[]{"3042.0.1.0","3042.0.2.0","3042.0.3.0","3042.0.4.0"})
                {
                    service.RecordQuestRemoteAction(signature,phoenixRoom.Id,"living-world-smoke");
                    stateBitResults.Add(service.EventsAfter(0).Last(value=>value.Kind=="quest-remote-action"&&value.EntityId==signature)
                        .Metadata.GetValueOrDefault("resulting_native_state",string.Empty));
                }
                Check(report,stateBitResults.SequenceEqual(new[]{"224","192","128","8"}),"decoded-type6-native-state-bit-sequence");
            }
            var generatedRoomItemCount=CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,"room_item_placements.json"));
            var generatedRoomItems=service.Capture().Items??Array.Empty<RuntimeItemInstance>();
            Check(report,generatedRoomItemCount==363&&service.GroundItemCount==363&&generatedRoomItems.Count==363,
                "generated-room-item-placement-materialization");
            Check(report,generatedRoomItems.Count(RuntimeClassicHiddenSearchService.IsHidden)==261
                &&generatedRoomItems.Count(item=>!RuntimeClassicHiddenSearchService.IsHidden(item))==102,
                "generated-room-item-visibility-classification");
            Check(report,generatedRoomItems.All(item=>world.FindRoomById(item.LocationId) is not null),
                "generated-room-item-map-qualified-placement");
            var resetCatalog=RuntimeItemCatalog.Load(projectRoot,worldId);
            var resetProbe=generatedRoomItems.First(item=>!RuntimeClassicHiddenSearchService.IsHidden(item)
                &&resetCatalog.Require(item.CanonicalItemId).GameLimit<=0);
            var takenProbe=service.TakeGroundItem(resetProbe.InstanceId,"room-item-reset-smoke");
            var maintenanceReset=service.ResetGeneratedRoomItems(new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
                {[resetProbe.CanonicalItemId]=1});
            Check(report,takenProbe.OwnerId=="room-item-reset-smoke"&&maintenanceReset.RestoredCount==1
                &&maintenanceReset.GameLimitBlockedCount==0&&service.GroundItemCount==363,
                "generated-room-item-maintenance-reset-restores-normal-owned-item");
            var limitedGroups=generatedRoomItems.GroupBy(item=>item.CanonicalItemId,StringComparer.OrdinalIgnoreCase)
                .Select(group=>new{Items=group.ToArray(),Definition=resetCatalog.Require(group.Key)})
                .Where(group=>group.Definition.GameLimit>0&&group.Items.Sum(item=>item.StackQuantity)<=group.Definition.GameLimit)
                .ToArray();
            var limitedProbe=limitedGroups.First().Items[0];
            _=service.TakeGroundItem(limitedProbe.InstanceId,"room-item-limit-smoke");
            var limitedReset=service.ResetGeneratedRoomItems(
                new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
                    {[limitedProbe.CanonicalItemId]=limitedGroups.First().Definition.GameLimit},
                unlimitedItemsEnabled:false);
            Check(report,limitedReset.RestoredCount==0&&limitedReset.GameLimitBlockedCount>=1
                &&service.GroundItemCount==362,"generated-room-item-reset-honors-game-limit-when-unlimited-disabled");
            var restoringReset=service.ResetGeneratedRoomItems(
                new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
                    {[limitedProbe.CanonicalItemId]=limitedGroups.First().Definition.GameLimit},
                unlimitedItemsEnabled:true);
            Check(report,restoringReset.RestoredCount==1&&restoringReset.GameLimitBlockedCount==0
                &&service.GroundItemCount==363,"generated-room-item-reset-bypasses-game-limit-when-unlimited-enabled");
            var idempotentReset=service.ResetGeneratedRoomItems();
            Check(report,idempotentReset.RestoredCount==0&&idempotentReset.ExistingCount==363,
                "generated-room-item-reset-idempotent-restoration");
            Check(report,world.ExitCount>world.TriggerCount,"movement-authority-from-map-qualified-exits");
            var generatedBlockedDestinations = RuntimeChunkedClassicCatalog
                .LoadRecords(Path.Combine(projectRoot,definition.DataRoot,definition.ExitsFile))
                .Select(RuntimeExit.FromGeneratedRecord)
                .Count(exit=>exit.HasBlockedDestination);
            Check(report,
                world.AllRooms().SelectMany(room=>room.Exits).Count(exit=>exit.HasBlockedDestination)==generatedBlockedDestinations,
                "invalid-destination-exit-enforcement");

            var expandedCandidateCount=world.Spawns.Where(spawn=>spawn.PrimaryRoomAvailable&&spawn.FullyResolved)
                .Sum(spawn=>spawn.BossDefinition is not null ? 1 : spawn.GroupDefinitions.Count);
            Check(report,expandedCandidateCount>0,"generated-spawn-runtime-authority");
            Check(report,service.NpcInstanceCount>0&&service.NpcInstanceCount<expandedCandidateCount,"persistent-npc-instantiation");
            Check(report,service.GateCount==world.ExitCount,"generated-exit-gate-catalog");
            Check(report,report.GateKinds.Count>0&&report.GateKinds.All(x=>!string.IsNullOrWhiteSpace(x)),"generated-gate-kind-classification");
            var candleRoom=world.FindRoom(1,2252);
            var candleGate=candleRoom is null ? null : service.CaptureGates().SingleOrDefault(gate=>
                gate.ExitId.Equals("exit.map1.2252.n",StringComparison.OrdinalIgnoreCase));
            if(candleRoom is null||candleGate is null)report.Failures.Add("decoded-candle-exit-unavailable");
            else
            {
                var expectedCandleAliases=new[]{"turn right candle left","twist right candle left","turn right candlestick left"};
                Check(report,candleGate.RoomId=="room.map1.2252"&&candleGate.Direction.Equals("n",StringComparison.OrdinalIgnoreCase)
                    &&candleGate.Commands.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expectedCandleAliases),
                    "decoded-candle-exit-source-and-aliases");
                foreach(var alias in expectedCandleAliases)
                {
                    var candleSession=new RuntimeSession("candle-smoke-"+Guid.NewGuid().ToString("N"),world,candleRoom,
                        new RuntimeDeveloperAvatar("candle-smoke","Candle Smoke"),"smoke");
                    var facade=new RuntimeLiveGameplayFacade(new RuntimeCharacterService(projectRoot),new RuntimeSessionManager());
                    var handled=facade.TryExecuteDecodedWorldCommand(candleSession,RuntimeCommand.Parse(alias,world.CommandCatalog),out _);
                    Check(report,handled&&candleSession.CurrentRoom.Id.Equals("room.map1.2254",StringComparison.OrdinalIgnoreCase),
                        "decoded-candle-command-traversal:"+alias);
                }
            }
            var levelGateRoom=world.FindRoomById("room.map1.2146");
            var levelGate=levelGateRoom is null ? null : world.FindExit(levelGateRoom,"W");
            if(levelGateRoom is null||levelGate is null)report.Failures.Add("newhaven-level-range-gate-unavailable");
            else
            {
                var levelOneDecision=service.EvaluateExitGate(levelGateRoom,levelGate.Direction,new RuntimeExitGateContext(
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),0,new HashSet<string>(StringComparer.OrdinalIgnoreCase),null){Level=1});
                var levelThreeDecision=service.EvaluateExitGate(levelGateRoom,levelGate.Direction,new RuntimeExitGateContext(
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),0,new HashSet<string>(StringComparer.OrdinalIgnoreCase),null){Level=3});
                var levelFourDecision=service.EvaluateExitGate(levelGateRoom,levelGate.Direction,new RuntimeExitGateContext(
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),0,new HashSet<string>(StringComparer.OrdinalIgnoreCase),null){Level=4});
                Check(report,levelGate.GateKind.Equals("level_range",StringComparison.OrdinalIgnoreCase)
                    &&levelGate.Qualifier.Equals("Level: 1 to 3",StringComparison.OrdinalIgnoreCase)
                    &&levelOneDecision.Allowed
                    &&levelThreeDecision.Allowed
                    &&!levelFourDecision.Allowed
                    &&levelFourDecision.Code=="level-above-maximum",
                    "newhaven-2146-west-level-range-inclusive");
            }
            var actionGates=service.CaptureGates().Where(gate=>gate.RequiredActionCount>0).ToArray();
            var boundActionGates=actionGates.Where(gate=>gate.BoundActionIds.Count>=gate.RequiredActionCount).ToArray();
            var evidenceOnlyActionGates=actionGates.Where(gate=>gate.BoundActionIds.Count<gate.RequiredActionCount).ToArray();
            var nativePassiveActionGates=evidenceOnlyActionGates.Where(gate=>gate.NativePassiveEffect).ToArray();
            var unresolvedActionGates=evidenceOnlyActionGates.Where(gate=>!gate.NativePassiveEffect).ToArray();
            Check(report,actionGates.Length==206,"generated-action-gate-count");
            Check(report,boundActionGates.Length==206,"generated-action-gate-specific-bindings");
            Check(report,evidenceOnlyActionGates.Length==0,"unbound-action-gates-fail-closed");
            Check(report,nativePassiveActionGates.Length==0,"native-passive-action-gates-classified");
            Check(report,unresolvedActionGates.Length==0,"ordinary-action-semantics-gaps-closed");
            var rubbleGate=boundActionGates.SingleOrDefault(gate=>gate.ExitId=="exit.map9.1012.n");
            var rubbleAction=rubbleGate is null ? null : service.CaptureWorldObjectDefinitions().SingleOrDefault(definition=>
                rubbleGate.BoundActionIds.Contains(definition.Id,StringComparer.OrdinalIgnoreCase));
            Check(report,rubbleGate is not null&&rubbleAction is not null
                &&rubbleAction.Metadata.GetValueOrDefault("required_skill")=="strength"
                &&rubbleAction.Metadata.GetValueOrDefault("skill_modifier")=="0"
                &&rubbleAction.Metadata.GetValueOrDefault("failure_textblock_id")=="1423"
                &&rubbleAction.Metadata.GetValueOrDefault("success_remote_action")=="1012|1840|0|0",
                "decoded-rubble-strength-action-bound");
            Check(report,RuntimeClassicSkillTest.SuccessChance(40,0)==40
                &&RuntimeClassicSkillTest.SuccessChance(100,10)==100
                &&RuntimeClassicSkillTest.SuccessChance(5,-10)==0
                &&RuntimeClassicSkillTest.Passes(40,0,40)
                &&!RuntimeClassicSkillTest.Passes(40,0,41),
                "decoded-skill-test-roll-boundaries");
            var rubbleRoom=rubbleGate is null ? null : world.FindRoomById(rubbleGate.RoomId);
            if(rubbleRoom is null)report.Failures.Add("decoded-rubble-room-unavailable");
            else
            {
                var rubbleSession=new RuntimeSession("rubble-smoke",world,rubbleRoom,new RuntimeDeveloperAvatar("rubble-smoke","Rubble Smoke"),"smoke");
                _=service.Interact(rubbleSession,RuntimeCommand.Parse("clear rubble"),null);
                var rubbleDecision=service.EvaluateExitGate(rubbleRoom,rubbleGate!.Direction,new RuntimeExitGateContext(
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),0,new HashSet<string>(StringComparer.OrdinalIgnoreCase),null));
                Check(report,rubbleDecision is {Allowed:true,Code:"hidden-action-open"},"decoded-rubble-success-opens-hidden-exit");
            }
            Check(report,nativePassiveActionGates.Length==0,"native-passive-action-gates-fully-linked");
            Check(report,new[]{"exit.map12.1920.u","exit.map12.2001.u"}.All(exitId=>
                boundActionGates.Any(gate=>gate.ExitId.Equals(exitId,StringComparison.OrdinalIgnoreCase)
                    && gate.BoundActionIds.Count==1)),"decoded-sphinx-action-gates-bound");
            Check(report,boundActionGates.Any(gate=>gate.ExitId=="exit.map12.2051.u"
                &&gate.BoundActionIds.Count==1),"decoded-stars-sphinx-action-gate-bound");
            var decodedBoulderExitIds=new[]{
                "exit.map17.2946.s","exit.map17.2947.n",
                "exit.map17.2949.e","exit.map17.2950.w",
                "exit.map17.2956.n","exit.map17.2957.s",
                "exit.map17.2958.n","exit.map17.2972.s",
                "exit.map17.2970.s","exit.map17.2971.n"};
            var decodedBoulderActions=service.CaptureWorldObjectDefinitions().Where(definition=>
                decodedBoulderExitIds.Contains(definition.Metadata.GetValueOrDefault("bound_exit_id"),StringComparer.OrdinalIgnoreCase)).ToArray();
            Check(report,decodedBoulderExitIds.All(exitId=>
                    boundActionGates.Any(gate=>gate.ExitId.Equals(exitId,StringComparison.OrdinalIgnoreCase)
                        &&gate.BoundActionIds.Count>=1))
                &&decodedBoulderActions.Length==10
                &&decodedBoulderActions.All(action=>
                    action.Metadata.GetValueOrDefault("required_skill")=="strength"
                    &&action.Metadata.GetValueOrDefault("skill_modifier")=="60"
                    &&action.Metadata.GetValueOrDefault("delay_seconds")=="5"
                    &&action.Metadata.GetValueOrDefault("failure_textblock_id")=="4235"
                    &&action.Metadata.ContainsKey("success_remote_action")),
                "decoded-map17-boulder-actions-bound");
            var pedestalGate=boundActionGates.SingleOrDefault(gate=>gate.ExitId=="exit.map17.3042.n");
            var pedestalActions=service.CaptureWorldObjectDefinitions().Where(definition=>
                definition.Metadata.GetValueOrDefault("bound_exit_id")=="exit.map17.3042.n").ToArray();
            Check(report,pedestalGate is not null&&pedestalGate.RequiredActionCount==4
                &&pedestalGate.BoundActionIds.Count==4&&pedestalActions.Length==4
                &&pedestalActions.Select(action=>action.Metadata.GetValueOrDefault("required_inventory_item_id"))
                    .OrderBy(value=>value,StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(new[]{"item.1921","item.1922","item.1923","item.1924"},StringComparer.OrdinalIgnoreCase)
                &&pedestalActions.All(action=>action.Metadata.GetValueOrDefault("consume_inventory_item")=="True"),
                "decoded-map17-pedestal-actions-bound");
            Check(report,nativePassiveActionGates.All(gate=>gate.BoundActionIds.Count<gate.RequiredActionCount),
                "unrelated-actions-cannot-open-native-passive-gate");
            Check(report,service.CaptureNpcAi().Count==service.NpcInstanceCount,"npc-ai-state-instantiation");
            var validation=service.Validate();
            report.ValidationSignals=validation.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            var placementIssues=service.CaptureSpawnPlacementIssues();
            Check(report,validation.All(IsClassifiedLivingWorldValidationSignal),"living-world-validator-hard-placement-signal");
            Check(report,placementIssues.All(issue=>!string.IsNullOrWhiteSpace(issue.SpawnId)&&!string.IsNullOrWhiteSpace(issue.SourceRoomReference)&&!string.IsNullOrWhiteSpace(issue.Reason)),"unresolved-spawn-placement-audit");
            var guildAndRiver=world.FindRoom(1,21);
            Check(report,guildAndRiver is not null&&guildAndRiver.Id.Equals("room.map1.21",StringComparison.OrdinalIgnoreCase),"guild-river-map-qualified-room");
            if(guildAndRiver is not null)
            {
                var visible=service.NpcsInRoom(guildAndRiver.Id).ToArray();
                Check(report,visible.Length<=1,"guild-river-no-group-five-explosion");
                Check(report,!visible.Any(x=>x.Name.Contains("storm giant",StringComparison.OrdinalIgnoreCase)),"guild-river-no-storm-giant-leak");
                Check(report,!visible.Any(x=>x.Name.Contains("woodelf",StringComparison.OrdinalIgnoreCase)),"guild-river-no-woodelf-leak");
                Check(report,visible.All(x=>x.RoomId.Equals(guildAndRiver.Id,StringComparison.OrdinalIgnoreCase)),"guild-river-visible-roomid-filter");
                Check(report,visible.All(x=>x.Metadata.TryGetValue("source_room_reference",out var source)&&source.Equals(guildAndRiver.Id,StringComparison.OrdinalIgnoreCase)),"guild-river-source-room-proof");
                Check(report,visible.All(x=>x.Metadata.TryGetValue("disposition",out var disposition)&&disposition.Equals("neutral",StringComparison.OrdinalIgnoreCase)),"guild-river-neutral-disposition");
            }

            var npc=service.Capture().Npcs.FirstOrDefault();
            if(npc is null)report.Failures.Add("npc-lifecycle-smoke-unavailable");
            else
            {
                var now=DateTimeOffset.UtcNow;
                Check(report,service.ApplyNpcDamage(npc.InstanceId,npc.MaximumHitPoints,now),"npc-death-transition");
                Check(report,service.CorpseCount==1,"npc-corpse-creation");
                Check(report,service.EventsAfter(0).Any(x=>x.Kind=="npc-died")&&service.EventsAfter(0).Any(x=>x.Kind=="corpse-created"),"multiplayer-event-sequencing");
                var due=now.AddMinutes(Math.Max(1,npc.RespawnMinutes));
                var tickEvents=service.Tick(due);
                Check(report,tickEvents.Any(x=>x.StartsWith("npc-respawn:",StringComparison.Ordinal)),"npc-respawn-scheduler");
                Check(report,tickEvents.Any(x=>x.StartsWith("corpse-expired:",StringComparison.Ordinal)),"corpse-cleanup-scheduler");
                Check(report,service.CaptureNpcAi().First(x=>x.InstanceId.Equals(npc.InstanceId,StringComparison.OrdinalIgnoreCase)).Mode=="idle","npc-ai-respawn-integration");
                var restored=new RuntimeLivingWorldService(projectRoot,world,smokeRoot);
                Check(report,restored.Capture().Npcs.Any(x=>x.InstanceId.Equals(npc.InstanceId,StringComparison.OrdinalIgnoreCase)&&x.Alive),"npc-persistence-reconnect");
                Check(report,restored.EventsAfter(0).Count>0,"scheduler-event-restart-recovery");
            }

            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand)) is not null,"world-interaction-routing");
            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.CanTraverseLivingWorld)) is not null,"restricted-exit-routing");
            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLivingWorldsAsync)) is not null,"living-world-scheduler-routing");
            Check(report,RuntimeCommand.SupportedCommands.Contains("open")&&RuntimeCommand.SupportedCommands.Contains("worldstate"),"living-world-command-catalog");
            Check(report,service.WorldObjectCount>=report.GeneratedInteractionCount,"generated-world-interactions-active");
        }
        catch(Exception ex){report.Failures.Add(ex.GetType().Name+":"+ex.Message);}
        finally{if(!string.IsNullOrWhiteSpace(smokeRoot)){try{Directory.Delete(smokeRoot,true);}catch{}}}
        return report;
    }

    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    private static void Check(RuntimeLivingWorldSmokeReport r,bool ok,string name){if(ok)r.Checks.Add(name);else r.Failures.Add(name);}
    private static bool IsClassifiedLivingWorldValidationSignal(string signal) =>
        signal.Equals("spawn-placement-unresolved",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("npc-ai-catalog-invalid",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("status-effect-catalog-invalid",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("loot-rule-catalog-invalid",StringComparison.OrdinalIgnoreCase);
    private static int CountGeneratedRecords(string path)
    {
        return RuntimeChunkedClassicCatalog.LoadRecords(path).Count;
    }
}
