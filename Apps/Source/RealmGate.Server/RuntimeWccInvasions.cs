using System.Text.Json;

namespace RealmGate.Server;

public sealed class RuntimeWccInvasionCatalog
{
    private readonly IReadOnlyDictionary<int,string> _scripts;
    private readonly IReadOnlyDictionary<string,RuntimeWccInvasionDefinition> _definitions;
    private RuntimeWccInvasionCatalog(IReadOnlyDictionary<int,string> scripts,IReadOnlyDictionary<string,RuntimeWccInvasionDefinition> definitions)
    {
        _scripts=scripts;
        _definitions=definitions;
    }
    public string Require(int id)=>_scripts.TryGetValue(id,out var script)?script:throw new InvalidDataException($"Generated invasion script {id} is missing.");
    public RuntimeWccInvasionDefinition RequireDefinition(string command)=>_definitions.TryGetValue(command,out var definition)
        ?definition:throw new InvalidOperationException("Unknown native invasion trigger.");
    public static RuntimeWccInvasionCatalog Load(string projectRoot,string worldId)
    {
        var path=Path.Combine(projectRoot,"Data",worldId,"invasion_scripts.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var records=doc.RootElement.GetProperty("records").EnumerateArray().ToArray();
        if(records.Length!=31)throw new InvalidDataException($"Expected 31 generated invasion scripts, found {records.Length}.");
        var interactionRecords=RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(projectRoot,"Data",worldId,"room_interactions.json"))
            .Where(record=>JsonValueReader.ReadString(record,"interaction_kind",string.Empty).Equals("native_support_invasion",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if(interactionRecords.Length!=6)throw new InvalidDataException($"Expected six generated support-room invasion definitions, found {interactionRecords.Length}.");
        var definitions=new Dictionary<string,RuntimeWccInvasionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach(var record in interactionRecords)
        {
            if(JsonValueReader.ReadString(record,"runtime_disposition",string.Empty)!="active_runtime")throw new InvalidDataException("A generated invasion definition is not active.");
            var relationships=record.GetProperty("relationships");
            var semantics=record.GetProperty("command_semantics");
            var commands=record.GetProperty("commands").EnumerateArray().Select(value=>value.GetString()??string.Empty).Where(value=>value.Length>0).ToArray();
            var placements=semantics.GetProperty("random_textblock_ids").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
            var messages=semantics.GetProperty("success_message_ids").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
            if(commands.Length!=1||placements.Length==0||messages.Length!=1||!semantics.GetProperty("consume_room_item").GetBoolean())
                throw new InvalidDataException("A generated invasion definition is incomplete or ambiguous.");
            var definition=new RuntimeWccInvasionDefinition(commands[0],relationships.GetProperty("from_room").GetString()??string.Empty,
                semantics.GetProperty("required_room_item_id").GetString()??string.Empty,placements,messages[0],
                semantics.GetProperty("return_room_id").GetString()??string.Empty);
            if(!definitions.TryAdd(definition.Command,definition))throw new InvalidDataException($"Duplicate generated invasion command {definition.Command}.");
        }
        return new(records.ToDictionary(x=>x.GetProperty("textblock_id").GetInt32(),x=>x.GetProperty("script").GetString()??string.Empty),definitions);
    }
}

public sealed record RuntimeWccInvasionDefinition(string Command,string SupportRoomId,string GateItemId,int[] PlacementTextblockIds,int SuccessMessageId,string ReturnRoomId);

public sealed class RuntimeWccInvasionService
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeAccountSecurityService _security;
    public RuntimeWccInvasionService(RuntimeCharacterService characters)
    {
        _characters=characters;
        _security=new RuntimeAccountSecurityService(characters.ProjectRoot);
    }

    public string Execute(RuntimeSession session,string entered,RuntimeLivingWorldService living)
    {
        if(session.Actor is not RuntimeCharacterActor actor)throw new InvalidOperationException("A persistent authenticated character is required.");
        var character=_characters.LoadCharacter(actor.Id)??throw new InvalidOperationException("Character save was not found.");
        var credential=_security.TryLoad(character.AccountId);
        if(credential is null||!credential.Enabled||credential.Role is not (RuntimeAccountRole.Sysop or RuntimeAccountRole.Administrator))
            throw new InvalidOperationException("WCCMMUD SYSOP authority is required to trigger an invasion.");
        var catalog=RuntimeWccInvasionCatalog.Load(_characters.ProjectRoot,session.World.Definition.Id);
        var definition=catalog.RequireDefinition(entered);
        if(!session.CurrentRoom.Id.Equals(definition.SupportRoomId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Native invasions may only be triggered in the generated Sysop support room.");
        if(!definition.ReturnRoomId.Equals(definition.SupportRoomId,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Generated invasion return-room semantics do not preserve the support room.");
        var plans=new List<(string RoomId,string NpcId)>();
        foreach(var placementId in definition.PlacementTextblockIds)
        {
            string? roomId=null;
            foreach(var operation in Operations(catalog.Require(placementId)))
            {
                if(operation.Name.Equals("teleport",StringComparison.OrdinalIgnoreCase))
                {
                    roomId=$"room.map{operation.Arguments[1]}.{operation.Arguments[0]}";
                    if(session.World.FindRoomById(roomId) is null)throw new InvalidDataException($"Invasion destination {roomId} is not generated.");
                }
                else if(operation.Name.Equals("random",StringComparison.OrdinalIgnoreCase))
                {
                    if(roomId is null)throw new InvalidDataException($"Invasion placement {placementId} invokes random before teleport.");
                    var selected=SelectThreshold(catalog.Require(operation.Arguments[0]),Random.Shared.Next(1,101));
                    plans.AddRange(Operations(selected).Where(x=>x.Name.Equals("summon",StringComparison.OrdinalIgnoreCase)).Select(x=>(roomId,$"npc.{x.Arguments[0]}")));
                }
            }
        }
        foreach(var plan in plans)_=session.World.Npcs.ContainsKey(plan.NpcId.Split('.').Last() is var n&&int.TryParse(n,out var id)?id:0)
            ? true:throw new InvalidDataException($"Invasion NPC {plan.NpcId} is not generated.");
        if(!living.TryConsumeGroundItem(session.CurrentRoom.Id,definition.GateItemId))return "That invasion has already been triggered during this cleanup cycle.";
        var source=$"wcc-invasion:{entered.Replace(' ','-')}:";
        for(var index=0;index<plans.Count;index++)living.SummonNpc(plans[index].NpcId,plans[index].RoomId,$"{source}{index:D3}");
        var messages=RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var output=messages.RequireMessage(definition.SuccessMessageId);
        return string.IsNullOrWhiteSpace(output)?$"{entered} triggered; summoned {plans.Count} NPCs.":output;
    }

    private sealed record Op(string Name,int[] Arguments);
    private static IEnumerable<Op> Operations(string script)
    {
        var body=script.Contains(':')&&int.TryParse(script.Split(':',2)[0],out _)?script.Split(':',2)[1]:script;
        foreach(var raw in body.Split(':',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var parts=raw.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(parts.Length==0)continue;
            yield return new(parts[0],parts.Skip(1).Select(value=>int.Parse(value,System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        }
    }
    private static string SelectThreshold(string script,int roll)
    {
        foreach(var line in script.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var split=line.Split(':',2);if(split.Length==2&&int.TryParse(split[0],out var threshold)&&roll<=threshold)return split[1];
        }
        throw new InvalidDataException($"Invasion random table does not cover roll {roll}.");
    }
}
