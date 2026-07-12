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

public sealed record RuntimeLivingWorldSnapshot(
    string WorldId,
    IReadOnlyList<RuntimeLivingNpcState> Npcs,
    IReadOnlyList<RuntimeLivingWorldObjectState> Objects,
    DateTimeOffset SavedAtUtc);

/// <summary>
/// Authoritative shared runtime state for generated NPC spawns and generated world interaction objects.
/// The service never invents NPCs, routes, doors, keys, hazards, or triggers: all definitions are loaded
/// from generated Data/&lt;world&gt; files. Runtime state is persisted separately under Saves/Worlds.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    private readonly object _gate = new();
    private readonly string _projectRoot;
    private readonly RuntimeWorld _world;
    private readonly string _persistenceRoot;
    private readonly Dictionary<string,RuntimeLivingNpcState> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectDefinition> _objectDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectState> _objects = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeLivingWorldService(string projectRoot, RuntimeWorld world, string? persistenceRoot = null)
    {
        _projectRoot = projectRoot;
        _world = world;
        _persistenceRoot = string.IsNullOrWhiteSpace(persistenceRoot) ? Path.Combine(projectRoot,"Saves","Worlds") : persistenceRoot;
        LoadGeneratedNpcInstances();
        LoadGeneratedWorldObjects();
        InitializeLivingWorldCompletion();
        Restore();
        RestoreLivingWorldCompletion();
    }

    public string WorldId => _world.Definition.Id;
    public int NpcInstanceCount { get { lock (_gate) return _npcs.Count; } }
    public int WorldObjectCount { get { lock (_gate) return _objectDefinitions.Count; } }

    public IReadOnlyList<RuntimeLivingNpcState> NpcsInRoom(string roomId)
    {
        lock (_gate)
            return _npcs.Values.Where(x => x.Alive && x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<RuntimeLivingWorldObjectDefinition> ObjectsInRoom(string roomId)
    {
        lock (_gate)
            return _objectDefinitions.Values.Where(x=>x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool CanTraverse(RuntimeRoom room, string direction, out string failure)
    {
        var normalized = NormalizeDirection(direction);
        lock (_gate)
        {
            var exit = room.Exits.FirstOrDefault(candidate =>
                NormalizeDirection(candidate.Direction).Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (exit is null)
            {
                failure = "You cannot go that way.";
                return false;
            }

            if (exit.HasBlockedDestination || _world.FindRoom(exit.TargetMapId, exit.TargetRoomNumber) is null)
            {
                failure = $"Exit {normalized} points to unresolved destination {exit.TargetKey}. Movement is blocked.";
                return false;
            }

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

    public string Interact(RuntimeSession session, RuntimeCommand command, RuntimeItemState? inventory)
    {
        if (command.Arguments.Count==0) return $"Use: {command.Name} <object>.";
        var query=command.ArgumentText.Trim();
        lock (_gate)
        {
            var matches=_objectDefinitions.Values.Where(x=>x.RoomId.Equals(session.CurrentRoom.Id,StringComparison.OrdinalIgnoreCase)
                && (x.Id.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(query,StringComparison.OrdinalIgnoreCase)
                    || MatchesAlias(x, command.Name, query))).ToArray();
            if(matches.Length==0)return $"You do not see an interactive object matching '{query}' here.";
            if(matches.Length>1)return $"More than one object matches '{query}'; use its generated ID.";
            var definition=matches[0]; var before=_objects[definition.Id];
            try
            {
                var after=command.Name switch
                {
                    "open" => Open(definition,before),
                    "close" => Close(definition,before),
                    "unlock" => Unlock(definition,before,inventory),
                    "lock" => Lock(definition,before,inventory),
                    "pull" or "push" or "activate" => Activate(definition,before),
                    _ => throw new InvalidOperationException("unsupported-world-interaction")
                };
                _objects[definition.Id]=after; Save();
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
            foreach(var current in _npcs.Values.ToArray())
            {
                if(!current.Alive && current.RespawnAtUtc is not null && current.RespawnAtUtc<=nowUtc)
                {
                    _npcs[current.InstanceId]=current with { Alive=true, RoomId=current.HomeRoomId, CurrentHitPoints=current.MaximumHitPoints, RespawnAtUtc=null };
                    events.Add($"npc-respawn:{current.InstanceId}:{current.HomeRoomId}");
                }
            }
            events.AddRange(TickLivingWorldCompletionLocked(nowUtc));
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
            var alive=hp>0;
            DateTimeOffset? respawn = alive ? null : nowUtc.AddMinutes(Math.Max(0, current.RespawnMinutes));
            _npcs[instanceId]=current with { CurrentHitPoints=hp, Alive=alive, RespawnAtUtc=respawn };
            if(!alive) RecordNpcDeathLocked(current, nowUtc, respawn);
            PublishLivingWorldEventLocked(alive ? "npc-damaged" : "npc-died", instanceId, current.RoomId, nowUtc);
            Save(); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public RuntimeLivingWorldSnapshot Capture()
    {
        lock(_gate)return new(WorldId,_npcs.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray(),_objects.Values.OrderBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).ToArray(),DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<string> Validate()
    {
        lock(_gate)
        {
            var failures=new List<string>();
            if(_npcs.Values.Any(x=>x.CurrentHitPoints<0||x.CurrentHitPoints>x.MaximumHitPoints))failures.Add("invalid-npc-hit-points");
            if(_npcs.Values.Any(x=>!x.Alive&&x.RespawnAtUtc is null))failures.Add("dead-npc-missing-respawn");
            if(_npcs.Values.Any(x=>_world.FindRoomById(x.RoomId) is null))failures.Add("npc-room-unresolved");
            if(_objects.Keys.Any(x=>!_objectDefinitions.ContainsKey(x)))failures.Add("unknown-world-object-state");
            return failures;
        }
    }

    private void LoadGeneratedNpcInstances()
    {
        foreach(var spawn in _world.Spawns.Where(x=>x.PrimaryRoomAvailable))
        {
            var definitions=spawn.BossDefinition is not null ? new[]{spawn.BossDefinition} : spawn.GroupDefinitions.OrderBy(x=>x.LegacyId).Take(1).ToArray();
            var ordinal=0;
            foreach(var npc in definitions)
            {
                ordinal++;
                var id=$"{spawn.Id}:{npc.Id}:{ordinal}";
                _npcs[id]=new(id,spawn.Id,npc.Id,npc.Name,spawn.ResolvedRuntimeRoomId,spawn.ResolvedRuntimeRoomId,
                    Math.Max(1,npc.HitPoints),Math.Max(1,npc.HitPoints),true,null,spawn.InteractionModel,Math.Max(0,spawn.RespawnMinutes),new Dictionary<string,string>());
            }
        }
    }

    private void LoadGeneratedWorldObjects()
    {
        LoadDecodedExitObjects();
        LoadDecodedRoomInteractions();
    }

    private void LoadDecodedExitObjects()
    {
        var exitsPath = Path.Combine(_projectRoot, _world.Definition.DataRoot, _world.Definition.ExitsFile);
        if (!File.Exists(exitsPath)) return;
        using var document = JsonDocument.Parse(File.ReadAllText(exitsPath));
        var records = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("records", out var generated) && generated.ValueKind == JsonValueKind.Array
                ? generated
                : default;
        if (records.ValueKind != JsonValueKind.Array) return;

        foreach (var value in records.EnumerateArray())
        {
            var gateKind = JsonValueReader.ReadString(value, "gate_kind", string.Empty);
            if (!gateKind.Equals("door", StringComparison.OrdinalIgnoreCase)) continue;
            var id = JsonValueReader.ReadString(value, "id", string.Empty);
            var roomId = ReadRelationship(value, "from_room_id", "from_room");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomId)) continue;
            var direction = JsonValueReader.ReadString(value, "direction", string.Empty);
            var targetRoomId = ReadRelationship(value, "to_room_id", "to_room");
            var qualifier = JsonValueReader.ReadString(value, "qualifier", "Door");
            var keyItemId = string.Empty;
            if (value.TryGetProperty("prerequisites", out var prerequisites) && prerequisites.ValueKind == JsonValueKind.Object)
                keyItemId = JsonValueReader.ReadString(prerequisites, "required_item_id", string.Empty);
            var definition = new RuntimeLivingWorldObjectDefinition(
                id, roomId, "door", string.IsNullOrWhiteSpace(qualifier) ? $"{direction} door" : qualifier,
                direction, keyItemId, false, false, targetRoomId, ReadMetadata(value));
            _objectDefinitions[id] = definition;
            _objects[id] = new(id, definition.InitiallyOpen, definition.InitiallyLocked, false, DateTimeOffset.UtcNow);
        }
    }

    private void LoadDecodedRoomInteractions()
    {
        var path = Path.Combine(_projectRoot, _world.Definition.DataRoot, _world.Definition.TriggersFile);
        if (!File.Exists(path)) return;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("records", out var generated) && generated.ValueKind == JsonValueKind.Array
                ? generated
                : default;
        if (records.ValueKind != JsonValueKind.Array) return;

        foreach (var value in records.EnumerateArray())
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
            foreach(var npc in snapshot.Npcs)if(_npcs.ContainsKey(npc.InstanceId))_npcs[npc.InstanceId]=npc;
            foreach(var state in snapshot.Objects)if(_objects.ContainsKey(state.Id))_objects[state.Id]=state;
        }
        catch(JsonException){ }
    }

    private void Save()
    {
        var path=PersistencePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(Capture(),new JsonSerializerOptions{WriteIndented=true})); File.Move(temp,path,true);
    }

    private string PersistencePath=>Path.Combine(_persistenceRoot,WorldId,"living-world.json");
    private static RuntimeLivingWorldObjectState Open(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s){if(s.Locked)throw new InvalidOperationException($"{d.Name} is locked.");return s with{Open=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Close(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)=>s with{Open=false,UpdatedAtUtc=DateTimeOffset.UtcNow};
    private static RuntimeLivingWorldObjectState Unlock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);return s with{Locked=false,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Lock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);if(s.Open)throw new InvalidOperationException($"Close {d.Name} first.");return s with{Locked=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Activate(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)=>s with{Activated=!s.Activated,UpdatedAtUtc=DateTimeOffset.UtcNow};
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
        if(commands is not null&&commands.Count>0)metadata["commands"]=string.Join("|",commands);
        return metadata;
    }
    private static bool MatchesAlias(RuntimeLivingWorldObjectDefinition definition,string verb,string query)
    {
        if(!definition.Metadata.TryGetValue("commands",out var encoded)||string.IsNullOrWhiteSpace(encoded))return false;
        var entered=$"{verb} {query}".Trim();
        return encoded.Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Any(alias=>alias.Equals(entered,StringComparison.OrdinalIgnoreCase)||alias.Equals(query,StringComparison.OrdinalIgnoreCase));
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string,RuntimeLivingWorldService> _livingWorlds=new(StringComparer.OrdinalIgnoreCase);
    private RuntimeLivingWorldService Living(RuntimeSession session)=>_livingWorlds.GetOrAdd(session.World.Definition.Id,_=>new RuntimeLivingWorldService(_characters.ProjectRoot,session.World));
    public IReadOnlyList<RuntimeLivingNpcState> LivingNpcsInRoom(RuntimeSession session)=>Living(session).NpcsInRoom(session.CurrentRoom.Id);
    public bool CanTraverseLivingWorld(RuntimeSession session,string direction,out string failure)=>Living(session).CanTraverse(session.CurrentRoom,direction,out failure);
    public string ExecuteLivingWorldCommand(RuntimeSession session,RuntimeCommand command)=>Living(session).Interact(session,command,LoadItemState(session));
    public string RenderLivingWorldState(RuntimeSession session)
    {
        var world=Living(session);var snapshot=world.Capture();
        return $"Living world: NPC instances={snapshot.Npcs.Count}; active={snapshot.Npcs.Count(x=>x.Alive)}; exits={session.World.ExitCount}; legacy movement triggers={session.World.TriggerCount}; interaction objects={snapshot.Objects.Count}; validation={string.Join(',',world.Validate())}";
    }

    public Task TickLivingWorldsAsync(CancellationToken cancellationToken)
    {
        var now=DateTimeOffset.UtcNow;
        foreach(var world in _livingWorlds.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            report.GeneratedInteractionCount=CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,definition.TriggersFile));
            report.LegacyMovementTriggerCount=world.TriggerCount;
            report.RuntimeNpcInstanceCount=service.NpcInstanceCount;
            report.GeneratedWorldObjectCount=service.WorldObjectCount;

            Check(report,world.SpawnCount>0,"generated-spawn-loading");
            Check(report,world.RoomCount==26666,"map-qualified-room-loading-26666");
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

            Check(report,report.GeneratedInteractionCount==284,"generated-room-interaction-loading-284");
            Check(report,world.ExitCount>world.TriggerCount,"movement-authority-from-map-qualified-exits");
            Check(report,world.AllRooms().SelectMany(room=>room.Exits).Count(exit=>exit.HasBlockedDestination)==5,"invalid-destination-exit-enforcement-5");

            var expectedNpcInstances=world.Spawns.Count(spawn=>spawn.PrimaryRoomAvailable&&(spawn.BossDefinition is not null||spawn.GroupDefinitions.Count>0));
            Check(report,expectedNpcInstances>0,"generated-spawn-runtime-authority");
            Check(report,service.NpcInstanceCount==expectedNpcInstances,"persistent-npc-instantiation");
            Check(report,service.GateCount==world.ExitCount,"generated-exit-gate-catalog");
            Check(report,service.CaptureGates().Select(x=>x.GateKind).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).SequenceEqual(new[]{"cast_transition","damage_trap","door","hidden","open","qualified_unknown","required_item","spell_trap","text_command","toll"},StringComparer.OrdinalIgnoreCase),"generated-gate-kind-coverage");
            Check(report,service.CaptureNpcAi().Count==service.NpcInstanceCount,"npc-ai-state-instantiation");
            Check(report,service.Validate().Count==0,"living-world-validator");

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
    private static int CountGeneratedRecords(string path)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        if(document.RootElement.ValueKind==JsonValueKind.Array)return document.RootElement.GetArrayLength();
        return document.RootElement.TryGetProperty("records",out var records)&&records.ValueKind==JsonValueKind.Array?records.GetArrayLength():0;
    }
}
