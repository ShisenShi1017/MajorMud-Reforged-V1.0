using MajorMud.Reforged.Compiler;
using System.Text.Json;

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "Librarium")) && Directory.Exists(Path.Combine(dir.FullName, "Data"))) return dir.FullName;
        dir = dir.Parent;
    }
    throw new Exception("Repository root not found.");
}

var root = FindRoot();
var result = new LibrariumDataWriter().Compile(new LibrariumCompilerOptions(root, "CompilerSmokeTest", Clean: true));
if (!result.Success) throw new Exception("Compiler smoke test failed: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
if (result.ObjectFamiliesCompiled < 10) throw new Exception("Too few object families compiled.");
if (!File.Exists(Path.Combine(result.OutputRoot, "compile_manifest.json"))) throw new Exception("Manifest was not generated.");

foreach (var familyFile in Directory.EnumerateFiles(result.OutputRoot, "*.json"))
{
    var fileName = Path.GetFileName(familyFile);
    if (fileName is "compile_manifest.json" or "world_manifest.json" or "client_projection.json") continue;
    using var document = JsonDocument.Parse(File.ReadAllText(familyFile));
    if (!document.RootElement.TryGetProperty("authority", out var authority) || authority.GetString() != "Librarium")
        throw new Exception($"Generated family '{fileName}' is missing Librarium authority.");
}

using (var itemDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "items.json"))))
{
    var items = itemDocument.RootElement.GetProperty("records");
    if (items.GetArrayLength() != 1950) throw new Exception("Runtime item projection did not contain 1950 items.");
    var first = items[0];
    if (!first.TryGetProperty("item_type", out _)) throw new Exception("Runtime item projection is missing item_type.");
    if (!items.EnumerateArray().Any(x => x.TryGetProperty("combat", out var combat) && combat.ValueKind == JsonValueKind.Object))
        throw new Exception("Runtime item projection lost promoted combat profiles.");
}

using (var equipmentDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "equipment.json"))))
{
    var equipment = equipmentDocument.RootElement.GetProperty("records");
    if (equipment.GetArrayLength() < 500) throw new Exception("Runtime equipment projection is unexpectedly small.");
    foreach (var record in equipment.EnumerateArray())
    {
        if (!record.TryGetProperty("item_id", out var itemId) || string.IsNullOrWhiteSpace(itemId.GetString()))
            throw new Exception("Runtime equipment projection contains a record without item_id.");
        if (!record.TryGetProperty("slot", out var slot) || string.IsNullOrWhiteSpace(slot.GetString()))
            throw new Exception("Runtime equipment projection contains a record without slot.");
    }
}

using (var roomsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "rooms.json"))))
using (var exitsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "exits.json"))))
using (var triggersDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "triggers.json"))))
{
    var rooms = roomsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    var exits = exitsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    var triggers = triggersDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();

    if (rooms.Length != 26666) throw new Exception($"MMExplorer room projection expected 26666 rooms but found {rooms.Length}.");
    if (exits.Length != 61976) throw new Exception($"MMExplorer exit projection expected 61976 exits but found {exits.Length}.");
    if (triggers.Length != 284) throw new Exception($"MMExplorer interaction projection expected 284 triggers but found {triggers.Length}.");

    var roomIds = rooms.Select(record => ReadString(record, "id")).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!roomIds.Contains("room.map1.1") || !roomIds.Contains("room.map2.1"))
        throw new Exception("Map-qualified duplicate room number identities were not preserved.");

    var roomNumberOneMaps = rooms
        .Where(record => ReadInt(record, "room_number", ReadInt(record, "roomNumber", 0)) == 1)
        .Select(record => ReadInt(record, "map_number", ReadInt(record, "map_id", 0)))
        .Where(value => value > 0)
        .Distinct()
        .Count();
    if (roomNumberOneMaps < 2) throw new Exception("Room number 1 was collapsed across maps.");

    var gateKinds = exits
        .Select(record => ReadString(record, "gate_kind"))
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var gateKind in new[] { "door", "hidden", "required_item", "toll", "damage_trap", "spell_trap", "text_command", "cast_transition" })
    {
        if (!gateKinds.Contains(gateKind)) throw new Exception($"MMExplorer exit projection lost gate kind '{gateKind}'.");
    }

    var danglingExitIds = new[] { "exit.map1.62.u", "exit.map1.1809.d", "exit.map1.291.w", "exit.map1.2337.e", "exit.map17.2838.ne" };
    foreach (var exitId in danglingExitIds)
    {
        var exit = exits.FirstOrDefault(record => ReadString(record, "id").Equals(exitId, StringComparison.OrdinalIgnoreCase));
        if (exit.ValueKind == JsonValueKind.Undefined) throw new Exception($"Expected dangling exit '{exitId}' was not preserved.");
        if (!ReadString(exit, "runtime_disposition").Equals("invalid_destination_blocked", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Dangling exit '{exitId}' was not marked invalid_destination_blocked.");

        var targetRoomId = $"room.map{ReadInt(exit, "to_map_number", 0)}.{ReadInt(exit, "to_room_number", 0)}";
        if (roomIds.Contains(targetRoomId)) throw new Exception($"Dangling exit '{exitId}' target unexpectedly exists as '{targetRoomId}'.");
    }
}


using (var questsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputRoot, "quests.json"))))
{
    var quests = questsDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
    var decodedFlows = quests.Where(record => ReadString(record, "classification").Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (quests.Length != 2183) throw new Exception($"Quest projection expected 2183 records but found {quests.Length}.");
    if (decodedFlows.Length != 83) throw new Exception($"Quest projection expected 83 decoded flows but found {decodedFlows.Length}.");
    foreach (var flow in decodedFlows)
    {
        if (!flow.TryGetProperty("runtime_mapping", out var runtimeMapping) ||
            runtimeMapping.ValueKind != JsonValueKind.Object ||
            !ReadString(runtimeMapping, "status").Equals("executable", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Quest flow '{ReadString(flow, "id")}' was not promoted to executable runtime data.");

        if (!flow.TryGetProperty("script_flow", out var scriptFlow) ||
            scriptFlow.ValueKind != JsonValueKind.Object ||
            !ReadString(scriptFlow, "execution_status").Equals("authoritative_script_graph", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Quest flow '{ReadString(flow, "id")}' lost its authoritative decoded script graph.");

        if (scriptFlow.TryGetProperty("stages", out var stages) && stages.ValueKind == JsonValueKind.Array)
        {
            foreach (var stage in stages.EnumerateArray())
            {
                if (!stage.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) continue;
                foreach (var operation in operations.EnumerateArray())
                {
                    if (!ReadString(operation, "status").Equals("decoded", StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Quest flow '{ReadString(flow, "id")}' contains a non-decoded operation.");
                }
            }
        }
    }
}

Console.WriteLine($"PASS: compiled {result.ObjectsCompiled} records across {result.ObjectFamiliesCompiled} families with authoritative runtime item/equipment projections.");

static string ReadString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
        : string.Empty;
}

static int ReadInt(JsonElement element, string propertyName, int fallback)
{
    if (!element.TryGetProperty(propertyName, out var value)) return fallback;
    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
    return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : fallback;
}
